using System;
using System.Collections.Generic;

namespace LicenseServer.Models
{
    public class License
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public int MaxMachines { get; set; } = 4;
        public string Status { get; set; } = "NOT_ACTIVATED"; // NOT_ACTIVATED, ACTIVE, REVOKED
        public DateTime ExpireAt { get; set; }
        
        public ICollection<Machine> Machines { get; set; } = new List<Machine>();
    }
}
