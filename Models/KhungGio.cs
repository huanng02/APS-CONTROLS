using System;

namespace QuanLyGiuXe.Models
{
    public class KhungGio
    {
        public int Id { get; set; }
        public string TenKhungGio { get; set; } = string.Empty;
        public TimeSpan GioBatDau { get; set; }
        public TimeSpan GioKetThuc { get; set; }
        public bool QuaDem { get; set; }
        public bool TrangThai { get; set; }

        // Convenience display label used by UI: "TenKhungGio (HH:mm - HH:mm)" or fallback to Id if name missing
        public string DisplayLabel
        {
            get
            {
                string name = string.IsNullOrWhiteSpace(TenKhungGio) ? $"Khung#{Id}" : TenKhungGio;
                return $"{name} ({GioBatDau:hh\\:mm} - {GioKetThuc:hh\\:mm})";
            }
        }
    }
}
