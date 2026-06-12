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
                ExpireAt = expireAt
            };

            _context.Licenses.Add(license);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                LicenseId = license.Id,
                LicenseKey = license.LicenseKey,
                MaxMachines = license.MaxMachines,
                Status = license.Status,
                ExpireAt = license.ExpireAt
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

            if (license.ExpireAt < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "License key has expired." });
            }

            // Check if machine fingerprint is already registered
            var existingMachine = license.Machines.FirstOrDefault(m => m.MachineFingerprint == request.MachineFingerprint);
            if (existingMachine == null)
            {
                // Verify max limits
                if (license.Machines.Count >= license.MaxMachines)
                {
                    return BadRequest(new { Message = "LICENSE EXCEEDED: Maximum machine limit reached (max 4)." });
                }

                // Register new machine
                var newMachine = new Machine
                {
                    LicenseId = license.Id,
                    MachineFingerprint = request.MachineFingerprint,
                    CreatedAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
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
                existingMachine.LastSeenAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Generate signed license response
            var fingerprints = license.Machines.Select(m => m.MachineFingerprint).OrderBy(f => f).ToList();
            var payload = $"{license.Id}|{license.LicenseKey}|{license.ExpireAt:yyyy-MM-ddTHH:mm:ssZ}|{license.MaxMachines}|{string.Join(",", fingerprints)}";
            var signature = _rsaService.SignData(payload);

            return Ok(new
            {
                LicenseId = license.Id,
                LicenseKey = license.LicenseKey,
                ExpireAt = license.ExpireAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
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

            if (license.ExpireAt < DateTime.UtcNow)
            {
                return Ok(new { IsValid = false, Message = "License key has expired." });
            }

            var isRegistered = license.Machines.Any(m => m.MachineFingerprint == request.MachineFingerprint);
            if (!isRegistered)
            {
                return Ok(new { IsValid = false, Message = "Machine is not registered for this license." });
            }

            return Ok(new { IsValid = true, Message = "License is valid." });
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
                // Revoke specific machine
                var machine = license.Machines.FirstOrDefault(m => m.MachineFingerprint == request.MachineFingerprint);
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
                return Ok(new { Message = "Machine registration revoked successfully." });
            }
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
                    l.ExpireAt,
                    ActiveMachines = l.Machines.Select(m => new { m.MachineFingerprint, m.CreatedAt, m.LastSeenAt }).ToList()
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
}
