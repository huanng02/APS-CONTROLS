using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using AForge.Video.DirectShow;


namespace QuanLyGiuXe.Services
{
    public class CameraService : IDisposable
    {
        // Quản lý Token và trạng thái
        private readonly Dictionary<string, CancellationTokenSource> _ipCameraTokens = new();
        private readonly Dictionary<string, Task> _ipCameraTasks = new();
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
            string actualKey = camKey;
            if (camKey == "VaoToanCanh" || camKey == "Vao1")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_ToanCanh";
            }
            else if (camKey == "VaoBienSo" || camKey == "Vao2")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_BienSo";
            }
            else if (camKey == "RaToanCanh" || camKey == "Ra1")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_ToanCanh";
            }
            else if (camKey == "RaBienSo" || camKey == "Ra2")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_BienSo";
            }

            _cameraUrls[actualKey] = url;
            _isCameraConnected[actualKey] = false;
            
            _ipCameraTokens.TryGetValue(actualKey, out var oldCts);
            _ipCameraTasks.TryGetValue(actualKey, out var oldTask);

            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch { }
                _ipCameraTokens.Remove(actualKey);
            }
            if (oldTask != null)
            {
                _ipCameraTasks.Remove(actualKey);
            }

            var cts = new CancellationTokenSource();
            _ipCameraTokens[camKey] = cts;

            var newTask = Task.Run(async () =>
            {
                if (oldTask != null)
                {
                    try
                    {
                        var delayTask = Task.Delay(2000);
                        var completedTask = await Task.WhenAny(oldTask, delayTask);
                        if (completedTask == delayTask)
                        {
                            System.Diagnostics.Debug.WriteLine($"Cảnh báo: Hết thời gian chờ camera {camKey} giải phóng.");
                        }
                    }
                    catch { }
                }

                await ErrorHandling.SafeExecutionService.SafeExecuteAsync(async () => 
                {
                    if (string.IsNullOrEmpty(url)) return;

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
                        // Check if it matches a USB camera name
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
                        catch
                        {
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
                            _isCameraConnected[camKey] = false;
                            System.Diagnostics.Debug.WriteLine($"Lỗi: Không kết nối được camera {camKey} (url: {url})");
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
                    }
                }, 
                source: $"CameraService.{camKey}",
                friendlyMessage: null);
            }, cts.Token);

            _ipCameraTasks[camKey] = newTask;
        }

        private static CameraService? _instance;
        public static CameraService Instance
        {
            get
            {
                if (_instance == null) _instance = new CameraService();
                return _instance;
            }
            set => _instance = value;
        }

        private int? GetLaneIdFromUiIndex(int uiIndex)
        {
            try
            {
                if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
                {
                    var mainWin = System.Windows.Application.Current.MainWindow as QuanLyGiuXe.MainWindow;
                    if (mainWin?.DataContext is ViewModels.MainViewModel vm)
                    {
                        return vm.GetDbLaneIdForUiIndex(uiIndex);
                    }
                }
                else
                {
                    int? result = null;
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var mainWin = System.Windows.Application.Current.MainWindow as QuanLyGiuXe.MainWindow;
                        if (mainWin?.DataContext is ViewModels.MainViewModel vm)
                        {
                            result = vm.GetDbLaneIdForUiIndex(uiIndex);
                        }
                    });
                    return result;
                }
            }
            catch
            {
            }
            return null;
        }

        private bool HasDynamicConfigForLane(int laneId)
        {
            try
            {
                var cfg = AppConfig.Load().Cameras;
                return cfg.LaneCameras != null && cfg.LaneCameras.Exists(lc => lc.LaneId == laneId && (!string.IsNullOrEmpty(lc.ToanCanh) || !string.IsNullOrEmpty(lc.BienSo)));
            }
            catch
            {
                return false;
            }
        }

        public bool IsConnected(string camKey)
        {
            if (_isCameraConnected.TryGetValue(camKey, out var connected) && connected)
                return true;

            string? mappedKey = null;
            if (camKey == "VaoToanCanh" || camKey == "Vao1")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                {
                    mappedKey = $"Lane_{laneId.Value}_ToanCanh";
                }
            }
            else if (camKey == "VaoBienSo" || camKey == "Vao2")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                {
                    mappedKey = $"Lane_{laneId.Value}_BienSo";
                }
            }
            else if (camKey == "RaToanCanh" || camKey == "Ra1")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                {
                    mappedKey = $"Lane_{laneId.Value}_ToanCanh";
                }
            }
            else if (camKey == "RaBienSo" || camKey == "Ra2")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                {
                    mappedKey = $"Lane_{laneId.Value}_BienSo";
                }
            }

            if (mappedKey != null && _isCameraConnected.TryGetValue(mappedKey, out connected) && connected)
                return true;

            string? fallbackKey = camKey switch
            {
                "VaoToanCanh" => "Vao1",
                "VaoBienSo" => "Vao2",
                "RaToanCanh" => "Ra1",
                "RaBienSo" => "Ra2",
                "Vao1" => "VaoToanCanh",
                "Vao2" => "VaoBienSo",
                "Ra1" => "RaToanCanh",
                "Ra2" => "RaBienSo",
                _ => null
            };

            if (fallbackKey != null && _isCameraConnected.TryGetValue(fallbackKey, out connected) && connected)
                return true;

            return false;
        }

        public string GetUrl(string camKey)
        {
            if (_cameraUrls.TryGetValue(camKey, out var url))
                return url;

            string? mappedKey = null;
            if (camKey == "VaoToanCanh" || camKey == "Vao1")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    mappedKey = $"Lane_{laneId.Value}_ToanCanh";
            }
            else if (camKey == "VaoBienSo" || camKey == "Vao2")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    mappedKey = $"Lane_{laneId.Value}_BienSo";
            }
            else if (camKey == "RaToanCanh" || camKey == "Ra1")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    mappedKey = $"Lane_{laneId.Value}_ToanCanh";
            }
            else if (camKey == "RaBienSo" || camKey == "Ra2")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    mappedKey = $"Lane_{laneId.Value}_BienSo";
            }

            if (mappedKey != null && _cameraUrls.TryGetValue(mappedKey, out url))
                return url;

            string? fallbackKey = camKey switch
            {
                "VaoToanCanh" => "Vao1",
                "VaoBienSo" => "Vao2",
                "RaToanCanh" => "Ra1",
                "RaBienSo" => "Ra2",
                "Vao1" => "VaoToanCanh",
                "Vao2" => "VaoBienSo",
                "Ra1" => "RaToanCanh",
                "Ra2" => "RaBienSo",
                _ => null
            };

            if (fallbackKey != null && _cameraUrls.TryGetValue(fallbackKey, out url))
                return url;

            return null;
        }

        public void StopIpCamera(string camKey)
        {
            if (_ipCameraTokens.TryGetValue(camKey, out var cts))
            {
                cts.Cancel();
                _ipCameraTokens.Remove(camKey);
            }
            if (_ipCameraTasks.TryGetValue(camKey, out var task))
            {
                _ipCameraTasks.Remove(camKey);
            }
        }

        public void StopAll()
        {
            foreach (var cts in _ipCameraTokens.Values) cts.Cancel();
            _ipCameraTokens.Clear();
            _ipCameraTasks.Clear();
        }

        public void Dispose() => StopAll();
    }
}