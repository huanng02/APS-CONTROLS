using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.ErrorHandling;

namespace QuanLyGiuXe.Services.Backup
{
    public class BackupScheduler
    {
        private static readonly Lazy<BackupScheduler> _instance = 
            new Lazy<BackupScheduler>(() => new BackupScheduler());
            
        public static BackupScheduler Instance => _instance.Value;

        private CancellationTokenSource _cts;
        private bool _isRunning;

        private BackupScheduler() { }

        public void Start()
        {
            var config = AppConfig.Load().Backup;
            if (!config.AutoBackupEnabled) return;

            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();

            Task.Run(() => ScheduleLoopAsync(_cts.Token));
            LoggingService.Instance.LogInfo("BACKUP_SCHEDULER", "Start", "Đã khởi động Backup Scheduler");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _isRunning = false;
            LoggingService.Instance.LogInfo("BACKUP_SCHEDULER", "Stop", "Đã dừng Backup Scheduler");
        }

        private async Task ScheduleLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var config = AppConfig.Load().Backup;
                    if (!config.AutoBackupEnabled)
                    {
                        // Đã bị tắt trong lúc chạy, chờ một chút rồi check lại
                        await Task.Delay(TimeSpan.FromMinutes(5), token);
                        continue;
                    }

                    // Parse thời gian backup (vd: "02:00")
                    if (!TimeSpan.TryParse(config.BackupTime, out TimeSpan backupTimeOfDay))
                    {
                        backupTimeOfDay = new TimeSpan(2, 0, 0); // Default 2 AM
                    }

                    DateTime now = DateTime.Now;
                    DateTime nextRunTime = now.Date + backupTimeOfDay;

                    if (now > nextRunTime)
                    {
                        nextRunTime = nextRunTime.AddDays(1);
                    }

                    TimeSpan delay = nextRunTime - now;
                    LoggingService.Instance.LogInfo("BACKUP_SCHEDULER", "Wait", $"Lần sao lưu tiếp theo sẽ chạy lúc {nextRunTime} (sau {delay.TotalHours:F2} giờ)");

                    // Chờ đến đúng giờ
                    await Task.Delay(delay, token);

                    if (token.IsCancellationRequested) break;

                    // Thực thi backup
                    LoggingService.Instance.LogInfo("BACKUP_SCHEDULER", "Execute", "Bắt đầu chạy Auto Backup...");
                    await DatabaseBackupService.Instance.BackupNowAsync("Scheduled");
                    
                    // Chờ thêm 1 phút để tránh việc loop lại ngay trong cùng phút đó
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorLoggingService.LogError(ex, "BackupScheduler.Loop");
                    await Task.Delay(TimeSpan.FromMinutes(5), token); // Lỗi thì đợi 5p thử lại
                }
            }
        }
    }
}
