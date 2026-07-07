using System;

namespace QuanLyGiuXe.Models
{
    public class BarrierConfig
    {
        public int Id { get; set; }
        public string BarrierName { get; set; } = string.Empty;
        public int? ControllerId { get; set; }
        public int RelayNumber { get; set; } = 1;
        public int? LaneId { get; set; }
        public string Direction { get; set; } = "IN";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helpers
        public string ControllerName { get; set; } = string.Empty;
        public string LaneName { get; set; } = string.Empty;
    }
}
