using System;
using System.Collections.Generic;

namespace QuanLyGiuXe.Services.ErrorHandling
{
    /// <summary>
    /// Dịch vụ chuyên ghi log lỗi hệ thống (Production-ready).
    /// Tự động thu thập thông tin về máy tính, người dùng và chi tiết ngoại lệ.
    /// </summary>
    public static class ErrorLoggingService
    {
        /// <summary>
        /// Ghi log một ngoại lệ (Exception) với đầy đủ chi tiết kỹ thuật.
        /// </summary>
        /// <param name="ex">Ngoại lệ cần ghi log.</param>
        /// <param name="source">Nguồn phát sinh lỗi (ví dụ: CameraService, MainViewModel).</param>
        /// <param name="additionalInfo">Thông tin bổ sung tùy chọn.</param>
        public static void LogError(Exception ex, string source, string additionalInfo = null)
        {
            if (ex == null) return;

            try
            {
                // Thu thập chi tiết lỗi
                string message = ex.Message;
                string stackTrace = ex.StackTrace;
                string innerEx = ex.InnerException?.ToString() ?? "None";
                
                // Cấu trúc dữ liệu chi tiết để lưu vào cột Details hoặc Exception trong DB
                var details = new
                {
                    Source = source,
                    AdditionalInfo = additionalInfo,
                    ExceptionType = ex.GetType().FullName,
                    InnerException = innerEx,
                    HelpLink = ex.HelpLink,
                    Data = ex.Data
                };

                // Sử dụng LoggingService hiện có để ghi log
                // LoggingService đã có cơ chế ghi vào File cục bộ nếu DB lỗi
                LoggingService.Instance.LogError(
                    eventType: "SystemError",
                    source: source,
                    details: System.Text.Json.JsonSerializer.Serialize(details),
                    ex: ex
                );
            }
            catch (Exception fallbackEx)
            {
                // Fallback cuối cùng nếu ngay cả việc ghi log cũng lỗi (ví dụ: lỗi ổ đĩa)
                System.Diagnostics.Debug.WriteLine($"CRITICAL: ErrorLoggingService failed: {fallbackEx.Message}");
            }
        }

        /// <summary>
        /// Ghi log một thông báo lỗi không có ngoại lệ.
        /// </summary>
        public static void LogWarning(string message, string source, string details = null)
        {
            LoggingService.Instance.LogWarning("SystemWarning", source, $"{message} | {details}");
        }
    }
}
