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

            // Hiển thị thông báo cho người dùng
            ToastNotificationService.Instance.ShowToast(
                "Đã có lỗi hệ thống xảy ra. Ứng dụng sẽ cố gắng tiếp tục hoạt động.", 
                ToastType.Error, 
                6000);

            // Gợi ý: Nếu lỗi quá nghiêm trọng, có thể yêu cầu khởi động lại app ở đây
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            
            // Ghi log lỗi (đây thường là lỗi chí tử khiến app buộc phải đóng)
            ErrorLoggingService.LogError(ex, "Global.AppDomain", $"IsTerminating: {e.IsTerminating}");

            if (!e.IsTerminating)
            {
                ToastNotificationService.Instance.ShowToast(
                    "Lỗi hệ thống nghiêm trọng đã được ghi lại.", 
                    ToastType.Error);
            }
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            // Ghi log lỗi
            ErrorLoggingService.LogError(e.Exception, "Global.TaskScheduler");

            // Đánh giá là đã quan sát để tránh crash (tùy thuộc vào .NET version)
            e.SetObserved();

            ToastNotificationService.Instance.ShowToast(
                "Phát hiện lỗi trong tác vụ chạy ngầm.", 
                ToastType.Warning);
        }
    }
}
