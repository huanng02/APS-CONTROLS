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
using QuanLyGiuXe.Services.Connection;

namespace QuanLyGiuXe.Services
{
    public class CameraService : IDisposable
    {
        // Quản lý Token và trạng thái
        private readonly ConcurrentDictionary<string, string> _cameraUrls = new();
        private readonly ConcurrentDictionary<string, CameraRuntimeState> _runtimeStates = new();

        // Cache database cameras to resolve Cam_xxxxxx keys to active running keys (like Lane_X_ToanCanh)
        private List<CameraEntity> _cachedDbCameras = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        public CameraService()
        {
            CameraConnectionManager.Instance.FrameReceived += (s, e) =>
            {
                NewMatFrameReceived?.Invoke(this, e);
            };
        }

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

        // Sự kiện gửi ảnh về UI (Dùng Mat để triệt tiêu GDI+)
        public event EventHandler<(string CamKey, Mat Frame)>? NewMatFrameReceived;

        public void Initialize()
        {
            // Không cần khởi tạo AForge nữa
        }

        public CameraRuntimeState GetRuntimeState(string key)
        {
            string resolvedKey = ResolveActiveKey(key);
            var state = _runtimeStates.GetOrAdd(resolvedKey, k => new CameraRuntimeState { CameraKey = k });
            
            var conn = CameraConnectionManager.Instance.GetConnectionByKey(resolvedKey);
            if (conn != null)
            {
                state.IsConnected = conn.IsConnected;
                state.ReconnectCount = conn.ReconnectCount;
                state.LastHeartbeat = conn.LastHeartbeat;
                state.LastFrameReceived = conn.LastFrameReceived;
            }
            else
            {
                state.IsConnected = false;
            }
            return state;
        }

        public List<CameraRuntimeState> GetAllRuntimeStates()
        {
            var states = new List<CameraRuntimeState>();
            var connections = CameraConnectionManager.Instance.GetConnections();
            foreach (var kvp in connections)
            {
                foreach (var consumerKey in kvp.Value.Consumers)
                {
                    var state = _runtimeStates.GetOrAdd(consumerKey, k => new CameraRuntimeState { CameraKey = k });
                    state.IsConnected = kvp.Value.IsConnected;
                    state.ReconnectCount = kvp.Value.ReconnectCount;
                    state.LastHeartbeat = kvp.Value.LastHeartbeat;
                    state.LastFrameReceived = kvp.Value.LastFrameReceived;
                    if (!states.Contains(state))
                    {
                        states.Add(state);
                    }
                }
            }
            return states;
        }

        public void StartIpCamera(string camKey, string url)
        {
            if (string.IsNullOrEmpty(url)) return;

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

            var config = AppConfig.Load().Cameras;
            int maxFps = config.MaxRenderFps;
            if (maxFps <= 0) maxFps = 10;

            int targetWidth = 640;
            int targetHeight = 480;
            if (!string.IsNullOrEmpty(config.TargetResolution))
            {
                var parts = config.TargetResolution.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                {
                    targetWidth = w;
                    targetHeight = h;
                }
            }

            CameraConnectionManager.Instance.StartStream(actualKey, url, targetWidth, targetHeight, maxFps);
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

        public Mat? GetLatestFrame(string key)
        {
            string resolvedKey = ResolveActiveKey(key);
            return CameraConnectionManager.Instance.GetLatestFrame(resolvedKey);
        }

        // Explicit lifecycle methods
        public void StartCamera(string cameraKey)
        {
            string resolvedKey = ResolveActiveKey(cameraKey);
            if (_cameraUrls.TryGetValue(resolvedKey, out var url) && !string.IsNullOrEmpty(url))
            {
                StartIpCamera(resolvedKey, url);
            }
            else
            {
                var dbCam = _cachedDbCameras.FirstOrDefault(c => c.CameraKey.Equals(resolvedKey, StringComparison.OrdinalIgnoreCase));
                if (dbCam != null && !string.IsNullOrEmpty(dbCam.RtspUrl))
                {
                    StartIpCamera(resolvedKey, dbCam.RtspUrl);
                }
            }
        }

        public void StopCamera(string cameraKey)
        {
            string resolvedKey = ResolveActiveKey(cameraKey);
            StopIpCamera(resolvedKey);
        }

        public void RestartCamera(string cameraKey)
        {
            string resolvedKey = ResolveActiveKey(cameraKey);
            StopCamera(resolvedKey);
            StartCamera(resolvedKey);
        }

        public void DisposeCamera(string cameraKey)
        {
            string resolvedKey = ResolveActiveKey(cameraKey);
            StopCamera(resolvedKey);
            _cameraUrls.TryRemove(resolvedKey, out _);
            _runtimeStates.TryRemove(resolvedKey, out _);
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

            // 1. If the key exists in our active URLs directly, return it
            if (_cameraUrls.ContainsKey(camKey))
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

            if (mappedKey != null && _cameraUrls.ContainsKey(mappedKey))
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

            if (fallbackKey != null && _cameraUrls.ContainsKey(fallbackKey))
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
            var conn = CameraConnectionManager.Instance.GetConnectionByKey(resolvedKey);
            return conn?.IsConnected ?? false;
        }

        public string GetUrl(string camKey)
        {
            string resolvedKey = ResolveActiveKey(camKey);
            if (_cameraUrls.TryGetValue(resolvedKey, out var url))
                return url;
            return "";
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

            CameraConnectionManager.Instance.StopStream(actualKey);
        }

        public void StopAll()
        {
            CameraConnectionManager.Instance.StopAll();
            _cameraUrls.Clear();
            _runtimeStates.Clear();
        }

        public void Dispose() => StopAll();
    }
}