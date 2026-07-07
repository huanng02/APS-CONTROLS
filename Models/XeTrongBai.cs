using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class XeTrongBai
    {
        public int Id { get; set; }
        public int? CardId { get; set; }

        public string BienSo { get; set; }
        public DateTime? ThoiGianVao { get; set; }

        public string AnhXe { get; set; }

        // Multi-zone fields
        public int? SiteId { get; set; }
        public int? ZoneId { get; set; }
        public int? EntryLaneId { get; set; }

        // Helper properties for display
        public string SiteName { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string EntryLaneName { get; set; } = string.Empty;
    }
}
