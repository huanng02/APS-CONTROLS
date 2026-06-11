using System;

namespace QuanLyGiuXe.Models
{
    public class CameraEntity
    {
        public int Id { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string CameraKey { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int? Port { get; set; } = 554;
        public string Protocol { get; set; } = "RTSP"; // RTSP / ONVIF
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;
        public int? LaneId { get; set; }
        public string Direction { get; set; } = "Overview"; // Entry / Exit / Overview
        public bool IsActive { get; set; } = true;
        public int? ResolutionWidth { get; set; }
        public int? ResolutionHeight { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helper
        public string LaneName { get; set; } = string.Empty;
    }
}
