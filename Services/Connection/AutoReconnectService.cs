using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Services.ErrorHandling;

namespace QuanLyGiuXe.Services.Connection
{
    public class AutoReconnectService
    {
        private static readonly Lazy<AutoReconnectService> _instance = 
            new Lazy<AutoReconnectService>(() => new AutoReconnectService());
        
        public static AutoReconnectService Instance => _instance.Value;

        private readonly List<IConnectionResource> _resources = new();
        private readonly Dictionary<string, int> _retryCounts = new();
        private CancellationTokenSource _cts;
        private bool _isRunning;

        private AutoReconnectService() { }

        public void RegisterResource(IConnectionResource resource)
        {
            if (!_resources.Any(r => r.ResourceId == resource.ResourceId))
            {
                _resources.Add(resource);
                _retryCounts[resource.ResourceId] = 0;
            }
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            // Lắng nghe sự kiện đổi Database IP từ giao diện
            ConnectionManager.Instance.ConnectionChanged -= OnConnectionChanged;
            ConnectionManager.Instance.ConnectionChanged += OnConnectionChanged;

            Task.Run(() => MonitorLoopAsync(_cts.Token));
            LoggingService.Instance.LogInfo("CONNECTION", "AutoReconnect", "Service started");
        }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            LoggingService.Instance.LogInfo("CONNECTION", "AutoReconnect", "Connection string changed, forcing immediate check.");
            ForceCheckAsync();
        }

        public void ForceCheckAsync()
        {
            if (!_isRunning || _cts == null) return;
            Task.Run(async () =>
            {
                try
                {
                    foreach (var resource in _resources)
                    {
                        await CheckAndReconnectResource(resource, _cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLoggingService.LogError(ex, "AutoReconnectService.ForceCheck");
                }
            });
        }

        public void Stop()
        {
            ConnectionManager.Instance.ConnectionChanged -= OnConnectionChanged;
            _cts?.Cancel();
            _isRunning = false;
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var resource in _resources)
                    {
                        if (token.IsCancellationRequested) break;

                        await CheckAndReconnectResource(resource, token);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLoggingService.LogError(ex, "AutoReconnectService.Loop");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), token); // Heartbeat mỗi 10 giây
            }
        }

        private async Task CheckAndReconnectResource(IConnectionResource resource, CancellationToken token)
        {
            // 1. Kiểm tra sức khỏe
            bool isHealthy = await resource.CheckHealthAsync(token);

            if (isHealthy)
            {
                ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Connected);
                _retryCounts[resource.ResourceId] = 0; // Reset số lần thử nếu ok
            }
            else
            {
                // 2. Nếu không khỏe, kích hoạt Reconnect
                _retryCounts[resource.ResourceId]++;
                int retries = _retryCounts[resource.ResourceId];
                
                ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Reconnecting);
                
                TimeSpan delay = RetryPolicy.GetNextDelay(retries);
                LoggingService.Instance.LogInfo("CONNECTION", resource.ResourceId, $"Lost connection. Retry #{retries} in {delay.TotalSeconds}s");

                // Thử kết nối lại
                bool success = await resource.ReconnectAsync(token);
                
                if (success)
                {
                    ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Connected);
                    _retryCounts[resource.ResourceId] = 0;
                    ToastNotificationService.Instance.ShowToast($"{resource.ResourceId} đã được kết nối lại.", ToastType.Success);
                }
                else
                {
                    if (retries >= 5) // Sau 5 lần thất bại liên tiếp
                    {
                        ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Failed);
                        // Chỉ hiện toast cảnh báo định kỳ để không spam
                        if (retries % 5 == 0)
                        {
                            ToastNotificationService.Instance.ShowToast($"Không thể kết nối lại {resource.ResourceId}. Đang tiếp tục thử trong nền.", ToastType.Warning);
                        }
                    }
                    else
                    {
                        ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Disconnected);
                    }
                }
            }
        }
    }
}
