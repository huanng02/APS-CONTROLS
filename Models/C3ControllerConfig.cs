using System;

namespace QuanLyGiuXe.Models
{
    public class C3ControllerConfig
    {
        public int Id { get; set; }
        public string ControllerName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int ZoneId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helper
        public string ZoneName { get; set; } = string.Empty;
    }
}
