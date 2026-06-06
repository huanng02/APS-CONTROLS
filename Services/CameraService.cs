using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using AForge.Video.DirectShow;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class CameraService : IDisposable
    {
        // Quản lý Token và trạng thái
        private readonly Dictionary<string, CancellationTokenSource> _ipCameraTokens = new();
        private readonly Dictionary<string, Task> _ipCameraTasks = new();
        private readonly Dictionary<string, string> _cameraUrls = new();
        private readonly Dictionary<string, bool> _isCameraConnected = new();
        private readonly ConcurrentDictionary<string, CameraRuntimeState> _runtimeStates = new();

        // Cache database cameras to resolve Cam_xxxxxx keys to active running keys (like Lane_X_ToanCanh)
        private List<CameraEntity> _cachedDbCameras = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        private async Task EnsureCacheLoadedAsync()
        {
            if (DateTime.UtcNow - _lastCacheUpdate < TimeSpan.FromSeconds(5))
                return;

            await _cacheLock.WaitAsync();
            try
            {
                if (DateTime.UtcNow - _lastCacheUpdate < TimeSpan.FromSeconds(5))
                    return;

                var list = await CameraRepository.Instance.GetAllAsync();
                _cachedDbCameras = list ?? new List<CameraEntity>();
                _lastCacheUpdate = DateTime.UtcNow;
            }
            catch
            {
                // Silently swallow
            }
            finally
            {
                _cacheLock.Release();
            }
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
                                      .Replace(":", "_")
                                      .Replace("\\", "_");
                return normalized;
            }
            catch
            {
                return url;
            }
        }

        // Sự kiện gửi ảnh về UI
        public event EventHandler<(string CamKey, Bitmap Frame)>? NewFrameReceived;

        public void Initialize()
        {
            // Không cần khởi tạo AForge nữa
        }

        public CameraRuntimeState GetRuntimeState(string key)
        {
            string resolvedKey = ResolveActiveKey(key);
            return _runtimeStates.GetOrAdd(resolvedKey, k => new CameraRuntimeState { CameraKey = k });
        }

        public List<CameraRuntimeState> GetAllRuntimeStates()
        {
            return _runtimeStates.Values.ToList();
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
            
            var state = GetRuntimeState(actualKey);
            state.IsConnected = false;
            
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
            _ipCameraTokens[actualKey] = cts;

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
                            System.Diagnostics.Debug.WriteLine($"Cảnh báo: Hết thời gian chờ camera {actualKey} giải phóng.");
                        }
                    }
                    catch { }
                }

                await ErrorHandling.SafeExecutionService.SafeExecuteAsync(async () => 
                {
                    if (string.IsNullOrEmpty(url)) return;

                    while (!cts.Token.IsCancellationRequested)
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
                                catch { }

                                if (foundIndex >= 0)
                                {
                                    capture = new VideoCapture(foundIndex, VideoCaptureAPIs.DSHOW);
                                }
                                else
                                {
                                    capture = new VideoCapture(url);
                                }
                            }

                            if (capture == null || !capture.IsOpened())
                            {
                                capture?.Dispose();
                                state.IsConnected = false;
                                _isCameraConnected[actualKey] = false;
                                
                                await Task.Delay(5000, cts.Token);
                                state.ReconnectCount++;
                                continue;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            capture?.Dispose();
                            break;
                        }
                        catch (Exception)
                        {
                            capture?.Dispose();
                            state.IsConnected = false;
                            _isCameraConnected[actualKey] = false;
                            
                            await Task.Delay(5000, cts.Token);
                            state.ReconnectCount++;
                            continue;
                        }

                        state.IsConnected = true;
                        _isCameraConnected[actualKey] = true;
                        state.LastHeartbeat = DateTime.UtcNow;

                        using (capture)
                        {
                            using var mat = new Mat();
                            int failCount = 0;

                            while (!cts.Token.IsCancellationRequested)
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
                                    failCount = 0;
                                    state.IsConnected = true;
                                    _isCameraConnected[actualKey] = true;
                                    state.LastHeartbeat = DateTime.UtcNow;
                                    state.LastFrameReceived = DateTime.UtcNow;
                                    
                                    Bitmap bitmap = BitmapConverter.ToBitmap(mat);
                                    NewFrameReceived?.Invoke(this, (actualKey, bitmap));
                                }
                                else
                                {
                                    failCount++;
                                    if (failCount >= 10) // Mất kết nối quá 10 frame liên tiếp
                                    {
                                        state.IsConnected = false;
                                        _isCameraConnected[actualKey] = false;
                                        break; // break read loop to reconnect
                                    }
                                }
                                
                                await Task.Delay(30, cts.Token);
                            }
                        }

                        if (!cts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(5000, cts.Token);
                            state.ReconnectCount++;
                        }
                    }
                }, 
                source: $"CameraService.{actualKey}",
                friendlyMessage: null);
            }, cts.Token);

            _ipCameraTasks[actualKey] = newTask;
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

        public async Task<bool> IsIpAddressUniqueAsync(string ipAddress, int? currentCameraId = null)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return true; // USB/empty IP does not require validation
            
            try
            {
                var allCameras = await CameraRepository.Instance.GetAllAsync();
                return !allCameras.Any(c => c.Id != currentCameraId && 
                                            !string.IsNullOrEmpty(c.IpAddress) && 
                                            c.IpAddress.Trim().Equals(ipAddress.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return true; // fallback on error (db will still catch it)
            }
        }

        public string GetCameraFriendlyName(string camKey)
        {
            if (string.IsNullOrEmpty(camKey)) return "Camera";

            // 1. Trigger background load of DB cameras if cache is stale (non-blocking)
            if (DateTime.UtcNow - _lastCacheUpdate > TimeSpan.FromSeconds(5))
            {
                _ = Task.Run(() => EnsureCacheLoadedAsync());
            }

            // 2. If it's a database key directly (Cam_xxxxxx), find it in cache
            var dbCam = _cachedDbCameras.FirstOrDefault(c => c.CameraKey.Equals(camKey, StringComparison.OrdinalIgnoreCase));
            if (dbCam != null)
            {
                return dbCam.CameraName;
            }

            // 3. If it's an active key (like Lane_3_ToanCanh), find its URL, then match in cache
            if (_cameraUrls.TryGetValue(camKey, out var url) && !string.IsNullOrEmpty(url))
            {
                string normUrl = NormalizeRtspUrl(url);
                var matchedCam = _cachedDbCameras.FirstOrDefault(c => !string.IsNullOrEmpty(c.RtspUrl) && NormalizeRtspUrl(c.RtspUrl) == normUrl);
                if (matchedCam != null)
                {
                    return matchedCam.CameraName;
                }
            }

            // 4. Fallback to readable slot name
            if (camKey.StartsWith("Lane_"))
            {
                var parts = camKey.Split('_');
                if (parts.Length >= 3)
                {
                    string laneId = parts[1];
                    string type = parts[2] == "ToanCanh" ? "Toàn Cảnh" : "Biển Số";
                    return $"Camera {type} Làn {laneId}";
                }
            }

            return camKey switch
            {
                "VaoToanCanh" => "Camera Toàn Cảnh Làn Vào",
                "VaoBienSo" => "Camera Biển Số Làn Vào",
                "RaToanCanh" => "Camera Toàn Cảnh Làn Ra",
                "RaBienSo" => "Camera Biển Số Làn Ra",
                "Vao1" => "Camera Toàn Cảnh Làn Vào",
                "Vao2" => "Camera Biển Số Làn Vào",
                "Ra1" => "Camera Toàn Cảnh Làn Ra",
                "Ra2" => "Camera Biển Số Làn Ra",
                _ => $"Camera {camKey}"
            };
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

        private string ResolveActiveKey(string camKey)
        {
            if (string.IsNullOrEmpty(camKey)) return camKey;

            // 1. If the key exists in our active dictionaries directly, return it
            if (_isCameraConnected.ContainsKey(camKey))
                return camKey;

            // 2. Check fallback mappings (legacy keys)
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

            if (mappedKey != null && _isCameraConnected.ContainsKey(mappedKey))
                return mappedKey;

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

            if (fallbackKey != null && _isCameraConnected.ContainsKey(fallbackKey))
                return fallbackKey;

            // 3. Trigger background load of DB cameras if cache is stale (non-blocking)
            if (DateTime.UtcNow - _lastCacheUpdate > TimeSpan.FromSeconds(5))
            {
                _ = Task.Run(() => EnsureCacheLoadedAsync());
            }

            // 4. Match by URL from cached DB cameras
            var dbCam = _cachedDbCameras.FirstOrDefault(c => c.CameraKey.Equals(camKey, StringComparison.OrdinalIgnoreCase));
            if (dbCam != null && !string.IsNullOrEmpty(dbCam.RtspUrl))
            {
                string normUrl = NormalizeRtspUrl(dbCam.RtspUrl);
                foreach (var activeKey in _cameraUrls.Keys.ToList())
                {
                    if (_cameraUrls.TryGetValue(activeKey, out var activeUrl) && !string.IsNullOrEmpty(activeUrl))
                    {
                        if (NormalizeRtspUrl(activeUrl) == normUrl)
                        {
                            return activeKey;
                        }
                    }
                }
            }

            return camKey;
        }

        public bool IsConnected(string camKey)
        {
            string resolvedKey = ResolveActiveKey(camKey);
            if (_isCameraConnected.TryGetValue(resolvedKey, out var connected) && connected)
                return true;
            return false;
        }

        public string GetUrl(string camKey)
        {
            string resolvedKey = ResolveActiveKey(camKey);
            if (_cameraUrls.TryGetValue(resolvedKey, out var url))
                return url;
            return null;
        }

        public void StopIpCamera(string camKey)
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

            if (_ipCameraTokens.TryGetValue(actualKey, out var cts))
            {
                cts.Cancel();
                _ipCameraTokens.Remove(actualKey);
            }
            if (_ipCameraTasks.TryGetValue(actualKey, out var task))
            {
                _ipCameraTasks.Remove(actualKey);
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