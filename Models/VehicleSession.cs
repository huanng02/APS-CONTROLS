using System;

namespace QuanLyGiuXe.Models
{
    public class VehicleSession
    {
        public int Id { get; set; }
        public int? CardId { get; set; }
        public string BienSo { get; set; } = string.Empty;
        public DateTime ThoiGianVao { get; set; }
        public DateTime? ThoiGianRa { get; set; }
        public double? Tien { get; set; }
        public string TrangThai { get; set; } = "Active"; // Active, Closed
        public string AnhVao { get; set; } = string.Empty;
        public string AnhRa { get; set; } = string.Empty;

        // Multi-zone fields
        public int? SiteId { get; set; }
        public int? ZoneId { get; set; }
        public int? EntryLaneId { get; set; }
        public int? ExitLaneId { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Helper properties for display
        public string SiteName { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string EntryLaneName { get; set; } = string.Empty;
        public string ExitLaneName { get; set; } = string.Empty;
    }
}
