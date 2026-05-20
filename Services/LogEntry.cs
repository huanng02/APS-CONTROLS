using System;

namespace QuanLyGiuXe.Services
{
    public enum LogSeverity
    {
        Info,
        Success,
        Warning,
        Error,
        Critical
    }

    public enum LogEventType
    {
        System,
        Security,
        Vehicle,
        Barrier,
        Reconnect,
        Backup,
        Restore,
        QaTest,
        Exception,
        Network,
        Camera,
        C3Controller,
        Health
    }

    public class LogEntry
    {
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

        // New tracing & monitoring fields
        public long? DurationMs { get; set; }
        public int? RetryCount { get; set; }
        public long? FileSize { get; set; }
        public string? TestName { get; set; }
        public bool? IsRecovered { get; set; }
        public string? AdditionalData { get; set; } // JSON format
    }
}
