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

        // ── Queue ─────────────────────────────────────────────────────────────────
        private readonly ConcurrentQueue<ToastItem> _queue = new();
        private volatile bool _isShowing = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private ToastNotificationService() { }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Thêm toast vào hàng đợi. Thread-safe, không block.
        /// </summary>
        public void ShowToast(string message, ToastType type, int durationMs = 4000)
        {
            _queue.Enqueue(new ToastItem
            {
                Message    = message,
                Type       = type,
                DurationMs = durationMs
            });

            // Kích hoạt xử lý queue nếu chưa có toast nào đang hiện
            if (!_isShowing)
                _ = ProcessQueueAsync();
        }

        // ── Queue processor ───────────────────────────────────────────────────────

        private async Task ProcessQueueAsync()
        {
            // Chỉ cho phép 1 processor chạy cùng lúc
            if (!await _semaphore.WaitAsync(0).ConfigureAwait(false))
                return;

            _isShowing = true;
            try
            {
                while (_queue.TryDequeue(out var item))
                {
                    await ShowSingleToastAsync(item).ConfigureAwait(false);

                    // Khoảng cách nhỏ giữa các toast liên tiếp
                    if (!_queue.IsEmpty)
                        await Task.Delay(300).ConfigureAwait(false);
                }
            }
            finally
            {
                _isShowing = false;
                _semaphore.Release();
            }
        }

        private async Task ShowSingleToastAsync(ToastItem item)
        {
            var tcs = new TaskCompletionSource<bool>();

            // Toast phải tạo trên UI thread
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    var toast = new Views.ToastWindow(item, () => tcs.TrySetResult(true));
                    toast.Show();
                }
                catch
                {
                    tcs.TrySetResult(true);
                }
            });

            // Đợi toast đóng (có timeout để không treo mãi)
            var timeoutTask = Task.Delay(item.DurationMs + 2000);
            await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);
        }
    }
}
