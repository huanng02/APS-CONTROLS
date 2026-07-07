using System;

namespace QuanLyGiuXe.Models
{
    public class RFIDAccessRule
    {
        public int Id { get; set; }
        public string CardUID { get; set; } = string.Empty;
        public int LaneId { get; set; }
        public int? ScheduleId { get; set; }
        public string RuleType { get; set; } = "Allow";
        public string TrangThai { get; set; } = "Active";
        public DateTime CreatedUtc { get; set; }
    }
}
