using System;
using System.Collections.Generic;

namespace LicenseServer.Models
{
    public class License
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public int MaxMachines { get; set; } = 4;
        public string Status { get; set; } = "NOT_ACTIVATED"; // NOT_ACTIVATED, ACTIVE, REVOKED, EXPIRED
        public DateTime ExpireDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<Machine> Machines { get; set; } = new List<Machine>();
    }
}
