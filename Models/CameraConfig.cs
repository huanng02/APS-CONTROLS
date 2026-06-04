using System;

namespace QuanLyGiuXe.Models
{
    public class CameraConfig
    {
        public int Id { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string CameraKey { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;
        public int? LaneId { get; set; }
        public string Direction { get; set; } = "IN";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helpers
        public string LaneName { get; set; } = string.Empty;
    }
}
