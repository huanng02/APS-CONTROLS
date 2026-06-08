using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class SessionHealthMonitor
    {
        private static readonly Lazy<SessionHealthMonitor> _lazy = new(() => new SessionHealthMonitor());
        public static SessionHealthMonitor Instance => _lazy.Value;

        private CancellationTokenSource? _cts;
        private bool _isStarted = false;

        private SessionHealthMonitor() { }

        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorLoopAsync(_cts.Token));
            LoggingService.Instance.LogInfo("HEALTH_MONITOR", "Service", "SessionHealthMonitor background loop started");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _isStarted = false;
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1. Detect corrupted sessions
                    var corrupted = await SessionConsistencyService.Instance.ValidateSessionsAsync();
                    if (corrupted.Count > 0)
                    {
                        LoggingService.Instance.LogWarning("HEALTH_MONITOR", "Check", $"Detected {corrupted.Count} inconsistent sessions. Initiating auto-repair...");
                        
                        // 2. Perform auto repair
                        int repaired = await SessionRepairService.Instance.RepairAllInconsistenciesAsync();
                        LoggingService.Instance.LogInfo("HEALTH_MONITOR", "Repair", $"Auto-repair finished. Fixed {repaired} items.");
                    }
                    else
                    {
                        LoggingService.Instance.LogInfo("HEALTH_MONITOR", "Check", "Garage session consistency: OK.");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("HEALTH_MONITOR", "Loop", "Error during session health check", ex);
                }

                // Check every 60 seconds
                await Task.Delay(TimeSpan.FromSeconds(60), token);
            }
        }
    }
}
