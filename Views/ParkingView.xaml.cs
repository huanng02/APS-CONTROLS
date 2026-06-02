using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class ParkingView : UserControl
    {
        private MainViewModel _mainVm;

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

