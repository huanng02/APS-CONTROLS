using System;

namespace QuanLyGiuXe.Models
{
    public class Department
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public bool IsDeleted { get; set; }

        // Helper property for join queries
        public string? CompanyName { get; set; }
    }
}
