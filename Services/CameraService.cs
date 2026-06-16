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

        public void ClearCache()
        {
            _lastCacheUpdate = DateTime.MinValue;
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

            AppConfig.ClearCache();
            var config = AppConfig.Load().Cameras;
            int maxFps = config.MaxRenderFps;
            if (maxFps <= 0) maxFps = 10;

            // Synchronously ensure cache is loaded or updated
            try
            {
                Task.Run(() => EnsureCacheLoadedAsync()).GetAwaiter().GetResult();
            }
            catch { }

            // Find specific resolution for this camera from the database cache
            var dbCam = _cachedDbCameras.FirstOrDefault(c => c.CameraKey.Equals(actualKey, StringComparison.OrdinalIgnoreCase) || 
                                                            (c.LaneId.HasValue && actualKey.Equals($"Lane_{c.LaneId.Value}_ToanCanh", StringComparison.OrdinalIgnoreCase) && c.Direction == "Overview") ||
                                                            (c.LaneId.HasValue && actualKey.Equals($"Lane_{c.LaneId.Value}_BienSo", StringComparison.OrdinalIgnoreCase) && c.Direction != "Overview"));
            
            // Or fallback match by normalized URL
            if (dbCam == null && !string.IsNullOrEmpty(url))
            {
                string normUrl = NormalizeRtspUrl(url);
                dbCam = _cachedDbCameras.FirstOrDefault(c => !string.IsNullOrEmpty(c.RtspUrl) && NormalizeRtspUrl(c.RtspUrl) == normUrl);
            }

            int targetWidth = 640;
            int targetHeight = 480;

            if (dbCam != null && dbCam.ResolutionWidth.HasValue && dbCam.ResolutionHeight.HasValue)
            {
                targetWidth = dbCam.ResolutionWidth.Value;
                targetHeight = dbCam.ResolutionHeight.Value;
            }
            else
            {
                // Fallback to config.json target resolution
                if (!string.IsNullOrEmpty(config.TargetResolution))
                {
                    var parts = config.TargetResolution.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                    {
                        targetWidth = w;
                        targetHeight = h;
                    }
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
            
            // If the key is a raw DB key (Cam_xxxxxx), resolve it first to find the active stream key
            if (!string.IsNullOrEmpty(camKey) && camKey.StartsWith("Cam_", StringComparison.OrdinalIgnoreCase))
            {
                actualKey = ResolveActiveKey(camKey);
            }

            if (actualKey == "VaoToanCanh" || actualKey == "Vao1")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_ToanCanh";
            }
            else if (actualKey == "VaoBienSo" || actualKey == "Vao2")
            {
                int? laneId = GetLaneIdFromUiIndex(1);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_BienSo";
            }
            else if (actualKey == "RaToanCanh" || actualKey == "Ra1")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_ToanCanh";
            }
            else if (actualKey == "RaBienSo" || actualKey == "Ra2")
            {
                int? laneId = GetLaneIdFromUiIndex(2);
                if (laneId.HasValue && HasDynamicConfigForLane(laneId.Value))
                    actualKey = $"Lane_{laneId.Value}_BienSo";
            }

            CameraConnectionManager.Instance.StopStream(actualKey);
            _cameraUrls.TryRemove(actualKey, out _);
        }

        private void SyncConfigObject(AppConfig cfg, List<CameraEntity> dbCams)
        {
            if (cfg.Cameras == null)
            {
                cfg.Cameras = new CameraConfig();
            }

            cfg.Cameras.LaneCameras = new List<LaneCameraSetting>();

            var activeCams = dbCams.Where(c => c.IsActive && c.LaneId.HasValue).ToList();
            var dbLanes = ParkingTopologyService.Instance.GetLanes();

            var firstIn = dbLanes.FirstOrDefault(l => l.Direction?.ToUpper() == "IN");
            var firstOut = dbLanes.FirstOrDefault(l => l.Direction?.ToUpper() == "OUT");

            // Reset legacy properties to empty before updating
            cfg.Cameras.VaoToanCanh = "";
            cfg.Cameras.VaoBienSo = "";
            cfg.Cameras.RaToanCanh = "";
            cfg.Cameras.RaBienSo = "";

            foreach (var group in activeCams.GroupBy(c => c.LaneId!.Value))
            {
                int laneId = group.Key;
                var laneSetting = new LaneCameraSetting { LaneId = laneId };

                foreach (var cam in group)
                {
                    if (cam.Direction == "Overview")
                    {
                        laneSetting.ToanCanh = cam.RtspUrl;
                    }
                    else
                    {
                        laneSetting.BienSo = cam.RtspUrl;
                    }

                    // Legacy fallbacks for the first IN/OUT lanes
                    if (firstIn != null && firstIn.Id == laneId)
                    {
                        if (cam.Direction == "Overview") cfg.Cameras.VaoToanCanh = cam.RtspUrl;
                        else cfg.Cameras.VaoBienSo = cam.RtspUrl;
                    }
                    else if (firstOut != null && firstOut.Id == laneId)
                    {
                        if (cam.Direction == "Overview") cfg.Cameras.RaToanCanh = cam.RtspUrl;
                        else cfg.Cameras.RaBienSo = cam.RtspUrl;
                    }
                }

                cfg.Cameras.LaneCameras.Add(laneSetting);
            }
        }

        public async Task SyncCamerasToConfigAsync(bool force = false)
        {
            try
            {
                var cfg = AppConfig.Load();
                var draftCfg = AppConfig.LoadDraft();

                bool shouldSyncActive = force || (cfg.Cameras != null && cfg.Cameras.AutoSyncFromDb != false);
                bool shouldSyncDraft = force || (draftCfg.Cameras != null && draftCfg.Cameras.AutoSyncFromDb != false);

                if (!shouldSyncActive && !shouldSyncDraft)
                {
                    return;
                }

                var dbCams = await CameraRepository.Instance.GetAllAsync();

                if (shouldSyncActive)
                {
                    SyncConfigObject(cfg, dbCams);
                    cfg.Save();
                }

                if (shouldSyncDraft)
                {
                    SyncConfigObject(draftCfg, dbCams);
                    draftCfg.SaveDraft();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    LoggingService.Instance.LogError("SyncCamerasToConfig", "CameraService", "Lỗi đồng bộ cấu hình camera vào config.json và config_draft.json", ex);
                }
                catch { }
            }
        }

        public async Task ApplyDeployedConfigurationAsync()
        {
            // 1. Keep a copy of the current cached config (the "old" config before deployment)
            await EnsureCacheLoadedAsync();
            List<CameraEntity> oldCams;
            await _cacheLock.WaitAsync();
            try
            {
                oldCams = _cachedDbCameras.ToList();
            }
            finally
            {
                _cacheLock.Release();
            }

            // 2. Fetch the latest deployed config from database
            List<CameraEntity> newCams;
            try
            {
                newCams = await CameraRepository.Instance.GetAllAsync();
            }
            catch (Exception ex)
            {
                try { LoggingService.Instance.LogError("CameraService", "ApplyDeployedConfiguration", "Failed to reload cameras from DB", ex); } catch { }
                return;
            }

            // 3. Update the cache
            await _cacheLock.WaitAsync();
            try
            {
                _cachedDbCameras = newCams ?? new List<CameraEntity>();
                _lastCacheUpdate = DateTime.UtcNow;
            }
            finally
            {
                _cacheLock.Release();
            }

            // 4. Sync database cameras to config.json
            await SyncCamerasToConfigAsync();

            // 5. Compare old and new configs to apply changes
            var allCameraIds = oldCams.Select(c => c.Id).Union(_cachedDbCameras.Select(c => c.Id)).Distinct().ToList();

            foreach (var id in allCameraIds)
            {
                var oldCam = oldCams.FirstOrDefault(c => c.Id == id);
                var newCam = _cachedDbCameras.FirstOrDefault(c => c.Id == id);

                if (oldCam == null && newCam != null)
                {
                    // Newly added camera
                    LogConfigChange(null, newCam);
                    if (newCam.IsActive)
                    {
                        StartCameraStreamForConfig(newCam);
                    }
                }
                else if (oldCam != null && newCam == null)
                {
                    // Deleted camera
                    HandleCameraDeletedInternal(oldCam);
                }
                else if (oldCam != null && newCam != null)
                {
                    // Modified camera or unchanged camera
                    bool isChanged = oldCam.CameraName != newCam.CameraName ||
                                     oldCam.CameraKey != newCam.CameraKey ||
                                     oldCam.IpAddress != newCam.IpAddress ||
                                     oldCam.Port != newCam.Port ||
                                     oldCam.Protocol != newCam.Protocol ||
                                     oldCam.Username != newCam.Username ||
                                     oldCam.Password != newCam.Password ||
                                     oldCam.RtspUrl != newCam.RtspUrl ||
                                     oldCam.LaneId != newCam.LaneId ||
                                     oldCam.Direction != newCam.Direction ||
                                     oldCam.IsActive != newCam.IsActive ||
                                     oldCam.ResolutionWidth != newCam.ResolutionWidth ||
                                     oldCam.ResolutionHeight != newCam.ResolutionHeight;

                    if (isChanged)
                    {
                        LogConfigChange(oldCam, newCam);
                        ApplyConfigChangeToRunningStreams(oldCam, newCam);
                    }
                }
            }
        }

        private void StartCameraStreamForConfig(CameraEntity newCam)
        {
            var keysToStart = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            keysToStart.Add(newCam.CameraKey);
            if (newCam.LaneId.HasValue)
            {
                string newRoleKey = newCam.Direction == "Overview" ? $"Lane_{newCam.LaneId}_ToanCanh" : $"Lane_{newCam.LaneId}_BienSo";
                keysToStart.Add(newRoleKey);
            }

            foreach (var key in keysToStart)
            {
                try { LoggingService.Instance.LogInfo("CameraService", "StartCameraStreamForConfig", $"Starting stream key '{key}' for new camera"); } catch { }
                StartIpCamera(key, newCam.RtspUrl);
                var state = GetRuntimeState(key);
                try { LoggingService.Instance.LogInfo("CameraService", "StartCameraStreamForConfig", $"Reconnect status for key '{key}': Connected={state.IsConnected}, Reconnects={state.ReconnectCount}"); } catch { }
            }
        }

        public async Task HandleCameraConfigChangedAsync(int cameraId)
        {
            // 1. Get old configuration from cache before reloading
            await EnsureCacheLoadedAsync();
            CameraEntity? oldCam = null;
            await _cacheLock.WaitAsync();
            try
            {
                oldCam = _cachedDbCameras.FirstOrDefault(c => c.Id == cameraId);
            }
            finally
            {
                _cacheLock.Release();
            }

            // 2. Fetch the latest from database to update cache
            List<CameraEntity> dbCams;
            try
            {
                dbCams = await CameraRepository.Instance.GetAllAsync();
            }
            catch (Exception ex)
            {
                try { LoggingService.Instance.LogError("CameraService", "HandleCameraConfigChanged", "Failed to reload cameras from DB", ex); } catch { }
                return;
            }

            // 3. Update cache
            await _cacheLock.WaitAsync();
            try
            {
                _cachedDbCameras = dbCams ?? new List<CameraEntity>();
                _lastCacheUpdate = DateTime.UtcNow;
            }
            finally
            {
                _cacheLock.Release();
            }

            // 4. Find new configuration
            CameraEntity? newCam = null;
            await _cacheLock.WaitAsync();
            try
            {
                newCam = _cachedDbCameras.FirstOrDefault(c => c.Id == cameraId);
            }
            finally
            {
                _cacheLock.Release();
            }

            if (newCam == null)
            {
                // If the camera is not found, it might have been deleted
                if (oldCam != null)
                {
                    HandleCameraDeletedInternal(oldCam);
                }
                return;
            }

            // 5. Log changes
            LogConfigChange(oldCam, newCam);

            // 6. Stop and restart streams immediately
            ApplyConfigChangeToRunningStreams(oldCam, newCam);
        }

        private void LogConfigChange(CameraEntity? oldCam, CameraEntity newCam)
        {
            string oldConfigStr = oldCam != null 
                ? $"Name={oldCam.CameraName}, Key={oldCam.CameraKey}, IP={oldCam.IpAddress}, URL={oldCam.RtspUrl}, Lane={oldCam.LaneId}, Role={oldCam.Direction}, Res={oldCam.ResolutionWidth}x{oldCam.ResolutionHeight}, Active={oldCam.IsActive}"
                : "None";

            string newConfigStr = $"Name={newCam.CameraName}, Key={newCam.CameraKey}, IP={newCam.IpAddress}, URL={newCam.RtspUrl}, Lane={newCam.LaneId}, Role={newCam.Direction}, Res={newCam.ResolutionWidth}x{newCam.ResolutionHeight}, Active={newCam.IsActive}";

            try
            {
                LoggingService.Instance.LogInfo("CameraService", "CameraConfigChanged", $"Camera Config Updated.\nOld Config: {oldConfigStr}\nNew Config: {newConfigStr}");
            }
            catch { }
        }

        private void ApplyConfigChangeToRunningStreams(CameraEntity? oldCam, CameraEntity newCam)
        {
            // Collect all consumer keys (active stream keys) that were/are associated with this camera
            var keysToRestart = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (oldCam != null)
            {
                keysToRestart.Add(oldCam.CameraKey);
                if (oldCam.LaneId.HasValue)
                {
                    string oldRoleKey = oldCam.Direction == "Overview" ? $"Lane_{oldCam.LaneId}_ToanCanh" : $"Lane_{oldCam.LaneId}_BienSo";
                    keysToRestart.Add(oldRoleKey);
                }
            }

            keysToRestart.Add(newCam.CameraKey);
            if (newCam.LaneId.HasValue)
            {
                string newRoleKey = newCam.Direction == "Overview" ? $"Lane_{newCam.LaneId}_ToanCanh" : $"Lane_{newCam.LaneId}_BienSo";
                keysToRestart.Add(newRoleKey);
            }

            // Also check connections in CameraConnectionManager to see if any are using the old URL or old key
            string oldNormUrl = oldCam != null ? NormalizeRtspUrl(oldCam.RtspUrl) : "";
            string newNormUrl = NormalizeRtspUrl(newCam.RtspUrl);

            var connections = CameraConnectionManager.Instance.GetConnections();
            foreach (var kvp in connections)
            {
                string connUrlNorm = kvp.Key; // normalized URL
                var conn = kvp.Value;

                bool urlMatch = (!string.IsNullOrEmpty(oldNormUrl) && connUrlNorm == oldNormUrl) ||
                                (connUrlNorm == newNormUrl);

                if (urlMatch)
                {
                    foreach (var consumer in conn.Consumers)
                    {
                        keysToRestart.Add(consumer);
                    }
                }
                else
                {
                    // Also check if consumer list contains any of our known keys
                    foreach (var consumer in conn.Consumers)
                    {
                        if (oldCam != null && (consumer.Equals(oldCam.CameraKey, StringComparison.OrdinalIgnoreCase) || 
                                               (oldCam.LaneId.HasValue && consumer.Equals($"Lane_{oldCam.LaneId}_ToanCanh", StringComparison.OrdinalIgnoreCase) && oldCam.Direction == "Overview") ||
                                               (oldCam.LaneId.HasValue && consumer.Equals($"Lane_{oldCam.LaneId}_BienSo", StringComparison.OrdinalIgnoreCase) && oldCam.Direction != "Overview")))
                        {
                            keysToRestart.Add(consumer);
                        }
                    }
                }
            }

            // For all found keys, if they are currently streaming:
            // 1. Stop the current stream.
            // 2. If the camera is active, start the stream again (which will apply the new URL, resolution, etc.).
            foreach (var key in keysToRestart)
            {
                bool isCurrentlyStreaming = CameraConnectionManager.Instance.GetConnectionByKey(key) != null;
                if (isCurrentlyStreaming)
                {
                    try { LoggingService.Instance.LogInfo("CameraService", "ApplyConfigChange", $"Stopping running stream key '{key}' for config update"); } catch { }
                    StopIpCamera(key);
                    
                    if (newCam.IsActive)
                    {
                        try { LoggingService.Instance.LogInfo("CameraService", "ApplyConfigChange", $"Restarting stream key '{key}' with new configuration"); } catch { }
                        
                        StartIpCamera(key, newCam.RtspUrl);
                        
                        var state = GetRuntimeState(key);
                        try { LoggingService.Instance.LogInfo("CameraService", "ApplyConfigChange", $"Reconnect status for key '{key}': Connected={state.IsConnected}, Reconnects={state.ReconnectCount}"); } catch { }
                    }
                }
            }
        }

        private void HandleCameraDeletedInternal(CameraEntity oldCam)
        {
            try { LoggingService.Instance.LogInfo("CameraService", "CameraDeleted", $"Camera deleted: Name={oldCam.CameraName}, Key={oldCam.CameraKey}"); } catch { }
            
            var keysToStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            keysToStop.Add(oldCam.CameraKey);
            if (oldCam.LaneId.HasValue)
            {
                string oldRoleKey = oldCam.Direction == "Overview" ? $"Lane_{oldCam.LaneId}_ToanCanh" : $"Lane_{oldCam.LaneId}_BienSo";
                keysToStop.Add(oldRoleKey);
            }

            string oldNormUrl = NormalizeRtspUrl(oldCam.RtspUrl);
            var connections = CameraConnectionManager.Instance.GetConnections();
            foreach (var kvp in connections)
            {
                if (kvp.Key == oldNormUrl)
                {
                    foreach (var consumer in kvp.Value.Consumers)
                    {
                        keysToStop.Add(consumer);
                    }
                }
            }

            foreach (var key in keysToStop)
            {
                try { LoggingService.Instance.LogInfo("CameraService", "ApplyConfigChange", $"Stopping running stream key '{key}' due to camera deletion"); } catch { }
                StopIpCamera(key);
            }
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