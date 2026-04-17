using System;

namespace QuanLyGiuXe.Models
{
    public class ImportPreviewRow
    {
        public int RowNumber { get; set; }
        public string CardUID { get; set; }
        public string BienSo { get; set; }
        public string LoaiXe { get; set; }
        public string LoaiVe { get; set; }
        public DateTime? NgayDangKy { get; set; }
        public DateTime? NgayHetHan { get; set; }
        public string TrangThai { get; set; }

        // mapping results
        public int? MappedLoaiXeId { get; set; }
        public int? MappedLoaiVeId { get; set; }

        public string Status { get; set; } // OK, AutoFix, Error
        public string Message { get; set; }
    }
}
