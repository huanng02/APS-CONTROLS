using System;

namespace QuanLyGiuXe.Models
{
    public class CardGroupLanePermission
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int LaneId { get; set; }
        public int? ScheduleId { get; set; }
        public string TrangThai { get; set; } = "Active";
        public DateTime CreatedUtc { get; set; }
    }
}
