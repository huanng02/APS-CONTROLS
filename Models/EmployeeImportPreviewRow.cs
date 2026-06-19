using System;

namespace QuanLyGiuXe.Models
{
    public class EmployeeImportPreviewRow
    {
        public int RowNumber { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string PositionName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CCCD { get; set; } = string.Empty;

        // RFID Card details (Optional)
        public string CardUID { get; set; } = string.Empty;
        public string BienSo { get; set; } = string.Empty;
        public string LoaiXe { get; set; } = string.Empty;
        public string LoaiVe { get; set; } = string.Empty;
        public DateTime? NgayHetHan { get; set; }

        // Verification status
        public string Status { get; set; } = string.Empty; // OK, Error, AutoFix, Exists
        public string Message { get; set; } = string.Empty;
    }
}
