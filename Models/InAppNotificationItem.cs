using System;
using System.Windows.Input;

namespace QuanLyGiuXe.Models
{
    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public class InAppNotificationItem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public int DurationMs { get; set; } = 4000;
        public ICommand DismissCommand { get; set; }
    }
}
