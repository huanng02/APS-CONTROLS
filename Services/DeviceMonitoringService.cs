using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class DeviceMonitoringService
    {
        private static readonly Lazy<DeviceMonitoringService> _lazy =
            new Lazy<DeviceMonitoringService>(() => new DeviceMonitoringService());

        public static DeviceMonitoringService Instance => _lazy.Value;

        private readonly Dictionary<string, bool> _previousDeviceStates = new();
        private readonly object _stateLock = new();

        private DeviceMonitoringService() { }

        // ── C3 Controllers Status ───────────────────────────────────────────
        public async Task<List<C3ControllerStatusDto>> GetC3ControllersStatusAsync()
        {
            var results = new List<C3ControllerStatusDto>();

            try
            {
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();

                // Ping in parallel
                var pingTasks = controllers.Select(async ctrl =>
                {
                    var dto = new C3ControllerStatusDto
                    {
                        Id = ctrl.Id,
                        ControllerName = ctrl.ControllerName,
                        IpAddress = ctrl.IpAddress,
                        IsActive = ctrl.IsActive,
                        GateName = gates.FirstOrDefault(g => g.Id == ctrl.GateId)?.GateName ?? "N/A"
                    };

                    if (!ctrl.IsActive)
                    {
                        dto.IsOnline = false;
                        dto.StatusDetails = "Vô hiệu hóa (Inactive)";
                        return dto;
                    }

                    var pingResult = await PingAddressAsync(ctrl.IpAddress);
                    dto.IsOnline = pingResult.Success;
                    dto.PingLatencyMs = pingResult.Latency;
                    dto.StatusDetails = pingResult.Success 
                        ? $"Đang hoạt động (Ping: {pingResult.Latency}ms)" 
                        : "Không phản hồi (Offline)";

                    return dto;
                });

                var completed = await Task.WhenAll(pingTasks);
                results.AddRange(completed);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeviceMonitoringService", "GetC3ControllersStatusAsync", "Failed to retrieve controllers status", ex);
            }

            return results;
        }

        // ── RFID Readers Status ─────────────────────────────────────────────
        public async Task<List<RfidReaderStatusDto>> GetRfidReadersStatusAsync(List<C3ControllerStatusDto> controllerStatuses)
        {
            var results = new List<RfidReaderStatusDto>();

            try
            {
                // Load Reader-Lane mappings
                var mappings = ReaderLaneMappingService.Instance.GetAll();
                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();

                // 1. Check COM Port reader (USB RFID Reader)
                bool usbReaderOnline = false;
                string usbDetails = "Mất kết nối";
                try
                {
                    var field = typeof(RFIDService).GetField("port", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var port = field?.GetValue(RFIDService.Instance) as System.IO.Ports.SerialPort;
                    if (port != null && port.IsOpen)
                    {
                        usbReaderOnline = true;
                        usbDetails = "Kết nối COM3 (Sẵn sàng)";
                    }
                    else
                    {
                        usbDetails = "Cổng COM3 đóng hoặc chưa cắm USB";
                    }
                }
                catch (Exception comEx)
                {
                    usbDetails = $"Lỗi đọc cổng COM: {comEx.Message}";
                }

                // Add USB Reader
                results.Add(new RfidReaderStatusDto
                {
                    ReaderNo = 1,
                    ReaderName = "Đầu đọc RFID USB đăng ký thẻ (COM3)",
                    ConnectionType = "USB COM Port",
                    PortOrAddress = "COM3",
                    AssociatedLaneName = "Cổng/Bàn Đăng ký",
                    IsOnline = usbReaderOnline,
                    StatusDetails = usbDetails
                });

                // 2. Add C3 Controller Readers
                var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var controllers = allControllers.Where(c => c.IsActive).ToList();
                foreach (var ctrl in controllers)
                {
                    var ctrlStatus = controllerStatuses.FirstOrDefault(c => c.Id == ctrl.Id);
                    bool ctrlOnline = ctrlStatus?.IsOnline ?? false;

                    int readerCount = 4;
                    for (int r = 1; r <= readerCount; r++)
                    {
                        int globalReaderNo = (ctrl.Id - 1) * 4 + r;
                        
                        bool isActiveController = string.Equals(ctrl.IpAddress, AppConfig.Load().ZKTeco.IpAddress, StringComparison.OrdinalIgnoreCase) 
                                               || controllers.Count == 1;

                        int lookupReaderNo = isActiveController ? r : globalReaderNo;

                        var mapping = mappings.FirstOrDefault(m => m.ReaderNo == lookupReaderNo);
                        string laneName = "Chưa cấu hình làn";
                        
                        if (mapping != null)
                        {
                            var lane = lanes.FirstOrDefault(l => l.Id == mapping.LaneId);
                            if (lane != null)
                            {
                                laneName = $"{lane.LaneName} ({mapping.Direction})";
                            }
                        }

                        string readerDisplayName;
                        if (mapping != null)
                        {
                            var lane = lanes.FirstOrDefault(l => l.Id == mapping.LaneId);
                            if (lane != null)
                            {
                                string gatePart = string.IsNullOrWhiteSpace(lane.GateName) ? string.Empty : $"Cổng: {lane.GateName} - ";
                                readerDisplayName = $"Đầu đọc Wiegand {r} ({gatePart}Làn: {lane.LaneName})";
                            }
                            else
                            {
                                readerDisplayName = $"Đầu đọc Wiegand {r} (Tủ: {ctrl.ControllerName})";
                            }
                        }
                        else
                        {
                            readerDisplayName = $"Đầu đọc Wiegand {r} (Tủ: {ctrl.ControllerName} - Chưa gán)";
                        }

                        bool isReaderEnabled = mapping?.IsEnabled ?? true;
                        bool isOnlineAndEnabled = ctrlOnline && isReaderEnabled;
                        string statusText = !isReaderEnabled 
                            ? "Vô hiệu hóa (Disabled)" 
                            : (ctrlOnline ? "Hoạt động (Wiegand D0/D1 OK)" : $"Mất kết nối tủ điều khiển ({ctrl.ControllerName} Offline)");

                        results.Add(new RfidReaderStatusDto
                        {
                            ReaderNo = lookupReaderNo,
                            ReaderName = readerDisplayName,
                            ConnectionType = "C3 Wiegand",
                            PortOrAddress = $"Reader Pin #{r} @ {ctrl.IpAddress}",
                            AssociatedLaneName = laneName,
                            IsOnline = isOnlineAndEnabled,
                            StatusDetails = statusText
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeviceMonitoringService", "GetRfidReadersStatusAsync", "Failed to retrieve RFID readers status", ex);
            }

            return results;
        }


        // ── Cameras Status ──────────────────────────────────────────────────
        public async Task<List<CameraStatusDto>> GetCamerasStatusAsync()
        {
            var results = new List<CameraStatusDto>();

            try
            {
                var cameras = await ParkingTopologyService.Instance.GetCamerasAsync();
                foreach (var c in cameras)
                {
                    bool isOnline = false;
                    try
                    {
                        isOnline = CameraService.Instance.IsConnected(c.CameraKey);
                    }
                    catch { }

                    string status = isOnline ? "Online" : "Offline";
                    string details = isOnline ? "Kết nối RTSP OK" : "Mất kết nối RTSP";

                    if (!c.IsActive)
                    {
                        details = "Vô hiệu hóa (Inactive)";
                        isOnline = false;
                        status = "Offline";
                    }

                    results.Add(new CameraStatusDto
                    {
                        Id = c.Id,
                        CameraName = c.CameraName,
                        LaneName = c.LaneName,
                        IpAddress = c.IpAddress,
                        Status = status,
                        IsOnline = isOnline,
                        LastFrameTime = isOnline ? DateTime.Now : null,
                        RtspUrl = c.RtspUrl,
                        StatusDetails = details
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeviceMonitoringService", "GetCamerasStatusAsync", "Failed to retrieve cameras status", ex);
            }

            return results;
        }

        // ── Lanes Status (Enhanced) ─────────────────────────────────────────
        public async Task<List<LaneStatusDto>> GetLanesStatusAsync(
            List<C3ControllerStatusDto> controllerStatuses,
            List<CameraStatusDto> cameraStatuses)
        {
            var results = new List<LaneStatusDto>();

            try
            {
                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var readerStatuses = await GetRfidReadersStatusAsync(controllerStatuses);

                foreach (var lane in lanes)
                {
                    var gate = gates.FirstOrDefault(g => g.Id == lane.GateId);
                    var controller = controllers.FirstOrDefault(c => c.GateId == lane.GateId);
                    var ctrlStatus = controller != null 
                        ? controllerStatuses.FirstOrDefault(c => c.Id == controller.Id) 
                        : null;

                    var associatedCamera = cameraStatuses.FirstOrDefault(c => c.LaneName == lane.LaneName);
                    var laneReaders = readerStatuses.Where(r => r.AssociatedLaneName != null && r.AssociatedLaneName.Contains(lane.LaneName)).ToList();

                    // Derive Statuses
                    string controllerStatusVal = ctrlStatus != null ? (ctrlStatus.IsOnline ? "Online" : "Offline") : "N/A";
                    string cameraStatusVal = associatedCamera != null ? (associatedCamera.IsOnline ? "Online" : "Offline") : "N/A";
                    
                    string readerStatusVal = "N/A";
                    if (laneReaders.Any())
                    {
                        readerStatusVal = laneReaders.Any(r => r.IsOnline) ? "Online" : "Offline";
                    }

                    var dto = new LaneStatusDto
                    {
                        Id = lane.Id,
                        LaneCode = lane.LaneCode,
                        LaneName = lane.LaneName,
                        Direction = lane.Direction,
                        IsActive = lane.IsActive,
                        GateName = gate?.GateName ?? "N/A",
                        ZoneName = lane.ZoneName ?? "N/A",
                        AssociatedCamera = associatedCamera?.CameraName ?? "N/A",
                        CameraOnline = associatedCamera?.IsOnline ?? false,
                        ControllerStatus = controllerStatusVal,
                        ReaderStatus = readerStatusVal,
                        CameraStatus = cameraStatusVal
                    };

                    if (!lane.IsActive)
                    {
                        dto.IsOnline = false;
                        dto.OverallHealth = "Unknown";
                        dto.StatusDetails = "Vô hiệu hóa (Inactive)";
                    }
                    else
                    {
                        // Health Logic
                        bool hasOfflineSubdevice = false;
                        bool isControllerOffline = false;
                        bool hasDevices = false;

                        if (controllerStatusVal != "N/A")
                        {
                            hasDevices = true;
                            if (controllerStatusVal == "Offline")
                            {
                                isControllerOffline = true;
                                hasOfflineSubdevice = true;
                            }
                        }
                        if (readerStatusVal != "N/A")
                        {
                            hasDevices = true;
                            if (readerStatusVal == "Offline") hasOfflineSubdevice = true;
                        }

                        if (cameraStatusVal != "N/A")
                        {
                            hasDevices = true;
                            if (cameraStatusVal == "Offline") hasOfflineSubdevice = true;
                        }

                        if (!hasDevices)
                        {
                            dto.OverallHealth = "Unknown";
                            dto.IsOnline = true;
                            dto.StatusDetails = "Làn sẵn sàng (Software Lane)";
                        }
                        else if (isControllerOffline)
                        {
                            dto.OverallHealth = "Critical";
                            dto.IsOnline = false;
                            dto.StatusDetails = $"Lỗi kết nối bộ điều khiển ({controller?.ControllerName} Offline)";
                        }
                        else if (hasOfflineSubdevice)
                        {
                            dto.OverallHealth = "Degraded";
                            dto.IsOnline = true;
                            dto.StatusDetails = "Hoạt động không ổn định (Một số thiết bị ngoại vi Offline)";
                        }
                        else
                        {
                            dto.OverallHealth = "Healthy";
                            dto.IsOnline = true;
                            dto.StatusDetails = "Hoạt động bình thường";
                        }
                    }

                    results.Add(dto);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeviceMonitoringService", "GetLanesStatusAsync", "Failed to retrieve lanes status", ex);
            }

            return results;
        }

        // ── Event Feed ──────────────────────────────────────────────────────
        public async Task<List<DeviceEventDto>> GetDeviceEventsAsync(int maxCount = 100)
        {
            var results = new List<DeviceEventDto>();

            try
            {
                string connStr = ConnectionManager.Instance.CurrentConnectionString;
                if (string.IsNullOrWhiteSpace(connStr)) return results;

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        SELECT TOP (@MaxCount) Id, Timestamp, DeviceType, DeviceName, EventType, Severity, Description
                        FROM dbo.DeviceEvents
                        ORDER BY Timestamp DESC";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@MaxCount", maxCount);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            while (await r.ReadAsync())
                            {
                                results.Add(new DeviceEventDto
                                {
                                    Id = r.GetInt64(0),
                                    Timestamp = r.GetDateTime(1),
                                    DeviceType = r.GetString(2),
                                    DeviceName = r.GetString(3),
                                    EventType = r.GetString(4),
                                    Severity = r.GetString(5),
                                    Description = r.IsDBNull(6) ? string.Empty : r.GetString(6)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Return empty list if table doesn't exist yet
                LoggingService.Instance.LogInfo("DeviceMonitoringService", "GetDeviceEventsAsync", $"Cannot read DeviceEvents: {ex.Message}");
            }

            return results;
        }

        public async Task LogDeviceEventAsync(DeviceEventDto evt)
        {
            try
            {
                string connStr = ConnectionManager.Instance.CurrentConnectionString;
                if (string.IsNullOrWhiteSpace(connStr)) return;

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // Insert
                    string insertSql = @"
                        INSERT INTO dbo.DeviceEvents (Timestamp, DeviceType, DeviceName, EventType, Severity, Description)
                        VALUES (@Timestamp, @DeviceType, @DeviceName, @EventType, @Severity, @Description)";
                    using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Timestamp", evt.Timestamp);
                        cmd.Parameters.AddWithValue("@DeviceType", evt.DeviceType);
                        cmd.Parameters.AddWithValue("@DeviceName", evt.DeviceName);
                        cmd.Parameters.AddWithValue("@EventType", evt.EventType);
                        cmd.Parameters.AddWithValue("@Severity", evt.Severity);
                        cmd.Parameters.AddWithValue("@Description", evt.Description ?? (object)DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Auto Purge (keep last 1000)
                    string purgeSql = @"
                        DELETE FROM dbo.DeviceEvents
                        WHERE Id NOT IN (
                            SELECT TOP (1000) Id 
                            FROM dbo.DeviceEvents 
                            ORDER BY Timestamp DESC
                        )";
                    using (var cmd = new SqlCommand(purgeSql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeviceMonitoringService", "LogDeviceEventAsync", "Failed to write device event", ex);
            }
        }

        // ── Differential Refresh (State Tracking) ───────────────────────────
        public async Task DetectAndLogStateChanges(
            List<C3ControllerStatusDto> controllers,
            List<RfidReaderStatusDto> readers,
            List<CameraStatusDto> cameras)
        {
            var changesToLog = new List<DeviceEventDto>();

            lock (_stateLock)
            {
                // Controllers
                foreach (var ctrl in controllers)
                {
                    string key = $"Controller_{ctrl.Id}";
                    bool current = ctrl.IsOnline;
                    if (_previousDeviceStates.TryGetValue(key, out bool prev))
                    {
                        if (prev != current)
                        {
                            changesToLog.Add(new DeviceEventDto
                            {
                                Timestamp = DateTime.Now,
                                DeviceType = "Controller",
                                DeviceName = ctrl.ControllerName,
                                EventType = current ? "Online" : "Offline",
                                Severity = current ? "Info" : "Error",
                                Description = current 
                                    ? $"Bộ điều khiển {ctrl.ControllerName} đã trực tuyến trở lại." 
                                    : $"Mất kết nối với bộ điều khiển {ctrl.ControllerName}."
                            });
                        }
                    }
                    _previousDeviceStates[key] = current;
                }

                // Readers
                foreach (var rdr in readers)
                {
                    string key = $"Reader_{rdr.ReaderNo}";
                    bool current = rdr.IsOnline;
                    if (_previousDeviceStates.TryGetValue(key, out bool prev))
                    {
                        if (prev != current)
                        {
                            changesToLog.Add(new DeviceEventDto
                            {
                                Timestamp = DateTime.Now,
                                DeviceType = "Reader",
                                DeviceName = rdr.ReaderName,
                                EventType = current ? "Online" : "Offline",
                                Severity = current ? "Info" : "Error",
                                Description = current 
                                    ? $"Đầu đọc RFID {rdr.ReaderName} đã trực tuyến." 
                                    : $"Mất kết nối với đầu đọc {rdr.ReaderName}."
                            });
                        }
                    }
                    _previousDeviceStates[key] = current;
                }



                // Cameras
                foreach (var cam in cameras)
                {
                    string key = $"Camera_{cam.Id}";
                    bool current = cam.IsOnline;
                    if (_previousDeviceStates.TryGetValue(key, out bool prev))
                    {
                        if (prev != current)
                        {
                            changesToLog.Add(new DeviceEventDto
                            {
                                Timestamp = DateTime.Now,
                                DeviceType = "Camera",
                                DeviceName = cam.CameraName,
                                EventType = current ? "Online" : "Offline",
                                Severity = current ? "Info" : "Error",
                                Description = current 
                                    ? $"Camera {cam.CameraName} đã trực tuyến." 
                                    : $"Mất kết nối với Camera {cam.CameraName}."
                            });
                        }
                    }
                    _previousDeviceStates[key] = current;
                }
            }

            // Log changes and publish to EventBus
            foreach (var change in changesToLog)
            {
                await LogDeviceEventAsync(change);

                var realtimeSeverity = change.Severity switch
                {
                    "Error" => RealtimeEventSeverity.Error,
                    "Warning" => RealtimeEventSeverity.Warning,
                    _ => RealtimeEventSeverity.Info
                };

                var eventTypeStr = (change.DeviceType.ToUpper(), change.EventType.ToUpper()) switch
                {
                    ("CONTROLLER", "ONLINE") => "CONTROLLER_ONLINE",
                    ("CONTROLLER", "OFFLINE") => "CONTROLLER_OFFLINE",
                    ("READER", "ONLINE") => "READER_ONLINE",
                    ("READER", "OFFLINE") => "READER_OFFLINE",
                    ("CAMERA", "ONLINE") => "CAMERA_ONLINE",
                    ("CAMERA", "OFFLINE") => "CAMERA_OFFLINE",
                    _ => "SYSTEM_INFO"
                };

                try
                {
                    EventBus.Instance.Publish(new RealtimeEvent
                    {
                        Timestamp = change.Timestamp,
                        EventType = eventTypeStr,
                        Source = change.DeviceName,
                        Message = change.Description,
                        Severity = realtimeSeverity
                    });
                }
                catch { }
            }
        }

        // ── Device Detail View ──────────────────────────────────────────────
        public async Task<DeviceDetailDto?> GetDeviceDetailAsync(string deviceType, string deviceName)
        {
            var detail = new DeviceDetailDto
            {
                DeviceName = deviceName,
                DeviceType = deviceType,
                CurrentStatus = "Offline",
                IsOnline = false,
                LastCommunication = null,
                StatusDetails = "Không xác định"
            };

            try
            {
                // Fetch info
                if (deviceType.Equals("Controller", StringComparison.OrdinalIgnoreCase))
                {
                    var controllers = await GetC3ControllersStatusAsync();
                    var ctrl = controllers.FirstOrDefault(c => c.ControllerName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
                    if (ctrl != null)
                    {
                        detail.IpAddress = ctrl.IpAddress;
                        detail.IsOnline = ctrl.IsOnline;
                        detail.CurrentStatus = ctrl.IsOnline ? "Online" : "Offline";
                        detail.StatusDetails = ctrl.StatusDetails;
                        detail.LastCommunication = ctrl.IsOnline ? DateTime.Now : null;
                    }
                }
                else if (deviceType.Equals("Camera", StringComparison.OrdinalIgnoreCase))
                {
                    var cameras = await GetCamerasStatusAsync();
                    var cam = cameras.FirstOrDefault(c => c.CameraName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
                    if (cam != null)
                    {
                        detail.IpAddress = cam.IpAddress;
                        detail.IsOnline = cam.IsOnline;
                        detail.CurrentStatus = cam.Status;
                        detail.StatusDetails = cam.StatusDetails;
                        detail.LastCommunication = cam.IsOnline ? DateTime.Now : null;
                    }
                }

                else if (deviceType.Equals("Reader", StringComparison.OrdinalIgnoreCase))
                {
                    var controllers = await GetC3ControllersStatusAsync();
                    var readers = await GetRfidReadersStatusAsync(controllers);
                    var rdr = readers.FirstOrDefault(r => r.ReaderName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
                    if (rdr != null)
                    {
                        detail.IsOnline = rdr.IsOnline;
                        detail.CurrentStatus = rdr.IsOnline ? "Online" : "Offline";
                        detail.StatusDetails = rdr.StatusDetails;
                        detail.LastCommunication = rdr.IsOnline ? DateTime.Now : null;
                    }
                }

                // Fetch recent events for this device
                string connStr = ConnectionManager.Instance.CurrentConnectionString;
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    using (var conn = new SqlConnection(connStr))
                    {
                        await conn.OpenAsync();
                        string sql = @"
                            SELECT TOP (20) Id, Timestamp, DeviceType, DeviceName, EventType, Severity, Description
                            FROM dbo.DeviceEvents
                            WHERE DeviceName = @DeviceName
                            ORDER BY Timestamp DESC";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@DeviceName", deviceName);
                            using (var r = await cmd.ExecuteReaderAsync())
                            {
                                while (await r.ReadAsync())
                                {
                                    detail.RecentEvents.Add(new DeviceEventDto
                                    {
                                        Id = r.GetInt64(0),
                                        Timestamp = r.GetDateTime(1),
                                        DeviceType = r.GetString(2),
                                        DeviceName = r.GetString(3),
                                        EventType = r.GetString(4),
                                        Severity = r.GetString(5),
                                        Description = r.IsDBNull(6) ? string.Empty : r.GetString(6)
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeviceMonitoringService", "GetDeviceDetailAsync", $"Error fetching details for {deviceType}/{deviceName}", ex);
            }

            return detail;
        }

        // ── Standard Ping Helper ────────────────────────────────────────────
        private async Task<(bool Success, long Latency)> PingAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return (false, -1);

            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ipAddress, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        return (true, reply.RoundtripTime);
                    }
                }
            }
            catch
            {
                // Silently swallow ping exception (e.g. host unreachable)
            }

            return (false, -1);
        }
    }
}
