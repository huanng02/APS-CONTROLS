using System;

namespace QuanLyGiuXe.Models
{
    public class AppLog
    {
        public int Id { get; set; }
        public DateTime? TimestampUtc { get; set; }

        public string Level { get; set; }
        public string EventType { get; set; }
        public string Source { get; set; }

        public string UserId { get; set; }
        public string Plate { get; set; }

        public string Details { get; set; }
        public string Exception { get; set; }
    }
}