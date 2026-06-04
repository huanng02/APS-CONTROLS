using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class MonitoringDashboardViewModel : BaseViewModel, IDisposable
    {
        private readonly DeviceMonitoringService _monitoringService = DeviceMonitoringService.Instance;
        private readonly DispatcherTimer _refreshTimer;
        private bool _disposed = false;

        // ── Device Status Collections ──────────────────────────────────────
        public ObservableCollection<LaneStatusDto> LanesStatus { get; } = new ObservableCollection<LaneStatusDto>();
        public ObservableCollection<C3ControllerStatusDto> C3ControllersStatus { get; } = new ObservableCollection<C3ControllerStatusDto>();
        public ObservableCollection<RfidReaderStatusDto> RfidReadersStatus { get; } = new ObservableCollection<RfidReaderStatusDto>();
        public ObservableCollection<CameraStatusDto> CamerasStatus { get; } = new ObservableCollection<CameraStatusDto>();
        public ObservableCollection<DeviceEventDto> DeviceEvents { get; } = new ObservableCollection<DeviceEventDto>();

        // ── Infrastructure Summary ─────────────────────────────────────────
        private InfrastructureSummaryDto _summary = new();
        public InfrastructureSummaryDto Summary
        {
            get => _summary;
            private set
            {
                _summary = value;
                OnPropertyChanged(nameof(Summary));
            }
        }

        // ── Operational Monitoring State ───────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        private string _lastUpdatedTime = "Chưa cập nhật";
        public string LastUpdatedTime
        {
            get => _lastUpdatedTime;
            set { _lastUpdatedTime = value; OnPropertyChanged(nameof(LastUpdatedTime)); }
        }

        private int _refreshIntervalSeconds = 10;
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (_refreshIntervalSeconds != value)
                {
                    _refreshIntervalSeconds = value;
                    OnPropertyChanged(nameof(RefreshIntervalSeconds));
                    if (_refreshTimer != null)
                    {
                        _refreshTimer.Interval = TimeSpan.FromSeconds(value);
                    }
                }
            }
        }

        private bool _isAutoRefreshEnabled = true;
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                if (_isAutoRefreshEnabled != value)
                {
                    _isAutoRefreshEnabled = value;
                    OnPropertyChanged(nameof(IsAutoRefreshEnabled));
                    if (value) _refreshTimer.Start();
                    else _refreshTimer.Stop();
                }
            }
        }

        // ── Summary Counts (Lanes) ─────────────────────────────────────────
        private int _onlineLanesCount;
        public int OnlineLanesCount
        {
            get => _onlineLanesCount;
            private set { _onlineLanesCount = value; OnPropertyChanged(nameof(OnlineLanesCount)); }
        }

        private int _totalLanesCount;
        public int TotalLanesCount
        {
            get => _totalLanesCount;
            private set { _totalLanesCount = value; OnPropertyChanged(nameof(TotalLanesCount)); }
        }

        // ── Summary Counts (Controllers) ───────────────────────────────────
        private int _onlineControllersCount;
        public int OnlineControllersCount
        {
            get => _onlineControllersCount;
            private set { _onlineControllersCount = value; OnPropertyChanged(nameof(OnlineControllersCount)); }
        }

        private int _totalControllersCount;
        public int TotalControllersCount
        {
            get => _totalControllersCount;
            private set { _totalControllersCount = value; OnPropertyChanged(nameof(TotalControllersCount)); }
        }

        // ── Summary Counts (Readers) ───────────────────────────────────────
        private int _onlineReadersCount;
        public int OnlineReadersCount
        {
            get => _onlineReadersCount;
            private set { _onlineReadersCount = value; OnPropertyChanged(nameof(OnlineReadersCount)); }
        }

        private int _totalReadersCount;
        public int TotalReadersCount
        {
            get => _totalReadersCount;
            private set { _totalReadersCount = value; OnPropertyChanged(nameof(TotalReadersCount)); }
        }



        // ── Summary Counts (Cameras) ───────────────────────────────────────
        private int _onlineCamerasCount;
        public int OnlineCamerasCount
        {
            get => _onlineCamerasCount;
            private set { _onlineCamerasCount = value; OnPropertyChanged(nameof(OnlineCamerasCount)); }
        }

        private int _totalCamerasCount;
        public int TotalCamerasCount
        {
            get => _totalCamerasCount;
            private set { _totalCamerasCount = value; OnPropertyChanged(nameof(TotalCamerasCount)); }
        }

        // ── Device Detail Popup ────────────────────────────────────────────
        private DeviceDetailDto? _selectedDeviceDetail;
        public DeviceDetailDto? SelectedDeviceDetail
        {
            get => _selectedDeviceDetail;
            set { _selectedDeviceDetail = value; OnPropertyChanged(nameof(SelectedDeviceDetail)); }
        }

        private bool _isDeviceDetailVisible;
        public bool IsDeviceDetailVisible
        {
            get => _isDeviceDetailVisible;
            set { _isDeviceDetailVisible = value; OnPropertyChanged(nameof(IsDeviceDetailVisible)); }
        }

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand RefreshCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }
        public ICommand ShowDeviceDetailCommand { get; }
        public ICommand CloseDeviceDetailCommand { get; }

        public MonitoringDashboardViewModel()
        {
            RefreshCommand = new RelayCommand(_ => _ = LoadMonitoringDataAsync());
            ToggleAutoRefreshCommand = new RelayCommand(_ => IsAutoRefreshEnabled = !IsAutoRefreshEnabled);
            ShowDeviceDetailCommand = new RelayCommand(async param => await ShowDeviceDetailAsync(param));
            CloseDeviceDetailCommand = new RelayCommand(_ => { IsDeviceDetailVisible = false; SelectedDeviceDetail = null; });

            // Initialize Timer
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds)
            };
            _refreshTimer.Tick += (s, e) => _ = LoadMonitoringDataAsync();

            // Initial load
            _ = LoadMonitoringDataAsync();

            // Start timer if auto refresh is enabled
            if (IsAutoRefreshEnabled)
            {
                _refreshTimer.Start();
            }
        }

        public async Task LoadMonitoringDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                // 1. Fetch C3 Controller statuses (with parallel pings)
                var controllerStatuses = await _monitoringService.GetC3ControllersStatusAsync();

                // 2. Fetch Cameras (independent of controllers)
                var cameraStatuses = await _monitoringService.GetCamerasStatusAsync();

                // 3. Fetch Lane and Reader statuses based on controller + camera status
                var lanesResult = await _monitoringService.GetLanesStatusAsync(controllerStatuses, cameraStatuses);
                var readersResult = await _monitoringService.GetRfidReadersStatusAsync(controllerStatuses);

                // 4. Detect state changes and log events
                await _monitoringService.DetectAndLogStateChanges(controllerStatuses, readersResult, cameraStatuses);

                // 5. Fetch latest device events
                var events = await _monitoringService.GetDeviceEventsAsync(100);

                // ── Update UI on Dispatcher thread ─────────────────────────

                // Lanes
                LanesStatus.Clear();
                int onlineLanes = 0;
                foreach (var lane in lanesResult)
                {
                    LanesStatus.Add(lane);
                    if (lane.IsOnline) onlineLanes++;
                }
                OnlineLanesCount = onlineLanes;
                TotalLanesCount = lanesResult.Count;

                // Controllers
                C3ControllersStatus.Clear();
                int onlineCtrls = 0;
                foreach (var ctrl in controllerStatuses)
                {
                    C3ControllersStatus.Add(ctrl);
                    if (ctrl.IsOnline) onlineCtrls++;
                }
                OnlineControllersCount = onlineCtrls;
                TotalControllersCount = controllerStatuses.Count;

                // Readers
                RfidReadersStatus.Clear();
                int onlineReaders = 0;
                foreach (var reader in readersResult)
                {
                    RfidReadersStatus.Add(reader);
                    if (reader.IsOnline) onlineReaders++;
                }
                OnlineReadersCount = onlineReaders;
                TotalReadersCount = readersResult.Count;



                // Cameras
                CamerasStatus.Clear();
                int onlineCameras = 0;
                foreach (var cam in cameraStatuses)
                {
                    CamerasStatus.Add(cam);
                    if (cam.IsOnline) onlineCameras++;
                }
                OnlineCamerasCount = onlineCameras;
                TotalCamerasCount = cameraStatuses.Count;

                // Events
                DeviceEvents.Clear();
                foreach (var evt in events)
                {
                    DeviceEvents.Add(evt);
                }

                // Infrastructure Summary
                Summary = new InfrastructureSummaryDto
                {
                    TotalControllers = controllerStatuses.Count,
                    OnlineControllers = onlineCtrls,
                    TotalReaders = readersResult.Count,
                    OnlineReaders = onlineReaders,
                    TotalCameras = cameraStatuses.Count,
                    OnlineCameras = onlineCameras,
                    TotalLanes = lanesResult.Count,
                    OnlineLanes = onlineLanes
                };

                LastUpdatedTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("MonitoringDashboardViewModel", "LoadMonitoringDataAsync", "Error loading monitoring data", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ShowDeviceDetailAsync(object? param)
        {
            if (param is not string[] args || args.Length < 2) return;

            string deviceType = args[0];
            string deviceName = args[1];

            try
            {
                var detail = await _monitoringService.GetDeviceDetailAsync(deviceType, deviceName);
                if (detail != null)
                {
                    SelectedDeviceDetail = detail;
                    IsDeviceDetailVisible = true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("MonitoringDashboardViewModel", "ShowDeviceDetailAsync", $"Error loading device detail for {deviceType}/{deviceName}", ex);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _refreshTimer?.Stop();
                }
                _disposed = true;
            }
        }
    }
}
