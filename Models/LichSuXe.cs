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

        private string? _trangThai;
        public string TrangThai
        {
            get => string.IsNullOrEmpty(_trangThai)
                ? (ThoiGianRa.HasValue ? "Đã ra" : "Trong bãi")
                : _trangThai;
            set => _trangThai = value;
        }

        public string AnhVao { get; set; }
        public string AnhRa { get; set; }

        public int? SiteId { get; set; }
        public int? ZoneId { get; set; }
        public int? EntryLaneId { get; set; }
        public int? ExitLaneId { get; set; }

        public string? EntryWorkstationId { get; set; }
        public string? ExitWorkstationId { get; set; }
        public int? EntryControllerId { get; set; }
        public int? ExitControllerId { get; set; }

        // Helper properties for display
        public string SiteName { get; set; } = string.Empty;
        public string ZoneName { get; set; } = string.Empty;
        public string EntryLaneName { get; set; } = string.Empty;
        public string ExitLaneName { get; set; } = string.Empty;
        public string LoaiVeName { get; set; } = string.Empty;
        public string LoaiXeName { get; set; } = string.Empty;

        // Cardholder / Owner properties
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
    }
}
