using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services.Connection
{
    public class ConnectionStateService : INotifyPropertyChanged
    {
        private static readonly Lazy<ConnectionStateService> _instance = 
            new Lazy<ConnectionStateService>(() => new ConnectionStateService());
        
        public static ConnectionStateService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, ConnectionState> _states = new();
        private readonly ConcurrentDictionary<string, bool> _notifiedOffline = new();

        public event PropertyChangedEventHandler PropertyChanged;

        private ConnectionStateService() { }

        public void UpdateState(string resourceId, ConnectionState newState)
        {
            if (!_states.TryGetValue(resourceId, out var oldState) || oldState != newState)
            {
                _states[resourceId] = newState;
                OnPropertyChanged(resourceId);
                
                // Cũng thông báo cho một property chung nếu cần
                OnPropertyChanged(nameof(AllStates));

                // Unified State-Change Toast Notifications
                bool isCamera = resourceId.StartsWith("Camera_");
                string displayName = GetDisplayName(resourceId);

                if (newState == ConnectionState.Connected)
                {
                    // Show reconnected toast only if it was previously confirmed offline
                    if (_notifiedOffline.TryGetValue(resourceId, out var wasOffline) && wasOffline)
                    {
                        ToastNotificationService.Instance.ShowToast($"{displayName} đã kết nối lại thành công.", ToastType.Success);
                        _notifiedOffline[resourceId] = false;
                    }
                }
                else if (newState == ConnectionState.Failed)
                {
                    // Confirmed offline state (5+ failed reconnects for camera, or DB/C3 fail)
                    if (!_notifiedOffline.TryGetValue(resourceId, out var wasOffline) || !wasOffline)
                    {
                        ToastNotificationService.Instance.ShowToast($"Mất kết nối {displayName}! Đang thử kết nối lại...", ToastType.Error);
                        _notifiedOffline[resourceId] = true;
                    }
                }
                else if (newState == ConnectionState.Disconnected)
                {
                    // For Critical resources (DB/C3), we notify immediately on Disconnected
                    if (!isCamera)
                    {
                        if (!_notifiedOffline.TryGetValue(resourceId, out var wasOffline) || !wasOffline)
                        {
                            ToastNotificationService.Instance.ShowToast($"Mất kết nối {displayName}! Đang thử kết nối lại...", ToastType.Error);
                            _notifiedOffline[resourceId] = true;
                        }
                    }
                }
            }
        }

        private string GetDisplayName(string resourceId)
        {
            if (resourceId.StartsWith("Camera_"))
            {
                string key = resourceId.Replace("Camera_", "");
                switch (key)
                {
                    case "VaoToanCanh": return "Camera Toàn Cảnh Làn Vào";
                    case "VaoBienSo": return "Camera Biển Số Làn Vào";
                    case "RaToanCanh": return "Camera Toàn Cảnh Làn Ra";
                    case "RaBienSo": return "Camera Biển Số Làn Ra";
                    default: return $"Camera {key}";
                }
            }
            if (resourceId == "C3200" || resourceId == "C3200Controller" || resourceId == "C3200Resource") return "Bộ điều khiển C3-200";
            if (resourceId == "Database") return "Cơ sở dữ liệu (SQL)";
            return resourceId;
        }

        public ConnectionState GetState(string resourceId)
        {
            return _states.TryGetValue(resourceId, out var state) ? state : ConnectionState.Disconnected;
        }

        public IReadOnlyDictionary<string, ConnectionState> AllStates => _states;

        public void ResetState()
        {
            _states.Clear();
            _notifiedOffline.Clear();
            OnPropertyChanged(nameof(AllStates));
            OnPropertyChanged(nameof(IsDatabaseOnline));
            OnPropertyChanged(nameof(IsC3Online));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Helper cho UI binding dễ dàng hơn
        public bool IsDatabaseOnline => GetState("Database") == ConnectionState.Connected;
        public bool IsC3Online => GetState("C3200") == ConnectionState.Connected;
    }
}
