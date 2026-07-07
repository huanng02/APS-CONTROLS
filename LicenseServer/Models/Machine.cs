using System;

namespace LicenseServer.Models
{
    public class Machine
    {
        public int Id { get; set; }
        public int LicenseId { get; set; }

        /// <summary>Hash fingerprint (SHA-256 của CPU+Disk+MAC)</summary>
        public string Fingerprint { get; set; } = string.Empty;

        public string Status { get; set; } = "ACTIVE"; // ACTIVE, REVOKED

        public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
        public DateTime? LastActivatedAt { get; set; }

        // ── Hardware Info (để admin nhận biết máy vật lý) ──────────────────
        /// <summary>Hostname của máy (Environment.MachineName)</summary>
        public string? MachineName { get; set; }

        /// <summary>CPU ProcessorId từ Win32_Processor</summary>
        public string? CpuId { get; set; }

        /// <summary>Volume Serial Number của ổ C:</summary>
        public string? DiskSerial { get; set; }

        /// <summary>MAC address của Ethernet interface ổn định nhất</summary>
        public string? MacAddress { get; set; }

        /// <summary>Phiên bản Windows</summary>
        public string? OsVersion { get; set; }

        /// <summary>IP của client lúc kích hoạt</summary>
        public string? ActivatedFromIp { get; set; }

        public string? MotherboardSerial { get; set; }
        public string? BiosSerial { get; set; }
    }
}
