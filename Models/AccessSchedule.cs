using System;

namespace QuanLyGiuXe.Models
{
    public class AccessSchedule
    {
        public int Id { get; set; }
        public string ScheduleName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string DaysOfWeek { get; set; } = string.Empty;
        public bool IsEmergencyOverride { get; set; }
        public string TrangThai { get; set; } = "Active";
        public DateTime CreatedUtc { get; set; }
    }
}
