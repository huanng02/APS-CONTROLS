using System;

namespace QuanLyGiuXe.Models
{
    public class CardGroup
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TrangThai { get; set; } = "Active";
        public DateTime CreatedUtc { get; set; }
    }
}
