using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public sealed class EventBus
    {
        private static readonly Lazy<EventBus> _lazy = new(() => new EventBus());
        public static EventBus Instance => _lazy.Value;

        private readonly List<Action<RealtimeEvent>> _subscribers = new();
        private readonly object _lock = new();

        // High-Performance In-Memory Cache for Enterprise Topology
        private List<LaneConfig>? _cachedLanes;
        private List<ParkingGate>? _cachedGates;
        private List<ParkingZone>? _cachedZones;
        private List<ParkingSite>? _cachedSites;
        private DateTime _lastCacheLoad = DateTime.MinValue;
        private readonly object _cacheLock = new();

        private EventBus()
        {
            // Initial background cache load (non-blocking)
            EnsureTopologyCache();

            // Initialize global hooking of existing services to feed events automatically
            InitializeHooks();
        }

        public void Subscribe(Action<RealtimeEvent> subscriber)
        {
            if (subscriber == null) return;
            lock (_lock)
            {
                if (!_subscribers.Contains(subscriber))
                {
                    _subscribers.Add(subscriber);
                }
            }
        }

        public void Unsubscribe(Action<RealtimeEvent> subscriber)
        {
            if (subscriber == null) return;
            lock (_lock)
            {
                _subscribers.Remove(subscriber);
            }
        }

        public void Publish(RealtimeEvent ev)
        {
            if (ev == null) return;

            Action<RealtimeEvent>[] targets;
            lock (_lock)
            {
                targets = _subscribers.ToArray();
            }

            // Dispatch to all subscribers asynchronously on the threadpool to avoid blocking the publisher
            Task.Run(() =>
            {
                foreach (var target in targets)
                {
                    try
                    {
                        target.Invoke(ev);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ [EventBus] Subscriber error: {ex.Message}");
                    }
                }
            });
        }

        private void InitializeHooks()
        {
            // Hook 1: LoggingService emits all logs (login, logout, system warnings, database backup/restore)
            LoggingService.Instance.LogEmitted += OnLogEmitted;

            // Hook 2: RFID COM3 Reader Scanned Cards
            RFIDService.Instance.OnCardScanned += OnRfidServiceCardScanned;

            // Hook 3: Network ZKTeco controller online/offline changes
            C3200Service.Instance.OnConnectionChanged += OnC3200ConnectionChanged;
        }

        private void EnsureTopologyCache()
        {
            lock (_cacheLock)
            {
                // Reload cache only if expired (5 minutes cache expiration) or not yet loaded
                if (_cachedLanes != null && (DateTime.Now - _lastCacheLoad).TotalMinutes < 5)
                {
                    return;
                }

                // Asynchronously fetch topology from database on threadpool so it NEVER blocks the main thread
                Task.Run(async () =>
                {
                    try
                    {
                        var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                        var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                        var zones = await ParkingTopologyService.Instance.GetZonesAsync();
                        var sites = await ParkingTopologyService.Instance.GetSitesAsync();

                        lock (_cacheLock)
                        {
                            _cachedLanes = lanes;
                            _cachedGates = gates;
                            _cachedZones = zones;
                            _cachedSites = sites;
                            _lastCacheLoad = DateTime.Now;
                        }
                        System.Diagnostics.Debug.WriteLine("✅ [EventBus Cache] Topology in-memory cache loaded successfully.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ [EventBus Cache] Failed to load cache in background: {ex.Message}");
                    }
                });
            }
        }

        private void OnLogEmitted(LogEntry log)
        {
            if (log == null) return;
            try
            {
                var ev = new RealtimeEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = log.Timestamp.ToLocalTime(),
                    Source = log.Source ?? "System",
                    Message = log.Details ?? string.Empty,
                    EventType = log.EventType ?? "SYSTEM_INFO"
                };

                // Map string level to RealtimeEventSeverity
                ev.Severity = log.Level?.ToUpper() switch
                {
                    "INFO" => RealtimeEventSeverity.Info,
                    "SUCCESS" => RealtimeEventSeverity.Success,
                    "WARNING" => RealtimeEventSeverity.Warning,
                    "ERROR" => RealtimeEventSeverity.Error,
                    "CRITICAL" => RealtimeEventSeverity.Critical,
                    _ => RealtimeEventSeverity.Info
                };

                string actionUpper = log.EventType?.ToUpper() ?? string.Empty;
                string detailsLower = log.Details?.ToLower() ?? string.Empty;

                // User login & logout mapping
                if (actionUpper == "LOGIN_SUCCESS")
                {
                    ev.EventType = "USER_LOGIN";
                    ev.Severity = RealtimeEventSeverity.Success;
                    ev.Message = $"Người dùng '{log.Username}' đăng nhập thành công vào máy trạm.";
                }
                else if (actionUpper == "LOGIN_FAILED")
                {
                    ev.EventType = "USER_LOGIN";
                    ev.Severity = RealtimeEventSeverity.Warning;
                    ev.Message = $"Đăng nhập thất bại cho tài khoản: '{log.Username}'. Chi tiết: {log.Details}";
                }
                else if (actionUpper == "LOGOUT")
                {
                    ev.EventType = "USER_LOGOUT";
                    ev.Severity = RealtimeEventSeverity.Info;
                    ev.Message = $"Người dùng '{log.Username}' đã đăng xuất.";
                }

                // DB Backup & Restore mapping
                else if (actionUpper.Contains("BACKUP_SUCCESS") || (actionUpper.Contains("BACKUP") && detailsLower.Contains("thành công")))
                {
                    ev.EventType = "SYSTEM_WARNING";
                    ev.Severity = RealtimeEventSeverity.Success;
                    ev.Message = $"Sao lưu CSDL thành công: {log.AdditionalData ?? log.Details}";
                }
                else if (actionUpper.Contains("RESTORE_SUCCESS") || (actionUpper.Contains("RESTORE") && detailsLower.Contains("thành công")))
                {
                    ev.EventType = "SYSTEM_CRITICAL";
                    ev.Severity = RealtimeEventSeverity.Critical;
                    ev.Message = $"Khôi phục CSDL thành công: {log.AdditionalData ?? log.Details}";
                }

                // Generic warning / error categorization
                else if (ev.Severity == RealtimeEventSeverity.Warning)
                {
                    ev.EventType = "SYSTEM_WARNING";
                }
                else if (ev.Severity == RealtimeEventSeverity.Error)
                {
                    ev.EventType = "SYSTEM_ERROR";
                }
                else if (ev.Severity == RealtimeEventSeverity.Critical)
                {
                    ev.EventType = "SYSTEM_CRITICAL";
                }

                // NOC High-Value Filter Check:
                // Only push events matching core NOC operations to avoid flooding the dispatcher with debug/trace spams.
                bool isNocEvent = ev.Severity == RealtimeEventSeverity.Warning ||
                                  ev.Severity == RealtimeEventSeverity.Error ||
                                  ev.Severity == RealtimeEventSeverity.Critical ||
                                  ev.EventType == "USER_LOGIN" ||
                                  ev.EventType == "USER_LOGOUT" ||
                                  ev.EventType == "RFID_SCAN" ||
                                  ev.EventType == "VEHICLE_ENTRY" ||
                                  ev.EventType == "VEHICLE_EXIT" ||
                                  ev.EventType == "BARRIER_OPEN" ||
                                  ev.EventType == "BARRIER_CLOSE" ||
                                  ev.EventType == "CONTROLLER_ONLINE" ||
                                  ev.EventType == "CONTROLLER_OFFLINE" ||
                                  ev.EventType == "READER_ONLINE" ||
                                  ev.EventType == "READER_OFFLINE" ||
                                  ev.EventType == "CAMERA_ONLINE" ||
                                  ev.EventType == "CAMERA_OFFLINE" ||
                                  ev.EventType == "CARD_REGISTERED" ||
                                  ev.EventType == "CARD_UPDATED" ||
                                  ev.EventType == "CARD_DELETED";

                if (!isNocEvent)
                {
                    return; // Skip general developer trace logs to keep presentation 100% lag-free
                }

                // Parse site details
                ev.Site = "HQ Office";
                ev.Zone = "Zone A";
                ev.Gate = "Cổng chính";
                ev.Lane = "Làn vận hành";

                Publish(ev);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [EventBus] Log hook parse error: {ex.Message}");
            }
        }

        private void OnRfidServiceCardScanned(string uid)
        {
            try
            {
                var ev = new RealtimeEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = "RFID_SCAN",
                    Severity = RealtimeEventSeverity.Info,
                    Source = "RFIDService",
                    Message = $"RFID Scan trên cổng COM3. Thẻ UID: {uid}",
                    Site = "HQ Office",
                    Zone = "Khu đăng ký",
                    Gate = "Bàn làm việc",
                    Lane = "Đăng ký thẻ"
                };

                Publish(ev);
            }
            catch { }
        }

        private void OnC3200ConnectionChanged(bool connected)
        {
            try
            {
                var config = AppConfig.Load();
                var ev = new RealtimeEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = connected ? "CONTROLLER_ONLINE" : "CONTROLLER_OFFLINE",
                    Severity = connected ? RealtimeEventSeverity.Success : RealtimeEventSeverity.Critical,
                    Source = "C3200Service",
                    Message = connected 
                        ? $"Bộ điều khiển C3-200 ({config.ZKTeco.IpAddress}) đã khôi phục kết nối." 
                        : $"Mất kết nối với Bộ điều khiển C3-200 ({config.ZKTeco.IpAddress})! Vui lòng kiểm tra lại mạng.",
                    Site = "HQ Office",
                    Zone = "Zone B",
                    Gate = "Cổng soát vé",
                    Lane = "Tất cả làn"
                };

                Publish(ev);
            }
            catch { }
        }

        public (string Site, string Zone, string Gate, string Lane) ResolveTopologyForReader(int readerNo)
        {
            // Triggers background refresh if needed (non-blocking)
            EnsureTopologyCache();

            try
            {
                List<LaneConfig>? lanes;
                List<ParkingGate>? gates;
                List<ParkingSite>? sites;

                lock (_cacheLock)
                {
                    lanes = _cachedLanes;
                    gates = _cachedGates;
                    sites = _cachedSites;
                }

                // If cache is loaded, resolve instantly in-memory (0ms block)
                if (lanes != null)
                {
                    var mapping = ReaderLaneMappingService.Instance.GetMappingByReader(readerNo);
                    if (mapping != null && mapping.IsEnabled)
                    {
                        int laneId = mapping.LaneId;
                        var lane = lanes.FirstOrDefault(l => l.Id == laneId);
                        if (lane != null)
                        {
                            var gate = gates?.FirstOrDefault(g => g.Id == lane.GateId);
                            
                            string siteName = string.Empty;
                            if (gate != null && sites != null)
                            {
                                var site = sites.FirstOrDefault(s => s.Id == gate.SiteId);
                                if (site != null) siteName = site.SiteName;
                            }

                            return (
                                string.IsNullOrEmpty(siteName) ? "HQ Office" : siteName, 
                                lane.ZoneName ?? "Zone A", 
                                gate?.GateName ?? "Cổng chính", 
                                lane.LaneName ?? $"Làn {readerNo}"
                            );
                        }
                    }
                }
            }
            catch { }
            
            // Standard smart fallbacks based on reader conventions if cache is not yet fully loaded
            string laneNameFallback = readerNo % 2 == 1 ? "Làn Vào" : "Làn Ra";
            string gateNameFallback = readerNo <= 2 ? "Cổng số 1" : "Cổng số 2";
            return ("HQ Office", "Zone A", gateNameFallback, laneNameFallback);
        }
    }
}
