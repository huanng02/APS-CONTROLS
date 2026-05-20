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
        // Quản lý Token và trạng thái
        private readonly Dictionary<string, CancellationTokenSource> _ipCameraTokens = new();
        private readonly Dictionary<string, string> _cameraUrls = new();
        private readonly Dictionary<string, bool> _isCameraConnected = new();

        // Sự kiện gửi ảnh về UI
        public event EventHandler<(string CamKey, Bitmap Frame)>? NewFrameReceived;

        public void Initialize()
        {
            // Không cần khởi tạo AForge nữa
        }

        public void StartIpCamera(string camKey, string url)
        {
            _cameraUrls[camKey] = url;
            _isCameraConnected[camKey] = false;
            
            StopIpCamera(camKey); // Dừng nếu camera này đang chạy

            var cts = new CancellationTokenSource();
            _ipCameraTokens[camKey] = cts;

            Task.Run(async () =>
            {
                await ErrorHandling.SafeExecutionService.SafeExecuteAsync(async () => 
                {
                    using var capture = new VideoCapture(url, VideoCaptureAPIs.FFMPEG);

                    if (!capture.IsOpened())
                    {
                        _isCameraConnected[camKey] = false;
                        System.Diagnostics.Debug.WriteLine($"Lỗi: Không kết nối được RTSP {camKey}");
                        return;
                    }

                    _isCameraConnected[camKey] = true;
                    using var mat = new Mat();
                    int failCount = 0;

                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (capture.Read(mat) && !mat.Empty())
                        {
                            failCount = 0;
                            _isCameraConnected[camKey] = true;
                            Bitmap bitmap = BitmapConverter.ToBitmap(mat);
                            NewFrameReceived?.Invoke(this, (camKey, bitmap));
                        }
                        else
                        {
                            failCount++;
                            if (failCount > 10) // Mất kết nối quá 10 frame liên tiếp
                            {
                                _isCameraConnected[camKey] = false;
                            }
                        }
                        
                        await Task.Delay(30, cts.Token);
                    }
                }, 
                source: $"CameraService.{camKey}",
                friendlyMessage: null);
            }, cts.Token);
        }

        public bool IsConnected(string camKey)
        {
            return _isCameraConnected.TryGetValue(camKey, out var connected) && connected;
        }

        public string GetUrl(string camKey)
        {
            return _cameraUrls.TryGetValue(camKey, out var url) ? url : null;
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