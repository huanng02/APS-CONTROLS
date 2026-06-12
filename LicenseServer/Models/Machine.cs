using System;

namespace LicenseServer.Models
{
    public class Machine
    {
        public int Id { get; set; }
        public int LicenseId { get; set; }
        public string MachineFingerprint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
