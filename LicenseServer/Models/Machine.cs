using System;

namespace LicenseServer.Models
{
    public class Machine
    {
        public int Id { get; set; }
        public int LicenseId { get; set; }
        public string Fingerprint { get; set; } = string.Empty;
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, REVOKED
        public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    }
}
