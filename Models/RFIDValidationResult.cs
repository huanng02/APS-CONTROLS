using System;

namespace QuanLyGiuXe.Models
{
    public class RFIDValidationResult
    {
        public string CardUID { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
        public string BienSo { get; set; } = string.Empty;
        public string LoaiXe { get; set; } = string.Empty;
        public string LoaiVe { get; set; } = string.Empty;
        public string TrangThai { get; set; } = string.Empty;
        public EmployeeValidationInfo? Employee { get; set; }
    }

    public class EmployeeValidationInfo
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Avatar { get; set; }
        public string? Company { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
    }
}
