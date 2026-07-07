using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace QuanLyGiuXe.Services
{
    public enum ToastType
    {
        Success,    // xanh lá
        Error,      // đỏ
        Warning     // vàng cam
    }

    public sealed class ToastItem
    {
        public string Message { get; init; } = "";
        public ToastType Type { get; init; }
        public int DurationMs { get; init; } = 4000;
    }

    /// <summary>
    /// Singleton quản lý hàng đợi toast notification WPF.
    /// Hiển thị toast ở góc dưới phải, không block UI, không overlap nhau.
    /// </summary>
    public sealed class ToastNotificationService
    {
        // ── Singleton ─────────────────────────────────────────────────────────────
        private static readonly Lazy<ToastNotificationService> _lazy =
            new Lazy<ToastNotificationService>(() => new ToastNotificationService());
        public static ToastNotificationService Instance => _lazy.Value;

        // ── Suppression ───────────────────────────────────────────────────────────
        /// <summary>
        /// Khi true, tất cả toast mới sẽ bị bỏ qua (dùng khi đăng xuất/đăng nhập).
        /// </summary>
        public bool IsSuppressed { get; set; } = false;

        /// <summary>
        /// Xóa tất cả toast đang chờ hoặc đang hiển thị.
        /// </summary>
        public void ClearQueue()
        {
            InAppNotificationService.Instance.ClearActiveNotifications();
        }

        private ToastNotificationService() { }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Thêm toast vào hàng đợi. Thread-safe, không block.
        /// </summary>
        public void ShowToast(string message, ToastType type, int durationMs = 4000)
        {
            // Bỏ qua toast nếu đang trong trạng thái suppressed (đăng xuất/đăng nhập)
            if (IsSuppressed) return;

            // Ánh xạ kiểu Toast sang In-App Notification
            Models.NotificationType inAppType = type switch
            {
                ToastType.Success => Models.NotificationType.Success,
                ToastType.Error   => Models.NotificationType.Error,
                ToastType.Warning => Models.NotificationType.Warning,
                _                 => Models.NotificationType.Info
            };

            InAppNotificationService.Instance.ShowNotification(message, inAppType, durationMs);
        }
    }
}
