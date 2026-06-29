using System;
using System.Collections.Generic;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services
{
    public class HardwareDetails
    {
        [JsonProperty("cpu")]
        public string Cpu { get; set; } = string.Empty;

        [JsonProperty("motherboard")]
        public string Motherboard { get; set; } = string.Empty;

        [JsonProperty("disk")]
        public string Disk { get; set; } = string.Empty;

        [JsonProperty("bios")]
        public string Bios { get; set; } = string.Empty;
    }

    public class ActivationRequestPayload
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("machineId")]
        public string MachineId { get; set; } = string.Empty;

        [JsonProperty("hardware")]
        public HardwareDetails Hardware { get; set; } = new();

        [JsonProperty("requestDate")]
        public string RequestDate { get; set; } = string.Empty;
    }

    public static class HardwareFingerprintService
    {
        public static HardwareDetails GetHardwareDetails()
        {
            return new HardwareDetails
            {
                Cpu = GetCpuId(),
                Motherboard = GetMotherboardSerial(),
                Disk = GetDiskSerial(),
                Bios = GetBiosSerial()
            };
        }

        public static string CalculateMachineId(HardwareDetails details)
        {
            string raw = $"CPU:{details.Cpu}|MB:{details.Motherboard}|BIOS:{details.Bios}|DISK:{details.Disk}";
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("X2"));
                }
                string fullHash = sb.ToString();
                return fullHash.Substring(0, 12); // First 12 characters, uppercase
            }
        }

        public static string GenerateActivationRequestJson()
        {
            var details = GetHardwareDetails();
            var machineId = CalculateMachineId(details);

            var request = new ActivationRequestPayload
            {
                Version = 1,
                MachineId = machineId,
                Hardware = details,
                RequestDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            return JsonConvert.SerializeObject(request, Formatting.Indented);
        }

        private static string GetCpuId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var val = obj["ProcessorId"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return CleanHardwareString(val);
                    }
                }
            }
            catch { }
            return "CPU_UNKNOWN";
        }

        private static string GetMotherboardSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var val = obj["SerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return CleanHardwareString(val);
                    }
                }
            }
            catch { }
            return "MB_UNKNOWN";
        }

        private static string GetBiosSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var val = obj["SerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return CleanHardwareString(val);
                    }
                }
            }
            catch { }
            return "BIOS_UNKNOWN";
        }

        private static string GetDiskSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var val = obj["SerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return CleanHardwareString(val);
                    }
                }
            }
            catch { }
            return "DISK_UNKNOWN";
        }

        private static string CleanHardwareString(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            val = val.Trim();
            
            string upper = val.ToUpperInvariant();
            if (upper == "NONE" || 
                upper == "TO BE FILLED BY O.E.M." || 
                upper == "00000000000000000000" || 
                upper == "00000000" || 
                upper == "DEFAULT STRING")
            {
                return "GENERIC_SERIAL";
            }
            
            return val;
        }
    }
}
