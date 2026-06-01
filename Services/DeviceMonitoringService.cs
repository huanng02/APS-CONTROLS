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

        private readonly DatabaseService _db = new DatabaseService();

        private DeviceMonitoringService() { }

        public async Task<DeviceKpiDto> GetDeviceKpiAsync()
        {
            var kpi = new DeviceKpiDto { TongCho = 200 };
            
            DateTime startOfDay = DateTime.Today;
            DateTime endOfDay = DateTime.Today.AddDays(1).AddTicks(-1);

            try
            {
                string connStr = ConnectionManager.Instance.CurrentConnectionString;
                if (string.IsNullOrWhiteSpace(connStr)) return kpi;

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    string sql = @"
                        SELECT 
                            (SELECT COUNT(*) FROM XeTrongBai WHERE ThoiGianRa IS NULL) AS XeTrongBai,
                            (SELECT COUNT(*) FROM LichSuXe WHERE ThoiGianVao >= @Start AND ThoiGianVao <= @End) AS LuotXeVao,
                            (SELECT COUNT(*) FROM LichSuXe WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End) AS LuotXeRa,
                            (SELECT ISNULL(SUM(Tien), 0) FROM LichSuXe WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End) AS DoanhThu,
                            (SELECT ISNULL(SUM(MaxCapacity), 200) FROM ParkingZones WHERE IsActive = 1) AS TongCho;
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startOfDay);
                        cmd.Parameters.AddWithValue("@End", endOfDay);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                kpi.XeTrongBai = reader["XeTrongBai"] != DBNull.Value ? Convert.ToInt32(reader["XeTrongBai"]) : 0;
                                kpi.LuotXeVaoHomNay = reader["LuotXeVao"] != DBNull.Value ? Convert.ToInt32(reader["LuotXeVao"]) : 0;
                                kpi.LuotXeRaHomNay = reader["LuotXeRa"] != DBNull.Value ? Convert.ToInt32(reader["LuotXeRa"]) : 0;
                                kpi.DoanhThuHomNay = reader["DoanhThu"] != DBNull.Value ? Convert.ToDouble(reader["DoanhThu"]) : 0;
                                kpi.TongCho = reader["TongCho"] != DBNull.Value ? Convert.ToInt32(reader["TongCho"]) : 200;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeviceMonitoringService", "GetDeviceKpiAsync", "Failed to retrieve KPIs", ex);
            }

            return kpi;
        }

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

        public async Task<List<LaneStatusDto>> GetLanesStatusAsync(List<C3ControllerStatusDto> controllerStatuses)
        {
            var results = new List<LaneStatusDto>();

            try
            {
                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();

                foreach (var lane in lanes)
                {
                    var gate = gates.FirstOrDefault(g => g.Id == lane.GateId);
                    var controller = controllers.FirstOrDefault(c => c.GateId == lane.GateId);
                    var ctrlStatus = controller != null 
                        ? controllerStatuses.FirstOrDefault(c => c.Id == controller.Id) 
                        : null;

                    var dto = new LaneStatusDto
                    {
                        Id = lane.Id,
                        LaneCode = lane.LaneCode,
                        LaneName = lane.LaneName,
                        Direction = lane.Direction,
                        IsActive = lane.IsActive,
                        GateName = gate?.GateName ?? "N/A",
                        ZoneName = lane.ZoneName ?? "N/A",
                    };

                    if (!lane.IsActive)
                    {
                        dto.IsOnline = false;
                        dto.StatusDetails = "Vô hiệu hóa (Inactive)";
                    }
                    else
                    {
                        // Check camera status based on camera service (if active)
                        dto.AssociatedCamera = lane.Direction.ToUpper() == "IN" ? "Cam Vào (Entrance)" : "Cam Ra (Exit)";
                        
                        // Smart determination: a lane is online if its gate's controller is online
                        if (ctrlStatus != null)
                        {
                            dto.IsOnline = ctrlStatus.IsOnline;
                            dto.StatusDetails = ctrlStatus.IsOnline 
                                ? $"Làn sẵn sàng (Bộ điều khiển {ctrlStatus.ControllerName} hoạt động)" 
                                : $"Lỗi kết nối bộ điều khiển ({ctrlStatus.ControllerName} Offline)";
                        }
                        else
                        {
                            // If no controller, default to Active state being Online for standalone software lanes
                            dto.IsOnline = true;
                            dto.StatusDetails = "Làn sẵn sàng (Software Lane)";
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
                    ReaderName = "Đầu đọc USB Đăng ký / Cổng",
                    ConnectionType = "USB COM Port",
                    PortOrAddress = "COM3",
                    AssociatedLaneName = "Cổng/Bàn Đăng ký",
                    IsOnline = usbReaderOnline,
                    StatusDetails = usbDetails
                });

                // 2. Add C3 Controller Readers
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();
                foreach (var ctrl in controllers)
                {
                    var ctrlStatus = controllerStatuses.FirstOrDefault(c => c.Id == ctrl.Id);
                    bool ctrlOnline = ctrlStatus?.IsOnline ?? false;

                    // A ZK C3-200 / C3-400 supports 2 or 4 readers
                    int readerCount = ctrl.ControllerName.Contains("400") ? 4 : 2;
                    for (int r = 1; r <= readerCount; r++)
                    {
                        int globalReaderNo = (ctrl.Id - 1) * 4 + r; // Unique ID formula for multiple controllers
                        var mapping = mappings.FirstOrDefault(m => m.ReaderNo == globalReaderNo);
                        string laneName = "Chưa cấu hình làn";
                        
                        if (mapping != null)
                        {
                            var lane = lanes.FirstOrDefault(l => l.Id == mapping.LaneId);
                            if (lane != null)
                            {
                                laneName = $"{lane.LaneName} ({mapping.Direction})";
                            }
                        }

                        results.Add(new RfidReaderStatusDto
                        {
                            ReaderNo = globalReaderNo,
                            ReaderName = $"Đầu đọc Wiegand {r} (Tủ {ctrl.ControllerName})",
                            ConnectionType = "C3 Wiegand",
                            PortOrAddress = $"Reader Pin #{r} @ {ctrl.IpAddress}",
                            AssociatedLaneName = laneName,
                            IsOnline = ctrlOnline,
                            StatusDetails = ctrlOnline 
                                ? $"Hoạt động (Wiegand D0/D1 OK)" 
                                : $"Mất kết nối tủ điều khiển ({ctrl.ControllerName} Offline)"
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
