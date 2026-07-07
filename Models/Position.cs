using System;

namespace QuanLyGiuXe.Models
{
    public class Position
    {
        public int Id { get; set; }
        public string PositionName { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public bool IsDeleted { get; set; }
    }
}
