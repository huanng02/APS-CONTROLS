using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LaneStateModel : BaseViewModel
    {
        public int DbLaneId { get; set; }
        public int UiLaneIndex { get; set; }
        
        private string _laneName;
        public string LaneName
        {
            get => _laneName;
            set { _laneName = value; OnPropertyChanged(nameof(LaneName)); }
        }
        
        private string _currentDirection;
        public string CurrentDirection
        {
            get => _currentDirection;
            set 
            { 
                _currentDirection = value; 
                OnPropertyChanged(nameof(CurrentDirection)); 
                OnPropertyChanged(nameof(DirectionText)); 
                OnPropertyChanged(nameof(DirectionColor)); 
                OnPropertyChanged(nameof(StatusColor)); 
                OnPropertyChanged(nameof(ProcessingStatusText)); 
            }
        }

        private bool _isLocked;
        public bool IsLocked
        {
            get => _isLocked;
            set 
            { 
                _isLocked = value; 
                OnPropertyChanged(nameof(IsLocked)); 
                OnPropertyChanged(nameof(StatusColor)); 
                OnPropertyChanged(nameof(ProcessingStatusText)); 
            }
        }

        public string DirectionText
        {
            get
            {
                switch (CurrentDirection?.ToUpper())
                {
                    case "IN": return "CHIỀU VÀO";
                    case "OUT": return "CHIỀU RA";
                    case "MAINTENANCE": return "BẢO TRÌ";
                    case "DISABLED": return "VÔ HIỆU HÓA";
                    default: return CurrentDirection;
                }
            }
        }

        public string DirectionColor
        {
            get
            {
                switch (CurrentDirection?.ToUpper())
                {
                    case "IN": return "#4CAF50";         // Green
                    case "OUT": return "#2196F3";        // Blue
                    case "MAINTENANCE": return "#FF9800";  // Orange
                    case "DISABLED": return "#9E9E9E";     // Grey
                    default: return "#4CAF50";
                }
            }
        }

        public string ProcessingStatusText
        {
            get
            {
                if (CurrentDirection == "DISABLED") return "Vô hiệu hóa (Tạm dừng)";
                if (CurrentDirection == "MAINTENANCE") return "Đang bảo trì";
                if (IsLocked) return "Đang bận (Đang xử lý)";
                return "Sẵn sàng (Rảnh)";
            }
        }

        public string StatusColor
        {
            get
            {
                if (CurrentDirection == "DISABLED") return "#9E9E9E";    // Grey
                if (CurrentDirection == "MAINTENANCE") return "#FF9800"; // Orange
                if (IsLocked) return "#F44336";                          // Red (Busy)
                return "#4CAF50";                                        // Green (Ready)
            }
        }

        public ICommand SetInboundCommand { get; set; }
        public ICommand SetOutboundCommand { get; set; }
        public ICommand SetMaintenanceCommand { get; set; }
        public ICommand EmergencyOpenCommand { get; set; }
    }

    public class LaneRuntimeControlViewModel : BaseViewModel
    {
        public ObservableCollection<LaneStateModel> Lanes { get; set; } = new();
        private DispatcherTimer _timer;

        public LaneRuntimeControlViewModel()
        {
            LoadLanes();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateLaneStates();
            _timer.Start();
        }

        private async void LoadLanes()
        {
            try
            {
                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();

                var activeMappings = ReaderLaneMappingService.Instance.GetAll()
                    .Where(m => m.IsEnabled)
                    .ToList();
                
                var distinctLaneIds = activeMappings
                    .Select(m => m.LaneId)
                    .Distinct()
                    .ToList();

                for (int i = 1; i <= 2; i++)
                {
                    int dbLaneId = i;
                    if (i == 1)
                    {
                        dbLaneId = distinctLaneIds.Count > 0 ? distinctLaneIds[0] : 1;
                    }
                    else
                    {
                        dbLaneId = distinctLaneIds.Count > 1 ? distinctLaneIds[1] : 2;
                    }
                    var laneDb = lanes.FirstOrDefault(l => l.Id == dbLaneId);
                    string laneName = laneDb?.LaneName ?? $"LÀN SỐ {i}";

                    var lane = new LaneStateModel
                    {
                        DbLaneId = dbLaneId,
                        UiLaneIndex = i,
                        LaneName = laneName.ToUpper(),
                        SetInboundCommand = new RelayCommand(p => SetLaneDirection((int)p, "IN")),
                        SetOutboundCommand = new RelayCommand(p => SetLaneDirection((int)p, "OUT")),
                        SetMaintenanceCommand = new RelayCommand(p => SetLaneDirection((int)p, "MAINTENANCE")),
                        EmergencyOpenCommand = new RelayCommand(p => EmergencyOpen((int)p))
                    };
                    Lanes.Add(lane);
                }
                UpdateLaneStates();
            }
            catch { }
        }

        private void UpdateLaneStates()
        {
            foreach (var lane in Lanes)
            {
                var state = LaneRuntimeManager.Instance.GetLaneState(lane.DbLaneId);
                lane.CurrentDirection = state.CurrentDirection;
                lane.IsLocked = state.IsLocked;
            }
        }

        private async void SetLaneDirection(int dbLaneId, string direction)
        {
            LaneRuntimeManager.Instance.SetLaneDirection(dbLaneId, direction);
            LoggingService.Instance.LogAudit("SYSTEM", $"Changed Lane {dbLaneId} direction to {direction}");
            UpdateLaneStates();
            
            try
            {
                var lane = await ParkingTopologyService.Instance.GetLaneByIdAsync(dbLaneId);
                if (lane != null)
                {
                    lane.Direction = direction;
                    await ParkingTopologyService.Instance.SaveLaneAsync(lane);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("LaneControl", "SaveDirection", ex.Message);
            }
        }

        private void EmergencyOpen(int uiLaneIndex)
        {
            Task.Run(async () =>
            {
                bool result = await C3200Service.Instance.OpenBarrierAsync(uiLaneIndex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(result ? $"✅ Đã mở khẩn cấp Barrier Làn {uiLaneIndex}" : $"❌ Lỗi mở Barrier Làn {uiLaneIndex}!");
                });
                LoggingService.Instance.LogAudit("SYSTEM", $"Emergency Open Lane {uiLaneIndex}");
            });
        }
        
        public void Cleanup()
        {
            _timer?.Stop();
        }
    }
}
