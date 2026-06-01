using System;
using System.Collections.ObjectModel;
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

        // KPI Properties
        private DeviceKpiDto _kpi = new DeviceKpiDto { TongCho = 200 };
        public DeviceKpiDto Kpi
        {
            get => _kpi;
            private set
            {
                _kpi = value;
                OnPropertyChanged(nameof(Kpi));
                OnPropertyChanged(nameof(XeTrongBai));
                OnPropertyChanged(nameof(LuotXeVaoHomNay));
                OnPropertyChanged(nameof(LuotXeRaHomNay));
                OnPropertyChanged(nameof(DoanhThuHomNay));
                OnPropertyChanged(nameof(TongCho));
                OnPropertyChanged(nameof(ChoTrong));
                OnPropertyChanged(nameof(TyLeLapDay));
                OnPropertyChanged(nameof(IsNearFull));
            }
        }

        public int XeTrongBai => Kpi.XeTrongBai;
        public int LuotXeVaoHomNay => Kpi.LuotXeVaoHomNay;
        public int LuotXeRaHomNay => Kpi.LuotXeRaHomNay;
        public double DoanhThuHomNay => Kpi.DoanhThuHomNay;
        public int TongCho => Kpi.TongCho;
        public int ChoTrong => Kpi.ChoTrong;
        public double TyLeLapDay => Kpi.TyLeLapDay;
        public bool IsNearFull => Kpi.TyLeLapDay > 90;

        // Device Status Lists
        public ObservableCollection<LaneStatusDto> LanesStatus { get; } = new ObservableCollection<LaneStatusDto>();
        public ObservableCollection<C3ControllerStatusDto> C3ControllersStatus { get; } = new ObservableCollection<C3ControllerStatusDto>();
        public ObservableCollection<RfidReaderStatusDto> RfidReadersStatus { get; } = new ObservableCollection<RfidReaderStatusDto>();

        // Operational Monitoring State
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

        // Summary Counts
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

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }

        public MonitoringDashboardViewModel()
        {
            RefreshCommand = new RelayCommand(_ => _ = LoadMonitoringDataAsync());
            ToggleAutoRefreshCommand = new RelayCommand(_ => IsAutoRefreshEnabled = !IsAutoRefreshEnabled);

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
                // 1. Fetch KPIs
                var kpiTask = _monitoringService.GetDeviceKpiAsync();
                
                // 2. Fetch C3 Controller statuses
                var controllersTask = _monitoringService.GetC3ControllersStatusAsync();
                
                await Task.WhenAll(kpiTask, controllersTask);

                var kpiResult = await kpiTask;
                var controllerStatuses = await controllersTask;

                // 3. Fetch Lane and Reader statuses based on controller status
                var lanesResult = await _monitoringService.GetLanesStatusAsync(controllerStatuses);
                var readersResult = await _monitoringService.GetRfidReadersStatusAsync(controllerStatuses);

                // Update UI on Dispatcher thread
                Kpi = kpiResult;

                LanesStatus.Clear();
                int onlineLanes = 0;
                foreach (var lane in lanesResult)
                {
                    LanesStatus.Add(lane);
                    if (lane.IsOnline) onlineLanes++;
                }
                OnlineLanesCount = onlineLanes;
                TotalLanesCount = lanesResult.Count;

                C3ControllersStatus.Clear();
                int onlineCtrls = 0;
                foreach (var ctrl in controllerStatuses)
                {
                    C3ControllersStatus.Add(ctrl);
                    if (ctrl.IsOnline) onlineCtrls++;
                }
                OnlineControllersCount = onlineCtrls;
                TotalControllersCount = controllerStatuses.Count;

                RfidReadersStatus.Clear();
                int onlineReaders = 0;
                foreach (var reader in readersResult)
                {
                    RfidReadersStatus.Add(reader);
                    if (reader.IsOnline) onlineReaders++;
                }
                OnlineReadersCount = onlineReaders;
                TotalReadersCount = readersResult.Count;

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
