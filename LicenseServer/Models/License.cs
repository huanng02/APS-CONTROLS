using System;
using System.Collections.Generic;

namespace LicenseServer.Models
{
    public class License
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public int MaxMachines { get; set; } = 1;
        public string Status { get; set; } = "NOT_ACTIVATED"; // NOT_ACTIVATED, ACTIVE, REVOKED, EXPIRED
        public DateTime ExpireDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Offline / Extended licensing fields
        public string? Product { get; set; } = "APS";
        public string? CustomerName { get; set; } = string.Empty;
        public string? LicenseType { get; set; } = "Commercial";
        public string? Features { get; set; } = string.Empty; // Comma-separated features (e.g. "CAMERA,LPR,RFID,REPORT")
        public int Version { get; set; } = 1;
        
        public ICollection<Machine> Machines { get; set; } = new List<Machine>();
    }
}
