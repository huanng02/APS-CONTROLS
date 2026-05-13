#if DEBUG
using System;

namespace QuanLyGiuXe.DebugTools.Models
{
    public class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Category { get; set; } = "General";

        public string StatusLabel => Success ? "PASS" : "FAIL";
        public string DisplayTimestamp => Timestamp.ToString("HH:mm:ss.fff");
        public string DisplayDuration => $"{Duration.TotalMilliseconds:F1}ms";
    }
}
#endif
