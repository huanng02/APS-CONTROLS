using System;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services.ErrorHandling
{
    /// <summary>
    /// Cung cấp các phương thức thực thi code an toàn, tự động xử lý lỗi và thông báo.
    /// </summary>
    public static class SafeExecutionService
    {
        /// <summary>
        /// Thực thi một tác vụ bất đồng bộ một cách an toàn.
        /// </summary>
        /// <param name="action">Tác vụ cần thực thi.</param>
        /// <param name="source">Nguồn gọi (để ghi log).</param>
        /// <param name="friendlyMessage">Thông báo thân thiện cho người dùng nếu có lỗi.</param>
        /// <param name="onException">Callback tùy chọn khi xảy ra lỗi.</param>
        public static async Task SafeExecuteAsync(
            Func<Task> action, 
            string source, 
            string friendlyMessage = "Có lỗi xảy ra trong quá trình xử lý.",
            Action<Exception> onException = null)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                // 1. Ghi log lỗi chuyên sâu
                ErrorLoggingService.LogError(ex, source);

                // 2. Gọi callback nếu có
                onException?.Invoke(ex);

                // 3. Hiển thị thông báo Toast cho người dùng
                if (!string.IsNullOrEmpty(friendlyMessage))
                {
                    ToastNotificationService.Instance.ShowToast(friendlyMessage, ToastType.Error);
                }
            }
        }

        /// <summary>
        /// Thực thi một hàm bất đồng bộ có giá trị trả về một cách an toàn.
        /// </summary>
        public static async Task<T> SafeExecuteAsync<T>(
            Func<Task<T>> action, 
            string source, 
            T defaultValue = default,
            string friendlyMessage = "Có lỗi xảy ra trong quá trình xử lý.",
            Action<Exception> onException = null)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                ErrorLoggingService.LogError(ex, source);
                onException?.Invoke(ex);

                if (!string.IsNullOrEmpty(friendlyMessage))
                {
                    ToastNotificationService.Instance.ShowToast(friendlyMessage, ToastType.Error);
                }

                return defaultValue;
            }
        }

        /// <summary>
        /// Thực thi một tác vụ đồng bộ một cách an toàn.
        /// </summary>
        public static void SafeExecute(
            Action action, 
            string source, 
            string friendlyMessage = "Có lỗi xảy ra.",
            Action<Exception> onException = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                ErrorLoggingService.LogError(ex, source);
                onException?.Invoke(ex);

                if (!string.IsNullOrEmpty(friendlyMessage))
                {
                    ToastNotificationService.Instance.ShowToast(friendlyMessage, ToastType.Error);
                }
            }
        }

        /// <summary>
        /// Thực thi một hàm đồng bộ có giá trị trả về một cách an toàn.
        /// </summary>
        public static T SafeExecute<T>(
            Func<T> action, 
            string source, 
            T defaultValue = default,
            string friendlyMessage = "Có lỗi xảy ra.",
            Action<Exception> onException = null)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                ErrorLoggingService.LogError(ex, source);
                onException?.Invoke(ex);

                if (!string.IsNullOrEmpty(friendlyMessage))
                {
                    ToastNotificationService.Instance.ShowToast(friendlyMessage, ToastType.Error);
                }

                return defaultValue;
            }
        }
    }
}
