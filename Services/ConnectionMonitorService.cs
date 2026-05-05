using System;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Snapshot trạng thái kết nối tại một thời điểm.
    /// </summary>
    public sealed class ConnectionStatus
    {
        public bool IsDatabaseConnected { get; init; }
        public bool IsC3Connected       { get; init; }
        public bool DatabaseChanged     { get; init; }   // true nếu state vừa thay đổi
        public bool C3Changed           { get; init; }
    }

    /// <summary>
    /// Background service kiểm tra kết nối Database + C3-200 mỗi 12 giây.
    /// Chỉ raise <see cref="StatusChanged"/> khi trạng thái THỰC SỰ thay đổi (anti-spam).
    /// </summary>
    public sealed class ConnectionMonitorService
    {
        // ── Singleton ─────────────────────────────────────────────────────────────
        private static readonly Lazy<ConnectionMonitorService> _lazy =
            new Lazy<ConnectionMonitorService>(() => new ConnectionMonitorService());
        public static ConnectionMonitorService Instance => _lazy.Value;

        // ── Config ────────────────────────────────────────────────────────────────
        private const int IntervalSeconds  = 12;
        private const int DbTimeoutSeconds = 5;
        private const int PingTimeoutMs    = 3000;

        // ── State (anti-spam) ─────────────────────────────────────────────────────
        // null = chưa check lần nào → lần đầu luôn fire event
        private bool? _lastDbStatus = null;
        private bool? _lastC3Status = null;

        // ── Lifecycle ─────────────────────────────────────────────────────────────
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        // ── Events ────────────────────────────────────────────────────────────────
        /// <summary>
        /// Được raise trên thread-pool khi ít nhất một trạng thái thay đổi.
        /// Subscriber phải marshal về UI thread nếu cần.
        /// </summary>
        public event Action<ConnectionStatus>? StatusChanged;

        private ConnectionMonitorService() { }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Khởi động vòng lặp monitor. Gọi một lần từ App startup.
        /// </summary>
        public void Start(CancellationToken appToken = default)
        {
            if (_loopTask != null && !_loopTask.IsCompleted) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(appToken);
            _loopTask = Task.Run(() => MonitorLoopAsync(_cts.Token));

            LoggingService.Instance.LogInfo("CONNECTION", "Monitor",
                $"ConnectionMonitorService started (interval={IntervalSeconds}s)");
        }

        /// <summary>
        /// Dừng vòng lặp monitor (gọi khi đóng app).
        /// </summary>
        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _loopTask?.Wait(2000);
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
            }
        }

        // ── Loop ──────────────────────────────────────────────────────────────────

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            // Lần check đầu tiên chạy ngay lập tức (không đợi 12s)
            await CheckAndNotifyAsync(token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), token)
                              .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (token.IsCancellationRequested) break;
                await CheckAndNotifyAsync(token).ConfigureAwait(false);
            }

            LoggingService.Instance.LogInfo("CONNECTION", "Monitor",
                "ConnectionMonitorService loop stopped");
        }

        private async Task CheckAndNotifyAsync(CancellationToken token)
        {
            // Chạy 2 check song song
            var dbTask = CheckDatabaseAsync(token);
            var c3Task = CheckC3Async(token);

            bool dbOk, c3Ok;
            try
            {
                await Task.WhenAll(dbTask, c3Task).ConfigureAwait(false);
                dbOk = dbTask.Result;
                c3Ok = c3Task.Result;
            }
            catch
            {
                dbOk = false;
                c3Ok = false;
            }

            bool dbChanged = _lastDbStatus != dbOk;
            bool c3Changed = _lastC3Status != c3Ok;

            // ── Ghi log chỉ khi trạng thái thay đổi ──────────────────────────────
            if (dbChanged)
            {
                _lastDbStatus = dbOk;
                if (dbOk)
                    LoggingService.Instance.LogInfo("CONNECTION", "DATABASE",
                        "Database connected successfully");
                else
                    LoggingService.Instance.LogError("CONNECTION", "DATABASE",
                        "Database connection lost");
            }

            if (c3Changed)
            {
                _lastC3Status = c3Ok;
                if (c3Ok)
                    LoggingService.Instance.LogInfo("CONNECTION", "C3-200",
                        "C3-200 connected successfully");
                else
                    LoggingService.Instance.LogError("CONNECTION", "C3-200",
                        "C3-200 connection lost");
            }

            // ── Raise event nếu có thay đổi ──────────────────────────────────────
            if (dbChanged || c3Changed)
            {
                var status = new ConnectionStatus
                {
                    IsDatabaseConnected = dbOk,
                    IsC3Connected       = c3Ok,
                    DatabaseChanged     = dbChanged,
                    C3Changed           = c3Changed,
                };

                try { StatusChanged?.Invoke(status); }
                catch { /* subscriber lỗi không được crash monitor */ }
            }
        }

        // ── Check functions ───────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra kết nối SQL Server với timeout ngắn.
        /// Dùng connection string hiện tại từ <see cref="ConnectionManager"/>.
        /// </summary>
        public async Task<bool> CheckDatabaseAsync(CancellationToken token = default)
        {
            try
            {
                // Xây connection string với timeout ngắn chỉ để health-check
                var config = ConnectionManager.Instance.CurrentConfig;
                string connStr = config.BuildConnectionString(timeout: DbTimeoutSeconds);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(DbTimeoutSeconds + 1));

                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(cts.Token).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra kết nối C3-200 bằng ICMP Ping.
        /// Nhanh hơn SDK connect/disconnect và không ảnh hưởng session đang chạy.
        /// </summary>
        public async Task<bool> CheckC3Async(CancellationToken token = default)
        {
            try
            {
                var cfg = AppConfig.Load();
                string ip = cfg.ZKTeco.IpAddress;

                if (string.IsNullOrWhiteSpace(ip)) return false;

                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, PingTimeoutMs)
                                      .ConfigureAwait(false);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        // ── Snapshot hiện tại (không raise event) ────────────────────────────────

        /// <summary>
        /// Trả về trạng thái cuối cùng đã biết (không chạy check mới).
        /// null nghĩa là chưa check lần nào.
        /// </summary>
        public (bool? Database, bool? C3) LastKnownStatus =>
            (_lastDbStatus, _lastC3Status);
    }
}
