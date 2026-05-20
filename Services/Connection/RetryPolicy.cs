using System;
using System.Collections.Generic;

namespace QuanLyGiuXe.Services.Connection
{
    public static class RetryPolicy
    {
        private static readonly int[] _defaultIntervals = { 2, 5, 10, 30, 60 }; // giây

        /// <summary>
        /// Lấy thời gian chờ tiếp theo dựa trên số lần thử thất bại.
        /// </summary>
        public static TimeSpan GetNextDelay(int retryCount)
        {
            if (retryCount <= 0) return TimeSpan.Zero;
            
            int index = Math.Min(retryCount - 1, _defaultIntervals.Length - 1);
            int seconds = _defaultIntervals[index];
            
            // Nếu vượt quá số lần trong mảng, dùng exponential backoff tối đa 5 phút
            if (retryCount > _defaultIntervals.Length)
            {
                seconds = Math.Min(60 * (retryCount - _defaultIntervals.Length + 1), 300);
            }

            return TimeSpan.FromSeconds(seconds);
        }
    }
}
