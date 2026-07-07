using System;

namespace QuanLyGiuXe.Models
{
    public class BangGiaKhungGio
    {
        public int Id { get; set; }
        public int BangGiaId { get; set; }
        public int KhungGioId { get; set; }

        // Optional navigation props for in-memory joins
        public KhungGio KhungGio { get; set; }
        // Convenience field for UI display/editing (populated when joining with KhungGio)
        private decimal _giaTien;
        public string TenKhungGio { get; set; } = string.Empty;
        public decimal GiaTien
        {
            get => _giaTien;
            set
            {
                if (value < 0) _giaTien = 0m;
                else _giaTien = value;
            }
        }
        // Convenience time fields populated from KhungGio for UI display (HH:mm)
        public TimeSpan GioBatDau { get; set; } = TimeSpan.Zero;
        public TimeSpan GioKetThuc { get; set; } = TimeSpan.Zero;
        // QuaDem inherited from KhungGio so UI can show day/night
        public bool QuaDem { get; set; } = false;

        // Convenience display for UI: "HH:mm → HH:mm"
        public string DisplayTime => $"{GioBatDau:hh\\:mm} → {GioKetThuc:hh\\:mm}";

        // Convenience: LoaiKhung text
        public string LoaiKhung => QuaDem ? "Ban đêm" : "Ban ngày";
    }
}
