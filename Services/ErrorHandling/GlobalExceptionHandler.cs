using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace QuanLyGiuXe.Services.ErrorHandling
{
    /// <summary>
    /// Thiết lập các trình xử lý ngoại lệ toàn cục cho ứng dụng WPF.
    /// </summary>
    public static class GlobalExceptionHandler
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            // 1. Bắt lỗi trên luồng UI (Main Thread)
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 2. Bắt lỗi trên các luồng nền (Background Threads)
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            // 3. Bắt lỗi trong các tác vụ Task không được await
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            _isInitialized = true;
            LoggingService.Instance.LogInfo("GlobalException", "App", "Hệ thống xử lý lỗi toàn cục đã được kích hoạt.");
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Ngăn chặn app bị tắt ngay lập tức
            e.Handled = true;

            // Ghi log lỗi
            ErrorLoggingService.LogError(e.Exception, "Global.Dispatcher");

            // Hiển thị thông báo cho người dùng dưới dạng in-app card
            InAppNotificationService.Instance.ShowNotification(
                "Đã có lỗi hệ thống xảy ra. Ứng dụng sẽ cố gắng tiếp tục hoạt động.", 
                Models.NotificationType.Error, 
                6000);
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            
            // Ghi log lỗi (đây thường là lỗi chí tử khiến app buộc phải đóng)
            ErrorLoggingService.LogError(ex, "Global.AppDomain", $"IsTerminating: {e.IsTerminating}");

            if (!e.IsTerminating)
            {
                InAppNotificationService.Instance.ShowNotification(
                    "Lỗi hệ thống nghiêm trọng đã được ghi lại.", 
                    Models.NotificationType.Error);
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Ghi log lỗi
            ErrorLoggingService.LogError(e.Exception, "Global.TaskScheduler");

            // Đánh giá là đã quan sát để tránh crash (tùy thuộc vào .NET version)
            e.SetObserved();

            InAppNotificationService.Instance.ShowNotification(
                "Phát hiện lỗi trong tác vụ chạy ngầm.", 
                Models.NotificationType.Warning);
        }
    }
}
