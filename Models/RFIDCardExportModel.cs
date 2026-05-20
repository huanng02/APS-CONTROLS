using System;

namespace QuanLyGiuXe.Models
{
    public class RFIDCardExportModel
    {
        public string CardUID { get; set; } = string.Empty;
        public string BienSo { get; set; } = string.Empty;
        public string LoaiXe { get; set; } = string.Empty;
        public string LoaiVe { get; set; } = string.Empty;
        public DateTime? NgayDangKy { get; set; }
        public DateTime? NgayHetHan { get; set; }
        public string TrangThai { get; set; } = string.Empty;
    }
}
