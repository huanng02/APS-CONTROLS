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
using OpenCvSharp;
using OpenCvSharp.Extensions;
using AForge.Video.DirectShow;
using QuanLyGiuXe.ViewModels;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class CameraSettingsWindow : Window
    {
        private readonly CameraManagementViewModel _viewModel;
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _previewCts;
        private string? _activeStreamKey;
        private bool _isTestingConnection = false;

        public CameraSettingsWindow() : this(null, null)
        {
        }

        public CameraSettingsWindow(int? defaultLaneId = null, string? defaultRole = null)
        {
            InitializeComponent();
            _viewModel = new CameraManagementViewModel(defaultLaneId, defaultRole);
            DataContext = _viewModel;

            // Subscribe to live frame events from background streams
            CameraService.Instance.NewFrameReceived += OnCameraNewFrameReceived;

            // Trigger preview state reload on selected camera changes
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.SelectedCamera))
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdatePreviewState();
                    });
                }
            };

            Closed += (s, e) =>
            {
                // Clean up streaming event handlers and cancel test/preview threads
                CameraService.Instance.NewFrameReceived -= OnCameraNewFrameReceived;
                StopConnectionTest();
                StopAutoPreview();

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.ReloadCameras();
                }
            };

            // Start preview for the initial state if loaded
            Loaded += (s, e) =>
            {
                UpdatePreviewState();
            };
        }

        private void OnCameraNewFrameReceived(object? sender, (string CamKey, Bitmap Frame) data)
        {
            if (_isTestingConnection) return;

            string selectedKey = "";
            string activeKey = "";
            Dispatcher.Invoke(() =>
            {
                activeKey = _activeStreamKey ?? "";
                if (_viewModel.SelectedCamera != null)
                {
                    selectedKey = _viewModel.SelectedCamera.CameraKey;
                }
            });

            // Match if it's the database key OR the active stream key
            if ((!string.IsNullOrEmpty(selectedKey) && data.CamKey == selectedKey) ||
                (!string.IsNullOrEmpty(activeKey) && data.CamKey == activeKey))
            {
                Bitmap bmpClone;
                lock (data.Frame)
                {
                    bmpClone = (Bitmap)data.Frame.Clone();
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NoStreamOverlay.Visibility = Visibility.Collapsed;
                    PreviewImage.Source = ConvertBitmap(bmpClone);
                    bmpClone.Dispose();
                }));
            }
        }

        private string? FindActiveStreamKey(string targetUrl)
        {
            if (string.IsNullOrEmpty(targetUrl)) return null;

            var states = CameraService.Instance.GetAllRuntimeStates();
            string normTarget = NormalizeRtspUrl(targetUrl);

            foreach (var state in states)
            {
                string streamUrl = CameraService.Instance.GetUrl(state.CameraKey);
                if (!string.IsNullOrEmpty(streamUrl) && NormalizeRtspUrl(streamUrl) == normTarget)
                {
                    return state.CameraKey;
                }
            }
            return null;
        }

        private string NormalizeRtspUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                string normalized = url.ToLower().Trim();
                normalized = normalized.Replace("_password=", "password=")
                                      .Replace("_channel=", "channel=")
                                      .Replace("_stream=", "stream=")
                                      .Replace("&", "_")
                                      .Replace("?", "_")
                                      .Replace("/", "_")
                                      .Replace(":", "_");
                return normalized;
            }
            catch
            {
                return url;
            }
        }

        private void UpdatePreviewState()
        {
            StopConnectionTest();
            StopAutoPreview();
            PreviewImage.Source = null;
            _activeStreamKey = null;

            if (_viewModel.SelectedCamera == null)
            {
                NoStreamOverlay.Visibility = Visibility.Visible;
                OverlaySubText.Text = "Chọn camera hoặc nhấn 'Kiểm tra kết nối' để xem hình ảnh.";
                return;
            }

            string targetUrl = _viewModel.SelectedCamera.RtspUrl;
            string? activeKey = FindActiveStreamKey(targetUrl);

            if (activeKey != null)
            {
                _activeStreamKey = activeKey;
                NoStreamOverlay.Visibility = Visibility.Visible;
                OverlaySubText.Text = "⏳ Đang tải hình ảnh từ luồng camera hoạt động...";
            }
            else
            {
                StartAutoPreview(targetUrl);
            }
        }

        private void StartAutoPreview(string url)
        {
            StopAutoPreview();

            if (string.IsNullOrWhiteSpace(url))
            {
                NoStreamOverlay.Visibility = Visibility.Visible;
                OverlaySubText.Text = "Chọn camera hoặc nhấn 'Kiểm tra kết nối' để xem hình ảnh.";
                return;
            }

            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            NoStreamOverlay.Visibility = Visibility.Visible;
            OverlaySubText.Text = "⏳ Đang kết nối tải hình ảnh preview...";
            PreviewImage.Source = null;

            Task.Run(() =>
            {
                VideoCapture? capture = null;
                try
                {
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
                        try
                        {
                            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                            for (int i = 0; i < devices.Count; i++)
                            {
                                if (devices[i].Name.Equals(url, StringComparison.OrdinalIgnoreCase) ||
                                    devices[i].Name.Contains(url, StringComparison.OrdinalIgnoreCase))
                                {
                                    foundIndex = i;
                                    break;
                                }
                            }
                        }
                        catch { }

                        if (foundIndex >= 0)
                            capture = new VideoCapture(foundIndex, VideoCaptureAPIs.DSHOW);
                        else
                            capture = new VideoCapture(url);
                    }

                    if (capture == null || !capture.IsOpened())
                    {
                        capture?.Dispose();
                        Dispatcher.Invoke(() =>
                        {
                            if (!token.IsCancellationRequested)
                            {
                                NoStreamOverlay.Visibility = Visibility.Visible;
                                OverlaySubText.Text = "❌ Không thể kết nối preview camera.";
                            }
                        });
                        return;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            NoStreamOverlay.Visibility = Visibility.Collapsed;
                        }
                    });

                    using (capture)
                    {
                        using (var mat = new Mat())
                        {
                            while (!token.IsCancellationRequested)
                            {
                                bool readSuccess = false;
                                try
                                {
                                    readSuccess = capture.Read(mat);
                                }
                                catch
                                {
                                    readSuccess = false;
                                }

                                if (readSuccess && !mat.Empty())
                                {
                                    var bitmap = BitmapConverter.ToBitmap(mat);
                                    Dispatcher.Invoke(() =>
                                    {
                                        if (!token.IsCancellationRequested)
                                        {
                                            PreviewImage.Source = ConvertBitmap(bitmap);
                                        }
                                        bitmap.Dispose();
                                    });
                                }
                                Thread.Sleep(33);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    capture?.Dispose();
                    Dispatcher.Invoke(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            NoStreamOverlay.Visibility = Visibility.Visible;
                            OverlaySubText.Text = "❌ Lỗi preview: " + ex.Message;
                        }
                    });
                }
            }, token);
        }

        private void StopAutoPreview()
        {
            _previewCts?.Cancel();
            _previewCts = null;
        }

        private async void BtnTestConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_isTestingConnection)
            {
                UpdatePreviewState();
                return;
            }

            string url = _viewModel.RtspUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                _viewModel.TestConnectionResult = "⚠️ Vui lòng nhập RTSP URL";
                _viewModel.TestConnectionColor = "Orange";
                return;
            }

            StopAutoPreview();
            _activeStreamKey = null;

            _isTestingConnection = true;
            _viewModel.IsTestingConnection = true;
            _viewModel.TestConnectionResult = "⏳ Đang kết nối thử...";
            _viewModel.TestConnectionColor = "Orange";
            BtnTestConnect.Content = "⏹️ Dừng lại";
            
            NoStreamOverlay.Visibility = Visibility.Collapsed;
            PreviewImage.Source = null;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                await Task.Run(() =>
                {
                    VideoCapture? capture = null;
                    try
                    {
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
                            try
                            {
                                var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                                for (int i = 0; i < devices.Count; i++)
                                {
                                    if (devices[i].Name.Equals(url, StringComparison.OrdinalIgnoreCase) ||
                                        devices[i].Name.Contains(url, StringComparison.OrdinalIgnoreCase))
                                    {
                                        foundIndex = i;
                                        break;
                                    }
                                }
                            }
                            catch { }

                            if (foundIndex >= 0)
                                capture = new VideoCapture(foundIndex, VideoCaptureAPIs.DSHOW);
                            else
                                capture = new VideoCapture(url);
                        }

                        if (capture == null || !capture.IsOpened())
                        {
                            capture?.Dispose();
                            Dispatcher.Invoke(() =>
                            {
                                StopConnectionTest();
                                _viewModel.TestConnectionResult = "❌ Kết nối thất bại!";
                                _viewModel.TestConnectionColor = "Red";
                                _viewModel.IsConnectionSuccessful = false;
                            });
                            return;
                        }

                        Dispatcher.Invoke(() =>
                        {
                            _viewModel.TestConnectionResult = "✅ Kết nối thành công!";
                            _viewModel.TestConnectionColor = "Green";
                            _viewModel.IsConnectionSuccessful = true;
                        });

                        using (capture)
                        {
                            using (var mat = new Mat())
                            {
                                while (!token.IsCancellationRequested)
                                {
                                    bool readSuccess = false;
                                    try
                                    {
                                        readSuccess = capture.Read(mat);
                                    }
                                    catch
                                    {
                                        readSuccess = false;
                                    }

                                    if (readSuccess && !mat.Empty())
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
                    }
                    catch (Exception ex)
                    {
                        capture?.Dispose();
                        Dispatcher.Invoke(() =>
                        {
                            StopConnectionTest();
                            _viewModel.TestConnectionResult = "❌ Lỗi: " + ex.Message;
                            _viewModel.TestConnectionColor = "Red";
                            _viewModel.IsConnectionSuccessful = false;
                        });
                    }
                }, token);
            }
            catch (Exception ex)
            {
                StopConnectionTest();
                _viewModel.TestConnectionResult = "❌ Lỗi: " + ex.Message;
                _viewModel.TestConnectionColor = "Red";
                _viewModel.IsConnectionSuccessful = false;
            }
        }

        private void StopConnectionTest()
        {
            _cts?.Cancel();
            _cts = null;
            _isTestingConnection = false;
            _viewModel.IsTestingConnection = false;
            BtnTestConnect.Content = "🔌 Kiểm tra kết nối";
            PreviewImage.Source = null;
            NoStreamOverlay.Visibility = Visibility.Visible;
            OverlaySubText.Text = "Chọn camera hoặc nhấn 'Kiểm tra kết nối' để xem hình ảnh.";
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

                PixelFormat wpfFormat;
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
