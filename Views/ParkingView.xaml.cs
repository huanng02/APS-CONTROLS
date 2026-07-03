using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class ParkingView : UserControl
    {
        private MainViewModel _mainVm;
        private ScanSessionControl? _lane1SessionControl;
        private ScanSessionControl? _lane2SessionControl;
        private System.Windows.Threading.DispatcherTimer? _lane1CloseTimer;
        private System.Windows.Threading.DispatcherTimer? _lane2CloseTimer;
        private bool _isLoadingRoi = false;

        public ParkingView()
        {
            InitializeComponent();
            this.Loaded += ParkingView_Loaded;
            this.Unloaded += ParkingView_Unloaded;
        }

        private void ParkingView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Window.GetWindow(this) is Window window && window.DataContext is MainViewModel vm)
                {
                    _mainVm = vm;
                    _mainVm.PropertyChanged += Vm_PropertyChanged;
                    UpdateLaneLayout();
                    ApplyRoiSettings();
                }
            }
            catch { }
        }

        private void ParkingView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_mainVm != null)
                {
                    _mainVm.PropertyChanged -= Vm_PropertyChanged;
                    _mainVm = null;
                }
            }
            catch { }
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsLane1Visible) ||
                e.PropertyName == nameof(MainViewModel.IsLane2Visible) ||
                e.PropertyName == nameof(MainViewModel.IsDualLaneMode))
            {
                UpdateLaneLayout();
            }
        }

        private void UpdateLaneLayout()
        {
            try
            {
                var vm = _mainVm ?? (Application.Current.MainWindow?.DataContext as MainViewModel);
                if (vm != null)
                {
                    if (vm.IsLane1Visible && vm.IsLane2Visible)
                    {
                        Lane1Column.Width = new GridLength(1, GridUnitType.Star);
                        Lane2Column.Width = new GridLength(1, GridUnitType.Star);
                        LaneSplitterColumn.Width = new GridLength(5);
                    }
                    else if (vm.IsLane1Visible)
                    {
                        Lane1Column.Width = new GridLength(1, GridUnitType.Star);
                        Lane2Column.Width = new GridLength(0);
                        LaneSplitterColumn.Width = new GridLength(0);
                    }
                    else if (vm.IsLane2Visible)
                    {
                        Lane1Column.Width = new GridLength(0);
                        Lane2Column.Width = new GridLength(1, GridUnitType.Star);
                        LaneSplitterColumn.Width = new GridLength(0);
                    }
                }
            }
            catch { }
        }

        private void OpenGateIn_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.OpenGateIn_Click(sender, e);
            }
        }

        private void OpenGateOut_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.OpenGateOut_Click(sender, e);
            }
        }



        // Show session overlay inside lane area
        public void ShowLaneSession(int laneIndex, QuanLyGiuXe.Models.LichSuXe session)
        {
            if (laneIndex == 1)
            {
                if (_lane1SessionControl == null)
                {
                    _lane1SessionControl = new ScanSessionControl();
                    _lane1SessionControl.RequestClose += (s, e) => CloseLaneSession(1);
                    // create timer to auto-close after 6 seconds
                    _lane1CloseTimer = new System.Windows.Threading.DispatcherTimer();
                    _lane1CloseTimer.Interval = TimeSpan.FromSeconds(6);
                    _lane1CloseTimer.Tick += (s, e) => { _lane1CloseTimer?.Stop(); CloseLaneSession(1); };
                }
                _lane1SessionControl.SetSession(session);
                var host = this.FindName("Lane1SessionHost") as ContentControl;
                if (host != null)
                {
                    host.Content = _lane1SessionControl;
                    host.Visibility = System.Windows.Visibility.Visible;
                    host.IsHitTestVisible = true;
                    _lane1CloseTimer?.Stop();
                    _lane1CloseTimer?.Start();
                }
            }
            else
            {
                if (_lane2SessionControl == null)
                {
                    _lane2SessionControl = new ScanSessionControl();
                    _lane2SessionControl.RequestClose += (s, e) => CloseLaneSession(2);
                    _lane2CloseTimer = new System.Windows.Threading.DispatcherTimer();
                    _lane2CloseTimer.Interval = TimeSpan.FromSeconds(6);
                    _lane2CloseTimer.Tick += (s, e) => { _lane2CloseTimer?.Stop(); CloseLaneSession(2); };
                }
                _lane2SessionControl.SetSession(session);
                var host = this.FindName("Lane2SessionHost") as ContentControl;
                if (host != null)
                {
                    host.Content = _lane2SessionControl;
                    host.Visibility = System.Windows.Visibility.Visible;
                    host.IsHitTestVisible = true;
                    _lane2CloseTimer?.Stop();
                    _lane2CloseTimer?.Start();
                }
            }
        }

        public void CloseLaneSession(int laneIndex)
        {
            if (laneIndex == 1)
            {
                var host = this.FindName("Lane1SessionHost") as ContentControl;
                if (host != null)
                {
                    host.Content = null;
                    host.Visibility = System.Windows.Visibility.Collapsed;
                    host.IsHitTestVisible = false;
                }
                _lane1SessionControl = null;
                _lane1CloseTimer?.Stop();
                _lane1CloseTimer = null;
            }
            else
            {
                var host = this.FindName("Lane2SessionHost") as ContentControl;
                if (host != null)
                {
                    host.Content = null;
                    host.Visibility = System.Windows.Visibility.Collapsed;
                    host.IsHitTestVisible = false;
                }
                _lane2SessionControl = null;
                _lane2CloseTimer?.Stop();
                _lane2CloseTimer = null;
            }
        }
        
        // Methods to update cameras from MainWindow
        public void UpdateCamera(string key, System.Windows.Media.ImageSource source)
        {
            switch (key)
            {
                case "Vao1": CameraVao1.Source = source; break;
                case "Vao2": CameraVao2.Source = source; break;
                case "Ra1": CameraRa1.Source = source; break;
                case "Ra2": CameraRa2.Source = source; break;
            }
        }

        private void ApplyRoiSettings()
        {
            try
            {
                var cfg = Services.AppConfig.Load();
                var vm = _mainVm ?? (System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel);
                if (vm != null)
                {
                    // Làn vào 1
                    int? dbLaneId1 = vm.GetDbLaneIdForUiIndex(1);
                    if (dbLaneId1.HasValue)
                    {
                        var laneCam1 = cfg.Cameras.LaneCameras.Find(c => c.LaneId == dbLaneId1.Value);
                        if (laneCam1 != null)
                        {
                            SetGridRoi(GridRoiVao, laneCam1.RoiX, laneCam1.RoiY, laneCam1.RoiWidth, laneCam1.RoiHeight);
                        }
                    }
                    
                    // Làn ra 2
                    int? dbLaneId2 = vm.GetDbLaneIdForUiIndex(2);
                    if (dbLaneId2.HasValue)
                    {
                        var laneCam2 = cfg.Cameras.LaneCameras.Find(c => c.LaneId == dbLaneId2.Value);
                        if (laneCam2 != null)
                        {
                            SetGridRoi(GridRoiRa, laneCam2.RoiX, laneCam2.RoiY, laneCam2.RoiWidth, laneCam2.RoiHeight);
                        }
                    }
                }
            }
            catch { }
        }

        private void SetGridRoi(Grid grid, double rx, double ry, double rw, double rh)
        {
            if (grid == null || grid.ColumnDefinitions.Count < 3 || grid.RowDefinitions.Count < 3) return;

            rx = System.Math.Max(0.0, System.Math.Min(1.0, rx));
            ry = System.Math.Max(0.0, System.Math.Min(1.0, ry));
            rw = System.Math.Max(0.0, System.Math.Min(1.0 - rx, rw));
            rh = System.Math.Max(0.0, System.Math.Min(1.0 - ry, rh));

            double rxRight = 1.0 - rx - rw;
            double ryBottom = 1.0 - ry - rh;

            grid.ColumnDefinitions[0].Width = new GridLength(rx * 100, GridUnitType.Star);
            grid.ColumnDefinitions[1].Width = new GridLength(rw * 100, GridUnitType.Star);
            grid.ColumnDefinitions[2].Width = new GridLength(rxRight * 100, GridUnitType.Star);

            grid.RowDefinitions[0].Height = new GridLength(ry * 100, GridUnitType.Star);
            grid.RowDefinitions[1].Height = new GridLength(rh * 100, GridUnitType.Star);
            grid.RowDefinitions[2].Height = new GridLength(ryBottom * 100, GridUnitType.Star);
        }

        #region ROI Settings Adjustment Panel Events

        // --- LÀN VÀO (ENTRY LANE - 1) ---
        private void BtnSettingRoiVao_Click(object sender, RoutedEventArgs e)
        {
            if (PanelRoiVao.Visibility == Visibility.Visible)
            {
                PanelRoiVao.Visibility = Visibility.Collapsed;
                return;
            }

            PanelRoiVao.Visibility = Visibility.Visible;
            try
            {
                var cfg = Services.AppConfig.Load();
                var vm = _mainVm ?? (System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel);
                if (vm != null)
                {
                    int? dbLaneId = vm.GetDbLaneIdForUiIndex(1);
                    if (dbLaneId.HasValue)
                    {
                        var laneCam = cfg.Cameras.LaneCameras.Find(c => c.LaneId == dbLaneId.Value);
                        if (laneCam != null)
                        {
                            _isLoadingRoi = true;
                            SliderRoiVaoX.Value = laneCam.RoiX * 100;
                            SliderRoiVaoY.Value = laneCam.RoiY * 100;
                            SliderRoiVaoW.Value = laneCam.RoiWidth * 100;
                            SliderRoiVaoH.Value = laneCam.RoiHeight * 100;
                            _isLoadingRoi = false;
                        }
                    }
                }
            }
            catch { }
        }

        private void SliderRoiVao_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingRoi || GridRoiVao == null || SliderRoiVaoX == null || SliderRoiVaoY == null || SliderRoiVaoW == null || SliderRoiVaoH == null) return;
            SetGridRoi(GridRoiVao, SliderRoiVaoX.Value / 100, SliderRoiVaoY.Value / 100, SliderRoiVaoW.Value / 100, SliderRoiVaoH.Value / 100);
        }

        private void BtnSaveRoiVao_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cfg = Services.AppConfig.Load();
                var vm = _mainVm ?? (System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel);
                if (vm != null)
                {
                    int? dbLaneId = vm.GetDbLaneIdForUiIndex(1);
                    if (dbLaneId.HasValue)
                    {
                        var laneCam = cfg.Cameras.LaneCameras.Find(c => c.LaneId == dbLaneId.Value);
                        if (laneCam != null)
                        {
                            laneCam.RoiX = SliderRoiVaoX.Value / 100.0;
                            laneCam.RoiY = SliderRoiVaoY.Value / 100.0;
                            laneCam.RoiWidth = SliderRoiVaoW.Value / 100.0;
                            laneCam.RoiHeight = SliderRoiVaoH.Value / 100.0;
                            cfg.Save();
                            MessageBox.Show("Đã lưu cấu hình vùng quét biển số Làn Vào thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi lưu cấu hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            PanelRoiVao.Visibility = Visibility.Collapsed;
        }

        private void BtnCloseRoiVao_Click(object sender, RoutedEventArgs e)
        {
            PanelRoiVao.Visibility = Visibility.Collapsed;
            ApplyRoiSettings();
        }


        // --- LÀN RA (EXIT LANE - 2) ---
        private void BtnSettingRoiRa_Click(object sender, RoutedEventArgs e)
        {
            if (PanelRoiRa.Visibility == Visibility.Visible)
            {
                PanelRoiRa.Visibility = Visibility.Collapsed;
                return;
            }

            PanelRoiRa.Visibility = Visibility.Visible;
            try
            {
                var cfg = Services.AppConfig.Load();
                var vm = _mainVm ?? (System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel);
                if (vm != null)
                {
                    int? dbLaneId = vm.GetDbLaneIdForUiIndex(2);
                    if (dbLaneId.HasValue)
                    {
                        var laneCam = cfg.Cameras.LaneCameras.Find(c => c.LaneId == dbLaneId.Value);
                        if (laneCam != null)
                        {
                            _isLoadingRoi = true;
                            SliderRoiRaX.Value = laneCam.RoiX * 100;
                            SliderRoiRaY.Value = laneCam.RoiY * 100;
                            SliderRoiRaW.Value = laneCam.RoiWidth * 100;
                            SliderRoiRaH.Value = laneCam.RoiHeight * 100;
                            _isLoadingRoi = false;
                        }
                    }
                }
            }
            catch { }
        }

        private void SliderRoiRa_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoadingRoi || GridRoiRa == null || SliderRoiRaX == null || SliderRoiRaY == null || SliderRoiRaW == null || SliderRoiRaH == null) return;
            SetGridRoi(GridRoiRa, SliderRoiRaX.Value / 100, SliderRoiRaY.Value / 100, SliderRoiRaW.Value / 100, SliderRoiRaH.Value / 100);
        }

        private void BtnSaveRoiRa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cfg = Services.AppConfig.Load();
                var vm = _mainVm ?? (System.Windows.Application.Current.MainWindow?.DataContext as MainViewModel);
                if (vm != null)
                {
                    int? dbLaneId = vm.GetDbLaneIdForUiIndex(2);
                    if (dbLaneId.HasValue)
                    {
                        var laneCam = cfg.Cameras.LaneCameras.Find(c => c.LaneId == dbLaneId.Value);
                        if (laneCam != null)
                        {
                            laneCam.RoiX = SliderRoiRaX.Value / 100.0;
                            laneCam.RoiY = SliderRoiRaY.Value / 100.0;
                            laneCam.RoiWidth = SliderRoiRaW.Value / 100.0;
                            laneCam.RoiHeight = SliderRoiRaH.Value / 100.0;
                            cfg.Save();
                            MessageBox.Show("Đã lưu cấu hình vùng quét biển số Làn Ra thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi lưu cấu hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            PanelRoiRa.Visibility = Visibility.Collapsed;
        }

        private void BtnCloseRoiRa_Click(object sender, RoutedEventArgs e)
        {
            PanelRoiRa.Visibility = Visibility.Collapsed;
            ApplyRoiSettings();
        }

        #endregion
    }
}

