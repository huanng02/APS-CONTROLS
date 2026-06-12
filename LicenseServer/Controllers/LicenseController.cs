using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseServer.Data;
using LicenseServer.Models;
using LicenseServer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LicenseServer.Controllers
{
    [ApiController]
    [Route("api/license")]
    public class LicenseController : ControllerBase
    {
        private readonly LicenseDbContext _context;
        private readonly IRsaSigningService _rsaService;

        public LicenseController(LicenseDbContext context, IRsaSigningService rsaService)
        {
            _context = context;
            _rsaService = rsaService;
        }

        // POST /api/license/create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateLicenseRequest request)
        {
            string key;
            do
            {
                key = GenerateLicenseKey();
            } while (await _context.Licenses.AnyAsync(l => l.LicenseKey == key));

            DateTime expireAt;
            if (request.ExpireDays == -1)
            {
                expireAt = new DateTime(3000, 12, 31, 0, 0, 0, DateTimeKind.Utc);
            }
            else
            {
                var expireDays = request.ExpireDays <= 0 ? 365 : request.ExpireDays;
                expireAt = DateTime.UtcNow.AddDays(expireDays);
            }

            var license = new License
            {
                LicenseKey = key,
                MaxMachines = request.MaxMachines <= 0 ? 4 : request.MaxMachines,
                Status = "NOT_ACTIVATED",
                ExpireDate = expireAt,
                CreatedAt = DateTime.UtcNow
            };

            _context.Licenses.Add(license);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                LicenseId = license.Id,
                LicenseKey = license.LicenseKey,
                MaxMachines = license.MaxMachines,
                Status = license.Status,
                ExpireAt = license.ExpireDate
            });
        }

        // POST /api/license/activate
        [HttpPost("activate")]
        public async Task<IActionResult> Activate([FromBody] ActivateLicenseRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LicenseKey) || string.IsNullOrWhiteSpace(request.MachineFingerprint))
            {
                return BadRequest(new { Message = "License key and machine fingerprint are required." });
            }

            var license = await _context.Licenses
                .Include(l => l.Machines)
                .FirstOrDefaultAsync(l => l.LicenseKey == request.LicenseKey);

            if (license == null)
            {
                return NotFound(new { Message = "License key not found." });
            }

            if (license.Status == "REVOKED")
            {
                return BadRequest(new { Message = "License key has been revoked." });
            }

            if (license.ExpireDate < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "License key has expired." });
            }

            // Check if machine fingerprint is already registered
            var existingMachine = license.Machines.FirstOrDefault(m => m.Fingerprint == request.MachineFingerprint);
            if (existingMachine == null)
            {
                // Verify max limits (only count ACTIVE machines)
                var activeCount = license.Machines.Count(m => m.Status == "ACTIVE");
                if (activeCount >= license.MaxMachines)
                {
                    return BadRequest(new { Message = $"LICENSE EXCEEDED: Maximum active machine limit reached (max {license.MaxMachines})." });
                }

                // Register new machine
                var newMachine = new Machine
                {
                    LicenseId = license.Id,
                    Fingerprint = request.MachineFingerprint,
                    Status = "ACTIVE",
                    ActivatedAt = DateTime.UtcNow,
                    LastHeartbeat = DateTime.UtcNow
                };
                license.Machines.Add(newMachine);

                if (license.Status == "NOT_ACTIVATED")
                {
                    license.Status = "ACTIVE";
                }

                await _context.SaveChangesAsync();
            }
            else
            {
                if (existingMachine.Status == "REVOKED")
                {
                    return BadRequest(new { Message = "DEVICE_REVOKED: This device has been revoked and cannot use this license." });
                }

                existingMachine.LastHeartbeat = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Generate signed license response
            var fingerprints = license.Machines
                .Where(m => m.Status == "ACTIVE")
                .Select(m => m.Fingerprint)
                .OrderBy(f => f)
                .ToList();
            var payload = $"{license.Id}|{license.LicenseKey}|{license.ExpireDate:yyyy-MM-ddTHH:mm:ssZ}|{license.MaxMachines}|{string.Join(",", fingerprints)}";
            var signature = _rsaService.SignData(payload);

            return Ok(new
            {
                LicenseId = license.Id,
                LicenseKey = license.LicenseKey,
                ExpireAt = license.ExpireDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                MaxMachines = license.MaxMachines,
                RegisteredFingerprints = fingerprints,
                Signature = signature
            });
        }

        // POST /api/license/validate
        [HttpPost("validate")]
        public async Task<IActionResult> Validate([FromBody] ValidateLicenseRequest request)
        {
            var license = await _context.Licenses
                .Include(l => l.Machines)
                .FirstOrDefaultAsync(l => l.LicenseKey == request.LicenseKey);

            if (license == null)
            {
                return Ok(new { IsValid = false, Message = "License key not found." });
            }

            if (license.Status == "REVOKED")
            {
                return Ok(new { IsValid = false, Message = "License key has been revoked." });
            }

            if (license.ExpireDate < DateTime.UtcNow)
            {
                return Ok(new { IsValid = false, Message = "License key has expired." });
            }

            var machine = license.Machines.FirstOrDefault(m => m.Fingerprint == request.MachineFingerprint);
            if (machine == null)
            {
                return Ok(new { IsValid = false, Message = "Machine is not registered for this license." });
            }

            if (machine.Status == "REVOKED")
            {
                return Ok(new { IsValid = false, Message = "This machine registration has been revoked." });
            }

            return Ok(new { IsValid = true, Message = "License is valid." });
        }

        // POST /api/license/heartbeat
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LicenseKey) || string.IsNullOrWhiteSpace(request.MachineFingerprint))
            {
                return BadRequest(new { Valid = false, Reason = "BAD_REQUEST", Message = "License key and machine fingerprint are required." });
            }

            var license = await _context.Licenses
                .Include(l => l.Machines)
                .FirstOrDefaultAsync(l => l.LicenseKey == request.LicenseKey);

            if (license == null)
            {
                return Ok(new { Valid = false, Reason = "LICENSE_NOT_FOUND" });
            }

            if (license.Status == "REVOKED")
            {
                return Ok(new { Valid = false, Reason = "LICENSE_REVOKED" });
            }

            if (license.ExpireDate < DateTime.UtcNow)
            {
                return Ok(new { Valid = false, Reason = "EXPIRED" });
            }

            var machine = license.Machines.FirstOrDefault(m => m.Fingerprint == request.MachineFingerprint);
            if (machine == null)
            {
                return Ok(new { Valid = false, Reason = "MACHINE_NOT_FOUND" });
            }

            if (machine.Status == "REVOKED")
            {
                return Ok(new { Valid = false, Reason = "MACHINE_REVOKED" });
            }

            machine.LastHeartbeat = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Valid = true });
        }

        // POST /api/license/revoke
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] RevokeLicenseRequest request)
        {
            var license = await _context.Licenses
                .Include(l => l.Machines)
                .FirstOrDefaultAsync(l => l.LicenseKey == request.LicenseKey);

            if (license == null)
            {
                return NotFound(new { Message = "License key not found." });
            }

            if (string.IsNullOrEmpty(request.MachineFingerprint))
            {
                // Revoke whole license
                license.Status = "REVOKED";
                license.Machines.Clear();
                await _context.SaveChangesAsync();
                return Ok(new { Message = "License revoked successfully." });
            }
            else
            {
                // Revoke specific machine (change its status to REVOKED instead of deleting it)
                var machine = license.Machines.FirstOrDefault(m => m.Fingerprint == request.MachineFingerprint);
                if (machine == null)
                {
                    return NotFound(new { Message = "Machine fingerprint not found on this license." });
                }

                machine.Status = "REVOKED";
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Machine registration revoked successfully." });
            }
        }

        // POST /api/license/reset-machine
        [HttpPost("reset-machine")]
        public async Task<IActionResult> ResetMachine([FromBody] ResetMachineRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LicenseKey) || string.IsNullOrWhiteSpace(request.MachineFingerprint))
            {
                return BadRequest(new { Message = "License key and machine fingerprint are required." });
            }

            var license = await _context.Licenses
                .Include(l => l.Machines)
                .FirstOrDefaultAsync(l => l.LicenseKey == request.LicenseKey);

            if (license == null)
            {
                return NotFound(new { Message = "License key not found." });
            }

            var machine = license.Machines.FirstOrDefault(m => m.Fingerprint == request.MachineFingerprint);
            if (machine == null)
            {
                return NotFound(new { Message = "Machine fingerprint not found on this license." });
            }

            license.Machines.Remove(machine);
            if (license.Machines.Count == 0 && license.Status == "ACTIVE")
            {
                license.Status = "NOT_ACTIVATED";
            }
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Machine registration reset successfully." });
        }

        // GET /api/license/list
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var list = await _context.Licenses
                .Include(l => l.Machines)
                .Select(l => new
                {
                    l.Id,
                    l.LicenseKey,
                    l.MaxMachines,
                    l.Status,
                    ExpireAt = l.ExpireDate,
                    ActiveMachines = l.Machines.Select(m => new
                    {
                        MachineFingerprint = m.Fingerprint,
                        m.Status,
                        CreatedAt = m.ActivatedAt,
                        LastSeenAt = m.LastHeartbeat
                    }).ToList()
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET /api/license/public-key
        [HttpGet("public-key")]
        public IActionResult GetPublicKey()
        {
            return Ok(new { PublicKeyXml = _rsaService.GetPublicKeyXml() });
        }

        private string GenerateLicenseKey()
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string Part() => new string(Enumerable.Repeat(chars, 4).Select(s => s[random.Next(s.Length)]).ToArray());
            return $"{Part()}-{Part()}-{Part()}-{Part()}";
        }
    }

    public class CreateLicenseRequest
    {
        public int ExpireDays { get; set; } = 365;
        public int MaxMachines { get; set; } = 4;
    }

    public class ActivateLicenseRequest
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string MachineFingerprint { get; set; } = string.Empty;
    }

    public class ValidateLicenseRequest
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string MachineFingerprint { get; set; } = string.Empty;
    }

    public class RevokeLicenseRequest
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string MachineFingerprint { get; set; } = string.Empty;
    }

    public class HeartbeatRequest
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string MachineFingerprint { get; set; } = string.Empty;
    }

    public class ResetMachineRequest
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string MachineFingerprint { get; set; } = string.Empty;
    }
}
