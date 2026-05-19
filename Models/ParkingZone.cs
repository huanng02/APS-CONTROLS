using System;

namespace QuanLyGiuXe.Models
{
    public class ParkingZone
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public string ZoneCode { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaxCapacity { get; set; } = 100;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helpers
        public string SiteCode { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
    }
}
