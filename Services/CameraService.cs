using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace QuanLyGiuXe.Services
{
    public class CameraData
    {
        public string CamKey { get; set; }
        public Bitmap Frame { get; set; }
        public BitmapSource FrameForUI { get; set; }
    }
    public class CameraService : IDisposable
    {
        public string CamKey { get; set; }
        // Dictionary quản lý Token để tắt camera sạch sẽ
        private readonly Dictionary<string, CancellationTokenSource> _ipCameraTokens = new();

        // Sự kiện gửi ảnh về UI
        public event EventHandler<CameraData> NewFrameReceived;

        public void Initialize()
        {
            // Không cần khởi tạo AForge nữa
        }

        private static BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            if (bitmap == null) return null;

            try
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                // Tự động chọn định dạng WPF tương ứng với Bitmap gốc
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
                        // Nếu là định dạng lạ, ta ép về Bgr24 nhưng có thể gây sọc
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi Convert: " + ex.Message);
                return null;
            }
        }

        public void StartIpCamera(string camKey, string url)
        {
            StopIpCamera(camKey);

            var cts = new CancellationTokenSource();
            _ipCameraTokens[camKey] = cts;

            Task.Run(() =>
            {
                try
                {
                    using var capture = new VideoCapture(url, VideoCaptureAPIs.FFMPEG);

                    if (!capture.IsOpened())
                    {
                        System.Diagnostics.Debug.WriteLine($"Lỗi: Không kết nối được RTSP {camKey}");
                        return;
                    }

                    using var mat = new Mat();
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (capture.Read(mat) && !mat.Empty())
                        {
                            // 1. CHỈNH SỬA: Chuyển màu từ BGR sang RGB trước khi tạo Bitmap
                            using Mat rgbMat = new Mat();
                            Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);
                            Bitmap bitmap = BitmapConverter.ToBitmap(rgbMat);

                            // 2. Chuyển cho UI (WPF dùng RGB nên ảnh sẽ đẹp hơn)
                            var uiSource = ConvertToBitmapSource(bitmap);

                            var data = new CameraData
                            {
                                CamKey = camKey,
                                Frame = bitmap,
                                FrameForUI = uiSource
                            };

                            NewFrameReceived?.Invoke(this, data);
                        }
                        Thread.Sleep(5);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi luồng {camKey}: {ex.Message}");
                }
            }, cts.Token);
        }
        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // Lấy ảnh từ eventArgs
                Bitmap bmp = (Bitmap)eventArgs.Frame.Clone();

                // 3. Đóng gói vào class CameraData (KHÔNG dùng dấu ngoặc đơn kiểu Tuple)
                var data = new CameraData
                {
                    CamKey = this.CamKey, // Sử dụng Property đã khai báo ở bước 1
                    Frame = bmp,
                    FrameForUI = ConvertToBitmapSource(bmp) // Hàm convert bạn đã chuyển sang
                };

                // 4. Phát sự kiện
                NewFrameReceived?.Invoke(this, data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi CameraService: " + ex.Message);
            }
        }
        public void StopIpCamera(string camKey)
        {
            if (_ipCameraTokens.TryGetValue(camKey, out var cts))
            {
                cts.Cancel();
                _ipCameraTokens.Remove(camKey);
            }
        }

        public void StopAll()
        {
            foreach (var cts in _ipCameraTokens.Values) cts.Cancel();
            _ipCameraTokens.Clear();
        }

        public void Dispose() => StopAll();
    }
}