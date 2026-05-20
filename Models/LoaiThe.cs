using System;

namespace QuanLyGiuXe.Models
{
    public class LoaiThe
    {
        public int Id { get; set; }
        public string TenLoaiThe { get; set; } = string.Empty;
        public decimal GiaTien { get; set; }
        public string TrangThai { get; set; } = string.Empty;
    }
}
