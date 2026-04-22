using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace QuanLyGiuXe.Services
{
    public class CameraService : IDisposable
    {
        // Dictionary quản lý Token để tắt camera sạch sẽ
        private readonly Dictionary<string, CancellationTokenSource> _ipCameraTokens = new();

        // Sự kiện gửi ảnh về UI
        public event EventHandler<(string CamKey, Bitmap Frame)>? NewFrameReceived;

        public void Initialize()
        {
            // Không cần khởi tạo AForge nữa
        }

        public void StartIpCamera(string camKey, string url)
        {
            StopIpCamera(camKey); // Dừng nếu camera này đang chạy

            var cts = new CancellationTokenSource();
            _ipCameraTokens[camKey] = cts;

            Task.Run(() =>
            {
                try
                {
                    // Sử dụng FFMPEG cho RTSP
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
                            // Chuyển đổi Mat sang Bitmap bằng cách gọi trực tiếp để tránh lỗi Extension
                            Bitmap bitmap = BitmapConverter.ToBitmap(mat);

                            // Gửi ảnh về sự kiện
                            NewFrameReceived?.Invoke(this, (camKey, bitmap));
                        }
                        // Nghỉ một chút để giảm tải CPU (khoảng 30fps)
                        Thread.Sleep(5);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi luồng {camKey}: {ex.Message}");
                }
            }, cts.Token);
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