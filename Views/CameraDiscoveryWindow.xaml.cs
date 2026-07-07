using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.Onvif;

namespace QuanLyGiuXe.Views
{
    public partial class CameraDiscoveryWindow : Window, INotifyPropertyChanged
    {
        private readonly OnvifDiscoveryService _discoveryService = new OnvifDiscoveryService();
        private readonly OnvifDeviceClient _deviceClient = new OnvifDeviceClient();
        
        private ObservableCollection<DiscoveredCamera> _discoveredCameras = new ObservableCollection<DiscoveredCamera>();
        public ObservableCollection<DiscoveredCamera> DiscoveredCameras
        {
            get => _discoveredCameras;
            set { _discoveredCameras = value; OnPropertyChanged(); }
        }

        private DiscoveredCamera? _selectedCamera;
        public DiscoveredCamera? SelectedCamera
        {
            get => _selectedCamera;
            set { _selectedCamera = value; OnPropertyChanged(); }
        }

        public DiscoveredCamera? ResultCamera { get; private set; }
        public string EnteredUsername { get; private set; } = "admin";
        public string EnteredPassword { get; private set; } = string.Empty;

        public CameraDiscoveryWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += CameraDiscoveryWindow_Loaded;
        }

        private async void CameraDiscoveryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await StartScanAsync();
        }

        private async void ScanBtn_Click(object sender, RoutedEventArgs e)
        {
            await StartScanAsync();
        }

        private async Task StartScanAsync()
        {
            ScanBtn.IsEnabled = false;
            LoadingOverlay.Visibility = Visibility.Visible;
            NoDevicesTxt.Visibility = Visibility.Collapsed;
            DiscoveredCameras.Clear();
            SelectedCamera = null;

            try
            {
                var cameras = await _discoveryService.DiscoverCamerasAsync();
                foreach (var cam in cameras)
                {
                    DiscoveredCameras.Add(cam);
                }

                if (DiscoveredCameras.Count == 0)
                {
                    NoDevicesTxt.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi quét thiết bị: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                NoDevicesTxt.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ScanBtn.IsEnabled = true;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void SelectBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProcessSelectionAsync();
        }

        private async void DiscoveryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await ProcessSelectionAsync();
        }

        private async Task ProcessSelectionAsync()
        {
            if (SelectedCamera == null)
            {
                MessageBox.Show("Vui lòng chọn một camera từ danh sách.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string username = UsernameBox.Text;
            string password = PasswordBox.Password;

            // Show a progress indicator/cursor while querying RTSP URL
            Cursor = Cursors.Wait;
            SelectBtn.IsEnabled = false;
            CancelBtn.IsEnabled = false;
            ScanBtn.IsEnabled = false;

            try
            {
                // Retrieve the RTSP URL from the camera using ONVIF
                string rtspUrl = await _deviceClient.GetRtspUrlAsync(SelectedCamera.ServiceUrl, username, password);
                
                SelectedCamera.RtspUrl = rtspUrl;
                
                // Parse RTSP port if available in URL
                if (Uri.TryCreate(rtspUrl, UriKind.Absolute, out Uri? rtspUri))
                {
                    SelectedCamera.RtspPort = rtspUri.Port > 0 ? rtspUri.Port : 554;
                }

                ResultCamera = SelectedCamera;
                EnteredUsername = username;
                EnteredPassword = password;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể kết nối ONVIF lấy RTSP URL. Vui lòng kiểm tra tài khoản/mật khẩu camera.\nChi tiết: {ex.Message}", 
                                "Lỗi Kết Nối ONVIF", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Warning);
            }
            finally
            {
                Cursor = Cursors.Arrow;
                SelectBtn.IsEnabled = true;
                CancelBtn.IsEnabled = true;
                ScanBtn.IsEnabled = true;
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
