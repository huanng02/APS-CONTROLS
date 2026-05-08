using System;

namespace QuanLyGiuXe.Services
{
    public class LogEntry
    {
        // original fields (kept for backward compatibility)
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string EventType { get; set; }
        public string Source { get; set; }
        public string UserId { get; set; }
        public string Plate { get; set; }
        public string Details { get; set; }
        public string Exception { get; set; }

        public string Action { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public string Username { get; set; }
        public string MachineName { get; set; }
        public string DeviceName { get; set; }
        public string SessionId { get; set; }
        public string CorrelationId { get; set; }
        public string IpAddress { get; set; }
    }
}
