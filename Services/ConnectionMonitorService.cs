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
        private bool? _lastDbStatus = null;
        private bool? _lastC3Status = null;
        private string? _cachedC3Ip;
        public string? CurrentC3Ip => _cachedC3Ip;


        // ── Lifecycle ─────────────────────────────────────────────────────────────
        private readonly object _lifecycleLock = new();
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
            lock (_lifecycleLock)
            {
                if (_loopTask != null && !_loopTask.IsCompleted) return;

                StartLocked(appToken);
            }

            LoggingService.Instance.LogInfo("CONNECTION", "Monitor",
                $"ConnectionMonitorService started (interval={IntervalSeconds}s)");
        }

        /// <summary>
        /// Hủy task hiện tại (nếu có) và khởi động vòng lặp mới. Dùng sau login lại để
        /// tránh race: check chạy khi chưa có subscriber làm mất sự kiện cập nhật UI.
        /// </summary>
        public void Restart(CancellationToken appToken = default)
        {
            lock (_lifecycleLock)
            {
                CancelAndJoinLoop();
                ResetState();
                StartLocked(appToken);
            }

            LoggingService.Instance.LogInfo("CONNECTION", "Monitor",
                $"ConnectionMonitorService restarted (interval={IntervalSeconds}s)");
        }

        /// <summary>
        /// Dừng vòng lặp monitor (gọi khi đóng app hoặc logout).
        /// </summary>
        public void Stop()
        {
            lock (_lifecycleLock)
            {
                CancelAndJoinLoop();
                ResetState();
            }
        }

        /// <summary>
        /// Ép thực hiện kiểm tra kết nối ngay lập tức (không đợi vòng lặp).
        /// </summary>
        public async Task ForceCheckAsync(CancellationToken token = default)
        {
            await CheckAndNotifyAsync(token).ConfigureAwait(false);
        }

        private void StartLocked(CancellationToken appToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(appToken);
            _loopTask = Task.Run(() => MonitorLoopAsync(_cts.Token));
        }

        private void CancelAndJoinLoop()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }
            try { _loopTask?.Wait(2000); } catch { /* ignore */ }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
            }
        }

        public void ResetState()
        {
            _lastDbStatus = null;
            _lastC3Status = null;
            _cachedC3Ip = null;
            LoggingService.Instance.LogInfo("CONNECTION", "Monitor", "Status state reset.");
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

            // ── Ghi log chuyên sâu khi trạng thái thay đổi ──────────────────────────────
            if (dbChanged)
            {
                _lastDbStatus = dbOk;
                LoggingService.Instance.LogReconnect("SQL_Server", dbOk, 1, 0, dbOk ? "Database connected" : "Database connection lost");
            }

            if (c3Changed)
            {
                _lastC3Status = c3Ok;
                LoggingService.Instance.LogReconnect("C3_Controller", c3Ok, 1, 0, c3Ok ? "C3-200 connected" : "C3-200 connection lost");
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
                if (_cachedC3Ip == null)
                {
                    var cfg = AppConfig.Load();
                    _cachedC3Ip = cfg.ZKTeco.IpAddress;
                }

                string ip = _cachedC3Ip;

                if (string.IsNullOrWhiteSpace(ip)) return false;

                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, PingTimeoutMs)
                                      .ConfigureAwait(false);
                if (reply.Status == IPStatus.Success)
                {
                    // cache the reachable IP (may have been updated from config)
                    _cachedC3Ip = ip;
                    return true;
                }
                return false;
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
