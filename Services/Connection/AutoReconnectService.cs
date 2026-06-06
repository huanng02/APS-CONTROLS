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
            lock (_resources)
            {
                if (!_resources.Any(r => r.ResourceId == resource.ResourceId))
                {
                    _resources.Add(resource);
                    _retryCounts[resource.ResourceId] = 0;
                }
            }
        }

        public void UpdateCameraResources(List<string> activeKeys, CameraService cameraService)
        {
            lock (_resources)
            {
                // Remove existing camera resources
                _resources.RemoveAll(r => r.Type == ResourceType.Camera);

                // Register new camera resources for the active keys currently running
                foreach (var key in activeKeys)
                {
                    string resId = $"Camera_{key}";
                    if (!_resources.Any(r => r.ResourceId == resId))
                    {
                        _resources.Add(new CameraResource(key, cameraService));
                        _retryCounts[resId] = 0;
                    }
                }
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
                    List<IConnectionResource> targets;
                    lock (_resources)
                    {
                        targets = _resources.ToList();
                    }

                    foreach (var resource in targets)
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
                    List<IConnectionResource> targets;
                    lock (_resources)
                    {
                        targets = _resources.ToList();
                    }

                    foreach (var resource in targets)
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
            var oldState = ConnectionStateService.Instance.GetState(resource.ResourceId);

            if (isHealthy)
            {
                ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Connected);
                _retryCounts[resource.ResourceId] = 0; // Reset số lần thử nếu ok

                if (oldState != ConnectionState.Connected)
                {
                    string displayName = ConnectionStateService.Instance.GetDisplayName(resource.ResourceId);
                    LoggingService.Instance.LogInfo("CONNECTION", resource.ResourceId, $"{displayName} connected successfully.");
                }
            }
            else
            {
                // 2. Nếu không khỏe, kích hoạt Reconnect
                _retryCounts[resource.ResourceId]++;
                int retries = _retryCounts[resource.ResourceId];
                
                ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Reconnecting);
                
                // Thử kết nối lại
                bool success = await resource.ReconnectAsync(token);
                
                if (success)
                {
                    ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Connected);
                    _retryCounts[resource.ResourceId] = 0;

                    if (oldState != ConnectionState.Connected)
                    {
                        string displayName = ConnectionStateService.Instance.GetDisplayName(resource.ResourceId);
                        LoggingService.Instance.LogInfo("CONNECTION", resource.ResourceId, $"{displayName} reconnected successfully.");
                    }
                }
                else
                {
                    if (retries >= 5) // Sau 5 lần thất bại liên tiếp
                    {
                        ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Failed);
                        
                        // Log only once when transitioning to failed state to avoid spamming the log feed
                        if (oldState != ConnectionState.Failed)
                        {
                            string displayName = ConnectionStateService.Instance.GetDisplayName(resource.ResourceId);
                            LoggingService.Instance.LogError("CONNECTION", resource.ResourceId, $"{displayName} connection failed (persistently offline).", null);
                        }

                        // Chỉ hiện toast cảnh báo định kỳ để không spam
                        if (retries % 5 == 0 && resource.Type != ResourceType.Camera)
                        {
                            string displayName = ConnectionStateService.Instance.GetDisplayName(resource.ResourceId);
                            ToastNotificationService.Instance.ShowToast($"Không thể kết nối lại {displayName}. Đang tiếp tục thử trong nền.", ToastType.Warning);
                        }
                    }
                    else
                    {
                        ConnectionStateService.Instance.UpdateState(resource.ResourceId, ConnectionState.Disconnected);

                        // Log only once when transitioning from online to offline
                        if (oldState == ConnectionState.Connected)
                        {
                            string displayName = ConnectionStateService.Instance.GetDisplayName(resource.ResourceId);
                            LoggingService.Instance.LogWarning("CONNECTION", resource.ResourceId, $"{displayName} disconnected. Retrying reconnection...");
                        }
                    }
                }
            }
        }
    }
}
