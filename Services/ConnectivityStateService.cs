using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace QuanLyGiuXe.Services
{
    public enum ConnectionStateEnum
    {
        ONLINE,
        OFFLINE,
        RECONNECTING
    }

    /// <summary>
    /// Chuyên trách giám sát kết nối SQL Server realtime.
    /// Heartbeat: 5 giây.
    /// Thread-safe & Async.
    /// </summary>
    public sealed class ConnectivityStateService : INotifyPropertyChanged
    {
        // ── Singleton ─────────────────────────────────────────────────────────────
        private static readonly Lazy<ConnectivityStateService> _lazy =
            new Lazy<ConnectivityStateService>(() => new ConnectivityStateService());
        public static ConnectivityStateService Instance => _lazy.Value;

        // ── Constants ─────────────────────────────────────────────────────────────
        private const int HeartbeatIntervalSeconds = 5;
        private const int ConnectionTimeoutSeconds = 3;

        // ── State ─────────────────────────────────────────────────────────────────
        private ConnectionStateEnum _currentState = ConnectionStateEnum.OFFLINE;
        private bool _isInitialized = false;
        private bool _isSimulatingOffline = false;
        private bool _forceTimeout = false;
        private bool _forceHeartbeatFail = false;
        
        private long _heartbeatLatencyMs = 0;
        private int _retryCount = 0;
        private int _reconnectAttempts = 0;
        private DateTime? _lastSuccessfulPing;
        private int _currentRetryDelaySeconds = HeartbeatIntervalSeconds;

        private CancellationTokenSource _cts;
        private readonly object _lock = new();

        public event PropertyChangedEventHandler PropertyChanged;

        private ConnectivityStateService() { }

        // ── Properties ────────────────────────────────────────────────────────────
        
        public bool IsSimulatingOffline
        {
            get => _isSimulatingOffline;
            set { _isSimulatingOffline = value; OnPropertyChanged(); _ = CheckConnectionAsync(); }
        }

        public bool ForceTimeout
        {
            get => _forceTimeout;
            set { _forceTimeout = value; OnPropertyChanged(); }
        }

        public bool ForceHeartbeatFail
        {
            get => _forceHeartbeatFail;
            set { _forceHeartbeatFail = value; OnPropertyChanged(); }
        }

        public long HeartbeatLatencyMs
        {
            get => _heartbeatLatencyMs;
            private set { _heartbeatLatencyMs = value; OnPropertyChanged(); }
        }

        public int RetryCount
        {
            get => _retryCount;
            private set { _retryCount = value; OnPropertyChanged(); }
        }

        public int ReconnectAttempts
        {
            get => _reconnectAttempts;
            private set { _reconnectAttempts = value; OnPropertyChanged(); }
        }

        public DateTime? LastSuccessfulPing
        {
            get => _lastSuccessfulPing;
            private set { _lastSuccessfulPing = value; OnPropertyChanged(); }
        }

        public int CurrentRetryDelaySeconds
        {
            get => _currentRetryDelaySeconds;
            private set { _currentRetryDelaySeconds = value; OnPropertyChanged(); }
        }

        public string MonitorThreadState => _isInitialized ? "RUNNING" : "STOPPED";

        public ConnectionStateEnum CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    var oldState = _currentState;
                    _currentState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOnline));
                    OnPropertyChanged(nameof(IsOffline));
                    OnPropertyChanged(nameof(IsReconnecting));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusText));

                    NotifyStateChange(oldState, value);
                }
            }
        }

        public bool IsOnline => CurrentState == ConnectionStateEnum.ONLINE;
        public bool IsOffline => CurrentState == ConnectionStateEnum.OFFLINE;
        public bool IsReconnecting => CurrentState == ConnectionStateEnum.RECONNECTING;

        public string StatusText => CurrentState switch
        {
            ConnectionStateEnum.ONLINE => "SQL ONLINE",
            ConnectionStateEnum.OFFLINE => "SQL OFFLINE",
            ConnectionStateEnum.RECONNECTING => "ĐANG KẾT NỐI...",
            _ => "UNKNOWN"
        };

        public System.Windows.Media.Brush StatusColor => CurrentState switch
        {
            ConnectionStateEnum.ONLINE => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#27AE60"),
            ConnectionStateEnum.OFFLINE => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#E74C3C"),
            ConnectionStateEnum.RECONNECTING => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F39C12"),
            _ => (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#95A5A6")
        };

        // ── Public API ────────────────────────────────────────────────────────────
        
        public void Start()
        {
            lock (_lock)
            {
                if (_isInitialized) return;
                _isInitialized = true;
                OnPropertyChanged(nameof(MonitorThreadState));

                _cts = new CancellationTokenSource();
                Task.Run(() => HeartbeatLoopAsync(_cts.Token));
                
                LoggingService.Instance.LogInfo("CONNECTIVITY", "Service", "ConnectivityStateService started");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _cts?.Cancel();
                _isInitialized = false;
                OnPropertyChanged(nameof(MonitorThreadState));
            }
        }

        public async Task<bool> CheckConnectionAsync(CancellationToken token = default)
        {
            if (IsSimulatingOffline || ForceHeartbeatFail) return false;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (ForceTimeout)
                {
                    await Task.Delay(5000, token); // Force 5s delay
                    return false;
                }

                var config = ConnectionManager.Instance.CurrentConfig;
                string connStr = config.BuildConnectionString(timeout: ConnectionTimeoutSeconds);

                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(token).ConfigureAwait(false);
                
                sw.Stop();
                HeartbeatLatencyMs = sw.ElapsedMilliseconds;
                LastSuccessfulPing = DateTime.Now;
                return true;
            }
            catch
            {
                sw.Stop();
                HeartbeatLatencyMs = sw.ElapsedMilliseconds;
                return false;
            }
        }

        // ── Internal Logic ────────────────────────────────────────────────────────

        private async Task HeartbeatLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool success = await CheckConnectionAsync(token).ConfigureAwait(false);

                if (success)
                {
                    CurrentState = ConnectionStateEnum.ONLINE;
                    RetryCount = 0;
                    CurrentRetryDelaySeconds = HeartbeatIntervalSeconds;
                }
                else
                {
                    RetryCount++;
                    if (CurrentState == ConnectionStateEnum.ONLINE)
                    {
                        CurrentState = ConnectionStateEnum.OFFLINE;
                        ReconnectAttempts = 0;
                    }
                    
                    CurrentState = ConnectionStateEnum.RECONNECTING;
                    ReconnectAttempts++;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void NotifyStateChange(ConnectionStateEnum oldState, ConnectionStateEnum newState)
        {
            if (oldState != ConnectionStateEnum.ONLINE && newState == ConnectionStateEnum.ONLINE)
            {
                ToastNotificationService.Instance.ShowToast("Kết nối SQL Server thành công!", ToastType.Success);
                LoggingService.Instance.LogInfo("CONNECTIVITY", "SQL", "SQL Server connection restored.");
            }
            else if (oldState == ConnectionStateEnum.ONLINE && newState != ConnectionStateEnum.ONLINE)
            {
                ToastNotificationService.Instance.ShowToast("Mất kết nối SQL Server! Đang thử kết nối lại...", ToastType.Error);
                LoggingService.Instance.LogError("CONNECTIVITY", "SQL", "SQL Server connection lost.", null);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
