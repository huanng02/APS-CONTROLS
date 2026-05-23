using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class LichSuXe
    {
        public int Id { get; set; }

        public int CardId { get; set; }
        public string BienSo { get; set; }

        public DateTime ThoiGianVao { get; set; }
        public DateTime? ThoiGianRa { get; set; }

        public double? Tien { get; set; }
        public string TrangThai { get; set; }

        public string AnhVao { get; set; }
        public string AnhRa { get; set; }

        // Multi-zone fields
        public int? SiteId { get; set; }
        public int? ZoneId { get; set; }
        public int? EntryLaneId { get; set; }
        public int? ExitLaneId { get; set; }

        // Helper properties for display
        public string SiteName { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string EntryLaneName { get; set; } = string.Empty;
        public string ExitLaneName { get; set; } = string.Empty;
    }
}
