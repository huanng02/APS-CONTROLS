using System;

namespace QuanLyGiuXe.Models
{
    public class LaneConfig
    {
        public int Id { get; set; }
        public string LaneCode { get; set; } = string.Empty;
        public string LaneName { get; set; } = string.Empty;
        public string Direction { get; set; } = "IN"; // IN, OUT
        public int? ZoneId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helper
        public string ZoneName { get; set; } = string.Empty;
    }
}
