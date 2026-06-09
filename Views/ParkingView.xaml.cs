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

        private void ShowCardInfo_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                // Tag set to lane number (1 or 2)
                if (sender is FrameworkElement fe && int.TryParse(fe.Tag?.ToString(), out int lane))
                {
                    main.ShowCardInfoForLane(lane);
                }
                else
                {
                    main.ShowCardInfoForLane(1);
                }
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
    }
}

