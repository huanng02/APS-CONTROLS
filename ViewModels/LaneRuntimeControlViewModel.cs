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
        public int LaneId { get; set; }
        
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

        private void LoadLanes()
        {
            // Tạm thời giả định có 2 làn
            for (int i = 1; i <= 2; i++)
            {
                var lane = new LaneStateModel
                {
                    LaneId = i,
                    SetInboundCommand = new RelayCommand(p => SetLaneDirection((int)p, "IN")),
                    SetOutboundCommand = new RelayCommand(p => SetLaneDirection((int)p, "OUT")),
                    SetMaintenanceCommand = new RelayCommand(p => SetLaneDirection((int)p, "MAINTENANCE")),
                    EmergencyOpenCommand = new RelayCommand(p => EmergencyOpen((int)p))
                };
                Lanes.Add(lane);
            }
            UpdateLaneStates();
        }

        private void UpdateLaneStates()
        {
            foreach (var lane in Lanes)
            {
                var state = LaneRuntimeManager.Instance.GetLaneState(lane.LaneId);
                lane.CurrentDirection = state.CurrentDirection;
                lane.IsLocked = state.IsLocked;
            }
        }

        private void SetLaneDirection(int laneId, string direction)
        {
            LaneRuntimeManager.Instance.SetLaneDirection(laneId, direction);
            LoggingService.Instance.LogAudit("SYSTEM", $"Changed Lane {laneId} direction to {direction}");
            UpdateLaneStates();
        }

        private void EmergencyOpen(int laneId)
        {
            Task.Run(async () =>
            {
                bool result = await C3200Service.Instance.OpenBarrierAsync(laneId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(result ? $"✅ Đã mở khẩn cấp Barrier Làn {laneId}" : $"❌ Lỗi mở Barrier Làn {laneId}!");
                });
                LoggingService.Instance.LogAudit("SYSTEM", $"Emergency Open Lane {laneId}");
            });
        }
        
        public void Cleanup()
        {
            _timer?.Stop();
        }
    }
}
