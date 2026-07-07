using System;

namespace QuanLyGiuXe.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string Status { get; set; } = "Active";
        public bool IsDeleted { get; set; }
    }
}
