using System;
using System.Windows.Media;

namespace QuanLyGiuXe.Models
{
    public enum RealtimeEventSeverity
    {
        Info,
        Success,
        Warning,
        Error,
        Critical
    }

    public class RealtimeEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EventType { get; set; } = "SYSTEM_INFO";
        public RealtimeEventSeverity Severity { get; set; } = RealtimeEventSeverity.Info;
        public string Site { get; set; } = string.Empty;
        public string Zone { get; set; } = string.Empty;
        public string Gate { get; set; } = string.Empty;
        public string Lane { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;

        // Custom display properties for high-end Control Room UI
        public System.Windows.Media.Brush SeverityBrush
        {
            get
            {
                return Severity switch
                {
                    RealtimeEventSeverity.Info => new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)),      // Bright HSL Blue
                    RealtimeEventSeverity.Success => new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113)),   // Emerald Green
                    RealtimeEventSeverity.Warning => new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 196, 15)),   // Sunny Yellow
                    RealtimeEventSeverity.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 126, 34)),     // Vibrant Orange
                    RealtimeEventSeverity.Critical => new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)),    // Intense Red
                    _ => new SolidColorBrush(System.Windows.Media.Colors.LightGray)
                };
            }
        }

        public System.Windows.Media.Brush SeverityBackgroundBrush
        {
            get
            {
                return Severity switch
                {
                    RealtimeEventSeverity.Info => new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 52, 152, 219)),
                    RealtimeEventSeverity.Success => new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 46, 204, 113)),
                    RealtimeEventSeverity.Warning => new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 241, 196, 15)),
                    RealtimeEventSeverity.Error => new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 230, 126, 34)),
                    RealtimeEventSeverity.Critical => new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 231, 76, 60)),
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 128, 128, 128))
                };
            }
        }

        public string EventIcon
        {
            get
            {
                return EventType.ToUpper() switch
                {
                    "RFID_SCAN" => "💳",
                    "VEHICLE_ENTRY" => "🚗",
                    "VEHICLE_EXIT" => "🚙",
                    "BARRIER_OPEN" => "🚧",
                    "BARRIER_CLOSE" => "🛑",
                    
                    "CONTROLLER_ONLINE" => "🟢",
                    "CONTROLLER_OFFLINE" => "🔴",
                    "READER_ONLINE" => "🟢",
                    "READER_OFFLINE" => "🔴",
                    "CAMERA_ONLINE" => "🟢",
                    "CAMERA_OFFLINE" => "🔴",
                    
                    "CARD_REGISTERED" => "📇",
                    "CARD_UPDATED" => "📝",
                    "CARD_DELETED" => "🗑️",
                    
                    "USER_LOGIN" => "🔑",
                    "USER_LOGOUT" => "🚪",
                    
                    "SYSTEM_WARNING" => "⚠️",
                    "SYSTEM_ERROR" => "❌",
                    "SYSTEM_CRITICAL" => "🚨",
                    
                    _ => "🔔"
                };
            }
        }

        public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");
    }
}
