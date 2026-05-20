using System;

namespace QuanLyGiuXe.Models
{
    public class DisplayLogEntry
    {
        public int STT { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string EventType { get; set; }
        public string Source { get; set; }
        public string Details { get; set; }
        public string UserId { get; set; }
        public string Plate { get; set; }
        public string Exception { get; set; }
        // new audit fields
        public string Username { get; set; }
        public string Action { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public string SessionId { get; set; }
        public string CorrelationId { get; set; }
    }
}
