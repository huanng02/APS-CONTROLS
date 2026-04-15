using System;

namespace QuanLyGiuXe.Models
{
    public class RFIDCard
    {
        public int Id { get; set; }
        public string UID { get; set; } = string.Empty;
        public string BienSo { get; set; } = string.Empty;
        public int LoaiVeId { get; set; }
        public int LoaiXeId { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public DateTime NgayTao { get; set; }
    }
}
