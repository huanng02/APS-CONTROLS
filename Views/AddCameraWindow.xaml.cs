using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video.DirectShow;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class AddCameraWindow : Window
    {
        private FilterInfoCollection _usbDevices;
        private CancellationTokenSource? _cts;
        private bool _isConnectedSuccessfully = false;
        private bool _isPreviewOnly = false;
        private int? _targetLaneId;
        private string? _targetCameraRole; // "ToanCanh" or "BienSo"

        public QuanLyGiuXe.Models.CameraConfig? NewCamera { get; private set; }

        public AddCameraWindow()
        {
            InitializeComponent();
            _usbDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        }

        public AddCameraWindow(int laneId, string cameraRole, string laneName, string laneDirection) : this()
        {
            _targetLaneId = laneId;
            _targetCameraRole = cameraRole;
            
            // Pre-fill the Camera Name based on lane name and role
            string roleName = cameraRole == "ToanCanh" ? "Toàn Cảnh" : "Biển Số";
            CameraNameBox.Text = $"{laneName} - {roleName}";
        }

        public AddCameraWindow(string cameraName, string urlOrName)
        {
            InitializeComponent();
            _usbDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            _isPreviewOnly = true;

            // Set fields
            CameraNameBox.Text = cameraName;
            CameraNameBox.IsEnabled = false;
            CameraTypeCombo.IsEnabled = false;

            if (urlOrName.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
                urlOrName.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
                urlOrName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                urlOrName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                CameraTypeCombo.SelectedIndex = 0; // IP
                RtspUrlBox.Text = urlOrName;
                RtspUrlBox.IsEnabled = false;
                
                // Parse IP address and port from URL if possible
                try
                {
                    var uri = new Uri(urlOrName);
                    IpAddrBox.Text = uri.Host;
                    RtspPortBox.Text = uri.Port > 0 ? uri.Port.ToString() : "554";
                }
                catch {}
            }
            else
            {
                CameraTypeCombo.SelectedIndex = 1; // USB
                UsbDeviceCombo.Items.Clear();
                UsbDeviceCombo.Items.Add(urlOrName);
                UsbDeviceCombo.SelectedIndex = 0;
                UsbDeviceCombo.IsEnabled = false;
            }

            // In preview-only mode, we don't show the "Save" button and we change the close button text
            BtnAddCamera.Visibility = Visibility.Collapsed;
            Title = $"Xem Thử Camera - {cameraName}";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isPreviewOnly)
            {
                // Trigger auto connect in background
                BtnTestConnect_Click(this, new RoutedEventArgs());
                return;
            }

            CameraTypeCombo.SelectedIndex = 0;
            CameraBrandCombo.SelectedIndex = 0;
            if (ResolutionCombo != null)
            {
                ResolutionCombo.SelectedIndex = 0;
            }
            
            // Populate USB devices
            foreach (FilterInfo device in _usbDevices)
            {
                UsbDeviceCombo.Items.Add(device.Name);
            }
            
            if (UsbDeviceCombo.Items.Count > 0)
            {
                UsbDeviceCombo.SelectedIndex = 0;
            }
        }

        private void CameraTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IpConfigPanel == null || UsbConfigPanel == null) return;

            var selectedItem = CameraTypeCombo.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string tag = selectedItem.Tag?.ToString() ?? "";
                if (tag == "IP")
                {
                    IpConfigPanel.Visibility = Visibility.Visible;
                    UsbConfigPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    IpConfigPanel.Visibility = Visibility.Collapsed;
                    UsbConfigPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private async void BtnTestConnect_Click(object sender, RoutedEventArgs e)
        {
            string camName = CameraNameBox.Text.Trim();
            if (string.IsNullOrEmpty(camName))
            {
                MessageBox.Show("Vui lòng nhập tên camera.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var typeItem = CameraTypeCombo.SelectedItem as ComboBoxItem;
            string type = typeItem?.Tag?.ToString() ?? "IP";
            string url = "";

            if (type == "IP")
            {
                url = RtspUrlBox.Text.Trim();
                if (string.IsNullOrEmpty(url) || url == "rtsp://")
                {
                    MessageBox.Show("Vui lòng nhập địa chỉ RTSP URL hợp lệ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                if (UsbDeviceCombo.SelectedItem == null)
                {
                    MessageBox.Show("Không tìm thấy thiết bị USB để kết nối.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                url = UsbDeviceCombo.SelectedItem.ToString();
            }

            // Disable controls during connection test
            CameraNameBox.IsEnabled = false;
            CameraTypeCombo.IsEnabled = false;
            if (IpAddrBox != null) IpAddrBox.IsEnabled = false;
            if (RtspPortBox != null) RtspPortBox.IsEnabled = false;
            if (OnvifPortBox != null) OnvifPortBox.IsEnabled = false;
            if (CameraBrandCombo != null) CameraBrandCombo.IsEnabled = false;
            if (UserBox != null) UserBox.IsEnabled = false;
            if (PasswordBox != null) PasswordBox.IsEnabled = false;
            if (ResolutionCombo != null) ResolutionCombo.IsEnabled = false;
            RtspUrlBox.IsEnabled = false;
            UsbDeviceCombo.IsEnabled = false;
            BtnTestConnect.IsEnabled = false;
            BtnStopConnect.IsEnabled = true;
            BtnAddCamera.IsEnabled = false;
            NoStreamOverlay.Visibility = Visibility.Collapsed;
            StatusText.Text = "⏳ Đang kết nối tới camera...";
            StatusText.Foreground = System.Windows.Media.Brushes.Orange;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _isConnectedSuccessfully = false;

            try
            {
                await Task.Run(() =>
                {
                    VideoCapture capture;
                    if (url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        capture = new VideoCapture(url, VideoCaptureAPIs.FFMPEG);
                    }
                    else if (int.TryParse(url, out int index))
                    {
                        capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
                    }
                    else
                    {
                        int foundIndex = -1;
                        for (int i = 0; i < _usbDevices.Count; i++)
                        {
                            if (_usbDevices[i].Name.Equals(url, StringComparison.OrdinalIgnoreCase))
                            {
                                foundIndex = i;
                                break;
                            }
                        }

                        if (foundIndex >= 0)
                        {
                            capture = new VideoCapture(foundIndex, VideoCaptureAPIs.DSHOW);
                        }
                        else
                        {
                            capture = new VideoCapture(url);
                        }
                    }

                    using (capture)
                    {
                        if (!capture.IsOpened())
                        {
                            Dispatcher.Invoke(() =>
                            {
                                StopPreviewUI("❌ Kết nối thất bại. Vui lòng kiểm tra lại cấu hình.");
                            });
                            return;
                        }

                        _isConnectedSuccessfully = true;
                        Dispatcher.Invoke(() =>
                        {
                            StatusText.Text = "✅ Đã kết nối thành công! Nhấn 'Lưu' để hoàn tất.";
                            StatusText.Foreground = System.Windows.Media.Brushes.Green;
                            BtnAddCamera.IsEnabled = true;
                        });

                        using (var mat = new Mat())
                        {
                            while (!token.IsCancellationRequested)
                            {
                                if (capture.Read(mat) && !mat.Empty())
                                {
                                    var bitmap = BitmapConverter.ToBitmap(mat);
                                    Dispatcher.Invoke(() =>
                                    {
                                        PreviewImage.Source = ConvertBitmap(bitmap);
                                        bitmap.Dispose();
                                    });
                                }
                                Thread.Sleep(33);
                            }
                        }
                    }
                }, token);
            }
            catch (Exception ex)
            {
                StopPreviewUI($"❌ Lỗi: {ex.Message}");
            }
        }

        private void BtnStopConnect_Click(object sender, RoutedEventArgs e)
        {
            StopPreviewUI("⏹️ Đã dừng preview.");
        }

        private void StopPreviewUI(string message)
        {
            _cts?.Cancel();
            _cts = null;

            PreviewImage.Source = null;
            NoStreamOverlay.Visibility = Visibility.Visible;
            
            if (!_isPreviewOnly)
            {
                CameraNameBox.IsEnabled = true;
                CameraTypeCombo.IsEnabled = true;
                if (IpAddrBox != null) IpAddrBox.IsEnabled = true;
                if (RtspPortBox != null) RtspPortBox.IsEnabled = true;
                if (OnvifPortBox != null) OnvifPortBox.IsEnabled = true;
                if (CameraBrandCombo != null) CameraBrandCombo.IsEnabled = true;
                if (UserBox != null) UserBox.IsEnabled = true;
                if (PasswordBox != null) PasswordBox.IsEnabled = true;
                if (ResolutionCombo != null) ResolutionCombo.IsEnabled = true;
                RtspUrlBox.IsEnabled = true;
                UsbDeviceCombo.IsEnabled = true;
                BtnTestConnect.IsEnabled = true;
            }
            BtnStopConnect.IsEnabled = false;

            StatusText.Text = message;
            StatusText.Foreground = message.StartsWith("✅") ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        private static BitmapSource? ConvertBitmap(Bitmap bitmap)
        {
            if (bitmap == null) return null;

            try
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                System.Windows.Media.PixelFormat wpfFormat;
                switch (bitmap.PixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        wpfFormat = PixelFormats.Bgr24;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                    case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                        wpfFormat = PixelFormats.Bgr32;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                        wpfFormat = PixelFormats.Gray8;
                        break;
                    default:
                        wpfFormat = PixelFormats.Bgr24;
                        break;
                }

                var bitmapSource = BitmapSource.Create(
                    bitmapData.Width, bitmapData.Height,
                    bitmap.HorizontalResolution, bitmap.VerticalResolution,
                    wpfFormat,
                    null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride);

                bitmap.UnlockBits(bitmapData);
                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }

        private bool _isUpdatingUrl = false;

        private void RebuildRtspUrl()
        {
            if (_isUpdatingUrl || CameraBrandCombo == null || RtspUrlBox == null) return;

            var brandItem = CameraBrandCombo.SelectedItem as ComboBoxItem;
            string brand = brandItem?.Tag?.ToString() ?? "XM";

            if (brand == "CUSTOM")
            {
                return;
            }

            _isUpdatingUrl = true;

            string ip = IpAddrBox?.Text?.Trim() ?? "";
            string port = RtspPortBox?.Text?.Trim() ?? "554";
            string user = UserBox?.Text?.Trim() ?? "admin";
            string pass = PasswordBox?.Text?.Trim() ?? "";

            string url = "";
            if (brand == "XM")
            {
                url = $"rtsp://{ip}:{port}/user={user}&password={pass}&channel=0&stream=0.sdp?real_stream";
            }
            else if (brand == "HIK")
            {
                string auth = string.IsNullOrEmpty(pass) ? user : $"{user}:{pass}";
                url = $"rtsp://{auth}@{ip}:{port}/Streaming/Channels/101";
            }
            else if (brand == "DAHUA")
            {
                string auth = string.IsNullOrEmpty(pass) ? user : $"{user}:{pass}";
                url = $"rtsp://{auth}@{ip}:{port}/cam/realmonitor?channel=1&subtype=0";
            }

            RtspUrlBox.Text = url;
            _isUpdatingUrl = false;
        }

        private void IpConfigField_TextChanged(object sender, TextChangedEventArgs e)
        {
            RebuildRtspUrl();
        }

        private void CameraBrandCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RtspUrlBox == null) return;
            var brandItem = CameraBrandCombo?.SelectedItem as ComboBoxItem;
            string brand = brandItem?.Tag?.ToString() ?? "XM";

            RtspUrlBox.IsReadOnly = brand != "CUSTOM";
            RebuildRtspUrl();
        }

        private async void AddCamera_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnectedSuccessfully)
            {
                MessageBox.Show("Vui lòng chạy thử camera và xác nhận kết nối trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string camName = CameraNameBox.Text.Trim();
            var typeItem = CameraTypeCombo.SelectedItem as ComboBoxItem;
            string type = typeItem?.Tag?.ToString() ?? "IP";
            string url = type == "IP" ? RtspUrlBox.Text.Trim() : UsbDeviceCombo.SelectedItem.ToString();

            // Extract IP from RTSP URL if possible
            string ipAddress = "";
            if (type == "IP")
            {
                try
                {
                    var uri = new Uri(url);
                    ipAddress = uri.Host;
                }
                catch
                {
                    int atIndex = url.IndexOf('@');
                    int startIndex = atIndex >= 0 ? atIndex + 1 : url.IndexOf("//") + 2;
                    int endIndex = url.IndexOf(':', startIndex);
                    if (endIndex < 0) endIndex = url.IndexOf('/', startIndex);
                    if (endIndex < 0) endIndex = url.Length;
                    if (startIndex >= 0 && startIndex < url.Length && endIndex > startIndex)
                    {
                        ipAddress = url[startIndex..endIndex];
                    }
                }
            }

            int? resWidth = null;
            int? resHeight = null;
            if (ResolutionCombo != null && ResolutionCombo.SelectedItem is ComboBoxItem selectedResItem)
            {
                string tag = selectedResItem.Tag?.ToString() ?? "";
                if (!string.IsNullOrEmpty(tag) && tag.Contains('x'))
                {
                    var parts = tag.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                    {
                        resWidth = w;
                        resHeight = h;
                    }
                }
            }

            var camera = new QuanLyGiuXe.Models.CameraConfig
            {
                CameraName = camName,
                CameraKey = $"Cam_{Guid.NewGuid().ToString("N")[..8]}",
                IpAddress = ipAddress,
                RtspUrl = url,
                LaneId = _targetLaneId,
                Direction = _targetCameraRole == "ToanCanh" ? "IN" : "OUT",
                IsActive = true,
                ResolutionWidth = resWidth,
                ResolutionHeight = resHeight,
                CreatedUtc = DateTime.UtcNow
            };

            bool success = await ParkingTopologyService.Instance.SaveCameraAsync(camera);
            if (success)
            {
                NewCamera = camera;
                _cts?.Cancel();
                _cts = null;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Không thể lưu camera vào cơ sở dữ liệu. Vui lòng kiểm tra kết nối CSDL.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts = null;
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            _cts = null;
            base.OnClosed(e);
        }
    }
}
