using System;

namespace QuanLyGiuXe.Models
{
    public class DiscoveredCamera
    {
        public string IpAddress { get; set; } = string.Empty;
        public int OnvifPort { get; set; } = 80;
        public string Manufacturer { get; set; } = "Unknown";
        public string Model { get; set; } = "Unknown";
        public string ServiceUrl { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;
        public int RtspPort { get; set; } = 554;

        public string DisplayName => $"{Manufacturer} {Model} ({IpAddress})";
    }
}
