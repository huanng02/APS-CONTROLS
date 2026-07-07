using System;

namespace QuanLyGiuXe.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public int PositionId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? CCCD { get; set; }
        public string? Avatar { get; set; }
        public int? DepartmentId { get; set; }
        public string Status { get; set; } = "Active";
        public bool IsDeleted { get; set; }

        // Helper properties for join queries
        public string? CompanyName { get; set; }
        public string? DepartmentName { get; set; }
        public string? PositionName { get; set; }

        // Helper properties for linked RFID Card
        public string? CardUID { get; set; }
        public string? CardStatus { get; set; }
        public DateTime? CardExpiration { get; set; }
    }
}
