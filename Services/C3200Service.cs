using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    /// <summary>Dữ liệu sự kiện real-time từ C3-200.</summary>
    public class C3200Event
    {
        public string Time { get; set; } = "";
        public string CardNo { get; set; } = "";
        public int Pin { get; set; }
        public int Door { get; set; } = 1;
        public int EventType { get; set; }
        public int InOutState { get; set; }
        public int VerifyMode { get; set; }
        public string RawData { get; set; } = "";
        public string ControllerIp { get; set; } = ""; // Mới: Xác định từ controller nào
    }

    /// <summary>Lưu trữ kết nối cho từng Controller.</summary>
    public class ControllerConnection
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 4370;
        public string Password { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 4000;
        public int BarrierDuration { get; set; } = 5;

        public IntPtr Handle { get; set; } = IntPtr.Zero;
        public CancellationTokenSource? Cts { get; set; }
        public bool IsConnected => Handle != IntPtr.Zero;
    }

    /// <summary>
    /// Điều khiển ZKTeco C3-200 Access Control Panel qua plcommpro.dll (Pull SDK).
    /// </summary>
    public sealed class C3200Service
    {
        public static readonly C3200Service Instance = new();

        // ── P/Invoke ──────────────────────────────────────────────────────────────

        [DllImport("plcommpro.dll", EntryPoint = "Connect",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr PLConnect(string parameters);

        [DllImport("plcommpro.dll", EntryPoint = "Disconnect",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int PLDisconnect(IntPtr handle);

        [DllImport("plcommpro.dll", EntryPoint = "GetRTLog",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int PLGetRTLog(IntPtr handle, byte[] buffer, int bufferSize);

        // ControlDevice(handle, operationID, param1, param2, param3, param4, options)
        [DllImport("plcommpro.dll", EntryPoint = "ControlDevice",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int PLControlDevice(IntPtr handle, int operationID,
            int param1, int param2, int param3, int param4, string options);

        [DllImport("plcommpro.dll", EntryPoint = "PullLastError",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int PLPullLastError();

        [DllImport("plcommpro.dll", EntryPoint = "GetDeviceParam",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int PLGetDeviceParam(IntPtr handle, byte[] buffer, int bufferSize, string itemValues);

        // ──────────────────────────────────────────────────────────────────────────

        private IntPtr _handle = IntPtr.Zero; // Primary connection handle for legacy compatibility
        private CancellationTokenSource? _cts; // Primary polling CTS
        private static readonly object _globalSdkLock = new();
        private static readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

        private string _ip = "192.168.1.201";
        private int _port = 4370;
        private string _password = "";
        private int _timeoutMs = 4000;
        private int _barrierDuration = 5;
        private string? _lastRawEventSignature = null;

        // Concurrent connection manager
        private readonly ConcurrentDictionary<string, ControllerConnection> _connections = new(StringComparer.OrdinalIgnoreCase);

        public bool IsConnected => GetConnection(_ip)?.IsConnected ?? false;
        public string LastError { get; private set; } = "";

        /// <summary>Sự kiện quẹt thẻ legacy: (cardNo, doorNumber, inOutState).</summary>
        public event Action<string, int, int>? OnCardScanned;

        /// <summary>Sự kiện quẹt thẻ mở rộng (có IP controller).</summary>
        public event Action<string, int, int, string>? OnCardScannedEx;

        /// <summary>Sự kiện đầy đủ từ RTLog (bao gồm tất cả dữ liệu).</summary>
        public event Action<C3200Event>? OnEvent;

        /// <summary>Sự kiện thay đổi trạng thái kết nối.</summary>
        public event Action<bool>? OnConnectionChanged;

        private C3200Service() { }

        // ── Cấu hình ─────────────────────────────────────────────────────────────

        public void Configure(string ip, int port = 4370, string password = "",
            int timeoutMs = 4000, int barrierDuration = 5)
        {
            _ip = ip;
            _port = port;
            _password = password;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : 4000;
            _barrierDuration = barrierDuration is > 0 and <= 254 ? barrierDuration : 5;

            var conn = _connections.GetOrAdd(ip, new ControllerConnection { IpAddress = ip });
            conn.Port = port;
            conn.Password = password;
            conn.TimeoutMs = _timeoutMs;
            conn.BarrierDuration = _barrierDuration;
        }

        public ControllerConnection? GetConnection(string ip)
        {
            _connections.TryGetValue(ip, out var conn);
            return conn;
        }

        public List<ControllerConnection> GetAllActiveConnections()
        {
            return _connections.Values.ToList();
        }

        // ── Kiểm tra quyền sở hữu controller ────────────────────────────────────

        /// <summary>
        /// Kiểm tra xem máy hiện tại có phải là máy được cấu hình để điều khiển
        /// controller này không (dựa theo PcIp lưu trong database).
        /// Nếu <paramref name="pcIp"/> rỗng hoặc 127.0.0.1 → luôn cho phép (backward-compatible).
        /// </summary>
        public static bool IsOwnerOfController(string? pcIp)
        {
            if (string.IsNullOrWhiteSpace(pcIp) ||
                pcIp.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return true;

            var localIps = GetLocalIpAddresses();
            return localIps.Contains(pcIp.Trim());
        }

        /// <summary>
        /// Trả về tập hợp tất cả IPv4 thực của máy hiện tại.
        /// </summary>
        public static HashSet<string> GetLocalIpAddresses()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                        continue;

                    var desc = nic.Description.ToLowerInvariant();
                    var name = nic.Name.ToLowerInvariant();
                    if (desc.Contains("virtual") || desc.Contains("vmware") ||
                        desc.Contains("virtualbox") || desc.Contains("hyper-v") ||
                        desc.Contains("vpn") || desc.Contains("pseudo") ||
                        name.Contains("vethernet") || name.Contains("loopback"))
                        continue;

                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                            continue;
                        var ipStr = addr.Address.ToString();
                        if (ipStr == "127.0.0.1" || ipStr.StartsWith("169.254."))
                            continue;
                        result.Add(ipStr);
                    }
                }
            }
            catch { }

            if (result.Count == 0)
            {
                try
                {
                    foreach (var addr in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    {
                        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            addr.ToString() != "127.0.0.1")
                            result.Add(addr.ToString());
                    }
                }
                catch { }
            }

            return result;
        }

        private static async Task<bool> IsActiveOwnerOfControllerAsync(string ip)
        {
            try
            {
                var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var matchedCtrl = allControllers.FirstOrDefault(c => c.IsActive && c.IpAddress.Trim().Equals(ip.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedCtrl == null) return true;

                return IsOwnerOfController(matchedCtrl.PcIp);
            }
            catch
            {
                return true; // fail-safe
            }
        }

        // ── Kết nối ───────────────────────────────────────────────────────────────

        public async Task<bool> ConnectAsync()
        {
            return await ConnectToControllerAsync(_ip);
        }

        public async Task<bool> ConnectToControllerAsync(string ip)
        {
            try
            {
                bool isOwner = await IsActiveOwnerOfControllerAsync(ip);
                if (!isOwner)
                {
                    var myIps = string.Join(", ", GetLocalIpAddresses());
                    LoggingService.Instance.LogWarning("C3_CONNECT_ABORT", "C3200Service",
                        $"Từ chối kết nối đến controller {ip} vì không phải active owner. Máy hiện tại: {myIps}");
                    LastError = $"Không có quyền active owner đối với controller {ip}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("C3_CONNECT_OWNER_CHECK_ERROR", "C3200Service",
                    "Lỗi khi kiểm tra PcIp ownership, tiếp tục kết nối (fail-safe).", ex);
            }

            return await ErrorHandling.SafeExecutionService.SafeExecuteAsync(async () => 
            {
                var conn = _connections.GetOrAdd(ip, new ControllerConnection { IpAddress = ip });
                
                await _connectionSemaphore.WaitAsync();
                try
                {
                    if (conn.IsConnected)
                    {
                        return true;
                    }

                    IntPtr handle = await Task.Run(() => {
                        IntPtr h = IntPtr.Zero;
                        string baseParams = $"protocol=TCP,ipaddress={ip},port={conn.Port},timeout={conn.TimeoutMs},device=1";
                        string[] candidates = string.IsNullOrWhiteSpace(conn.Password) 
                            ? new[] { baseParams, baseParams + ",password=", baseParams + ",passwd=" }
                            : new[] { baseParams + $",password={conn.Password}", baseParams + $",passwd={conn.Password}", baseParams };
                        
                        foreach (var parameters in candidates)
                        {
                            lock (_globalSdkLock)
                            {
                                h = PLConnect(parameters);
                            }
                            if (h != IntPtr.Zero) break;
                        }
                        return h;
                    });

                    if (handle == IntPtr.Zero)
                    {
                        LastError = $"Kết nối thất bại (sdkError={GetSdkError()})";
                        conn.Handle = IntPtr.Zero;
                        if (ip.Equals(_ip, StringComparison.OrdinalIgnoreCase))
                        {
                            _handle = IntPtr.Zero;
                            DetectedLockCount = -1;
                            OnConnectionChanged?.Invoke(false);
                        }
                        return false;
                    }

                    conn.Handle = handle;

                    if (ip.Equals(_ip, StringComparison.OrdinalIgnoreCase))
                    {
                        _handle = handle;
                        DetectedLockCount = GetLockCount(handle);
                        OnConnectionChanged?.Invoke(true);
                    }

                    StartPollingForConnection(conn);
                    return true;
                }
                finally
                {
                    _connectionSemaphore.Release();
                }
            }, 
            source: "C3200Service.Connect", 
            defaultValue: false,
            friendlyMessage: "Không thể kết nối với bộ điều khiển C3-200. Vui lòng kiểm tra mạng.");
        }

        public int DetectedLockCount { get; private set; } = -1;

        public int GetLockCount(IntPtr specificHandle = default)
        {
            if (specificHandle == IntPtr.Zero)
            {
                if (IsConnected && DetectedLockCount > 0)
                {
                    return DetectedLockCount;
                }
                return -1;
            }

            lock (_globalSdkLock)
            {
                if (specificHandle == IntPtr.Zero) return -1;
                var buffer = new byte[256];
                int ret = PLGetDeviceParam(specificHandle, buffer, buffer.Length, "LockCount");
                if (ret >= 0)
                {
                    string resultStr = Encoding.ASCII.GetString(buffer).Split('\0')[0];
                    if (!string.IsNullOrEmpty(resultStr) && resultStr.Contains("LockCount="))
                    {
                        var parts = resultStr.Split('=');
                        if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var lockCount))
                        {
                            return lockCount;
                        }
                    }
                }
                return -1;
            }
        }

        public void Disconnect()
        {
            _connectionSemaphore.Wait();
            try
            {
                foreach (var conn in _connections.Values)
                {
                    DisconnectControllerInternal(conn);
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        public void DisconnectController(string ip)
        {
            _connectionSemaphore.Wait();
            try
            {
                if (_connections.TryGetValue(ip, out var conn))
                {
                    DisconnectControllerInternal(conn);
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private void DisconnectControllerInternal(ControllerConnection conn)
        {
            StopPollingForConnection(conn);
            lock (_globalSdkLock)
            {
                if (conn.Handle != IntPtr.Zero)
                {
                    try { PLDisconnect(conn.Handle); } catch { }
                    conn.Handle = IntPtr.Zero;
                }
                if (conn.IpAddress.Equals(_ip, StringComparison.OrdinalIgnoreCase))
                {
                    _handle = IntPtr.Zero;
                    DetectedLockCount = -1;
                    OnConnectionChanged?.Invoke(false);
                }
            }
        }

        // ── Mở / Đóng barrier ────────────────────────────────────────────────────

        /// <summary>Mở barrier theo LaneId cấu hình trong topology.</summary>
        public async Task<bool> OpenBarrierAsync(int laneId)
        {
            return await ErrorHandling.SafeExecutionService.SafeExecuteAsync(async () => 
            {
                var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var allBarriers = await ParkingTopologyService.Instance.GetBarriersAsync();
                
                var barrier = allBarriers.FirstOrDefault(b => b.LaneId == laneId && b.IsActive);
                if (barrier == null)
                {
                    // Fallback to legacy OpenBarrier if laneId acts as physical door number on primary controller
                    return await OpenBarrierLegacyAsync(laneId);
                }

                var ctrl = allControllers.FirstOrDefault(c => c.Id == barrier.ControllerId && c.IsActive);
                if (ctrl == null)
                {
                    LastError = $"Không tìm thấy Controller cho LaneId {laneId}";
                    return false;
                }

                string ip = ctrl.IpAddress;

                if (!_connections.TryGetValue(ip, out var conn) || !conn.IsConnected)
                {
                    bool ok = await ConnectToControllerAsync(ip);
                    if (!ok || !_connections.TryGetValue(ip, out conn) || !conn.IsConnected)
                    {
                        LastError = $"Controller {ip} chưa kết nối";
                        return false;
                    }
                }

                StopPollingForConnection(conn);

                int r = -1;
                lock (_globalSdkLock)
                {
                    if (conn.Handle != IntPtr.Zero)
                    {
                        r = PLControlDevice(conn.Handle, 1, barrier.RelayNumber, 1, conn.BarrierDuration, 0, "");
                    }
                }

                if (r < 0)
                {
                    DisconnectController(ip);
                    await Task.Delay(50);
                    if (await ConnectToControllerAsync(ip) && _connections.TryGetValue(ip, out conn) && conn.IsConnected)
                    {
                        StopPollingForConnection(conn);
                        lock (_globalSdkLock)
                        {
                            if (conn.Handle != IntPtr.Zero)
                            {
                                r = PLControlDevice(conn.Handle, 1, barrier.RelayNumber, 1, conn.BarrierDuration, 0, "");
                            }
                        }
                    }
                }

                StartPollingForConnection(conn);

                try
                {
                    var dbg = $"{DateTime.Now:O}\tOpenBarrier\tLaneId={laneId}\tController={ip}\trelay={barrier.RelayNumber}\tret={r}\tsdk={GetSdkError()}\n";
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"), dbg);
                }
                catch { }

                if (r < 0)
                {
                    LastError = $"ControlDevice thất bại (ret={r}, sdkError={GetSdkError()})";
                    return false;
                }

                LastError = $"ret={r}";
                try
                {
                    var topo = EventBus.Instance.ResolveTopologyForReader(barrier.RelayNumber == 1 ? 1 : 3);
                    EventBus.Instance.Publish(new RealtimeEvent
                    {
                        Timestamp = DateTime.Now,
                        EventType = "BARRIER_OPEN",
                        Severity = RealtimeEventSeverity.Success,
                        Source = "C3200Service",
                        Message = $"Mở Barrier thành công tại Làn ID: '{laneId}'",
                        Site = topo.Site,
                        Zone = topo.Zone,
                        Gate = topo.Gate,
                        Lane = topo.Lane
                    });
                }
                catch { }
                return true;
            }, 
            source: $"C3200Service.OpenBarrier({laneId})",
            defaultValue: false,
            friendlyMessage: "Lỗi lệnh điều khiển Barrier.");
        }

        /// <summary>Mở barrier legacy. doorNumber: 1 = cửa vào, 2 = cửa ra.</summary>
        public async Task<bool> OpenBarrierLegacyAsync(int doorNumber = 1)
        {
            var conn = _connections.GetOrAdd(_ip, new ControllerConnection { IpAddress = _ip });
            if (!conn.IsConnected && !await ConnectToControllerAsync(_ip))
                return false;

            StopPollingForConnection(conn);

            int r = -1;
            lock (_globalSdkLock)
            {
                if (conn.Handle != IntPtr.Zero)
                {
                    r = PLControlDevice(conn.Handle, 1, doorNumber, 1, conn.BarrierDuration, 0, "");
                }
            }

            if (r < 0)
            {
                DisconnectController(_ip);
                await Task.Delay(50);
                if (await ConnectToControllerAsync(_ip) && _connections.TryGetValue(_ip, out conn) && conn.IsConnected)
                {
                    StopPollingForConnection(conn);
                    lock (_globalSdkLock)
                    {
                        if (conn.Handle != IntPtr.Zero)
                        {
                            r = PLControlDevice(conn.Handle, 1, doorNumber, 1, conn.BarrierDuration, 0, "");
                        }
                    }
                }
            }

            StartPollingForConnection(conn);

            try
            {
                var dbg = $"{DateTime.Now:O}\tOpenBarrierLegacy\tdoor={doorNumber}\tret={r}\tsdk={GetSdkError()}\n";
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"), dbg);
            }
            catch { }

            return r >= 0;
        }

        /// <summary>Đóng barrier. doorNumber: 1 = cửa vào, 2 = cửa ra.</summary>
        public Task<bool> CloseBarrierAsync(int doorNumber = 1)
        {
            var conn = GetConnection(_ip);
            if (conn == null || !conn.IsConnected) return Task.FromResult(false);

            try
            {
                int r = -1;
                lock (_globalSdkLock)
                {
                    if (conn.Handle != IntPtr.Zero)
                    {
                        r = PLControlDevice(conn.Handle, 2, doorNumber, 0, 0, 0, "");
                    }
                }
                if (r >= 0)
                {
                    return Task.FromResult(true);
                }

                LastError = $"CloseBarrier thất bại (ret={r}, sdkError={GetSdkError()})";
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return Task.FromResult(false);
            }
        }

        // ── RTLog polling (nhận sự kiện quẹt thẻ) ────────────────────────────────

        private void StartPollingForConnection(ControllerConnection conn)
        {
            lock (_globalSdkLock)
            {
                StopPollingForConnection(conn);
                conn.Cts = new CancellationTokenSource();
                _ = PollRTLogForConnectionAsync(conn, conn.Cts.Token);
            }
        }

        private void StopPollingForConnection(ControllerConnection conn)
        {
            conn.Cts?.Cancel();
            conn.Cts = null;
        }

        private async Task PollRTLogForConnectionAsync(ControllerConnection conn, CancellationToken token)
        {
            var buffer = new byte[4096];
            string ip = conn.IpAddress;

            while (!token.IsCancellationRequested && conn.IsConnected)
            {
                try
                {
                    int result = -1;
                    lock (_globalSdkLock)
                    {
                        if (conn.Handle != IntPtr.Zero)
                        {
                            result = PLGetRTLog(conn.Handle, buffer, buffer.Length);
                        }
                    }

                    if (result < 0)
                    {
                        lock (_globalSdkLock)
                        {
                            try { PLDisconnect(conn.Handle); } catch { }
                            conn.Handle = IntPtr.Zero;
                        }
                        if (ip.Equals(_ip, StringComparison.OrdinalIgnoreCase))
                        {
                            _handle = IntPtr.Zero;
                            OnConnectionChanged?.Invoke(false);
                        }
                        break;
                    }

                    if (result > 0)
                    {
                        int nullIndex = Array.IndexOf(buffer, (byte)0);
                        string rawData = nullIndex >= 0 
                            ? Encoding.UTF8.GetString(buffer, 0, nullIndex) 
                            : Encoding.UTF8.GetString(buffer);
                        
                        ParseEventForConnection(rawData, ip);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    lock (_globalSdkLock)
                    {
                        try { PLDisconnect(conn.Handle); } catch { }
                        conn.Handle = IntPtr.Zero;
                    }
                    if (ip.Equals(_ip, StringComparison.OrdinalIgnoreCase))
                    {
                        _handle = IntPtr.Zero;
                        OnConnectionChanged?.Invoke(false);
                    }
                    break;
                }

                await Task.Delay(200, token);
            }
        }

        private void ParseEventForConnection(string data, string controllerIp)
        {
            foreach (var record in data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var evt = TryParseRecord(record);
                if (evt == null) continue;

                evt.ControllerIp = controllerIp;

                Debug.WriteLine($"📡 C3200 Event [{controllerIp}]: card={evt.CardNo}, door={evt.Door}, " +
                    $"event={evt.EventType}, inout={evt.InOutState}, verify={evt.VerifyMode}, time={evt.Time}");

                bool isCardSwiped = !string.IsNullOrEmpty(evt.CardNo) && evt.CardNo != "0";
                string currentSig = $"{evt.CardNo}_{evt.Door}_{evt.EventType}_{evt.InOutState}";
                bool hasChanged = isCardSwiped || currentSig != _lastRawEventSignature;

                if (hasChanged)
                {
                    _lastRawEventSignature = currentSig;
                    try
                    {
                        LoggingService.Instance.LogInfo("C3200_RAW_EVENT", "C3200Service", $"Raw event [{controllerIp}]: card={evt.CardNo}, door={evt.Door}, event={evt.EventType}, inout={evt.InOutState}");
                    }
                    catch { }
                }

                OnEvent?.Invoke(evt);

                if (!string.IsNullOrEmpty(evt.CardNo) && evt.CardNo != "0")
                {
                    OnCardScanned?.Invoke(evt.CardNo, evt.Door, evt.InOutState);
                    OnCardScannedEx?.Invoke(evt.CardNo, evt.Door, evt.InOutState, controllerIp);
                }
            }
        }

        private static C3200Event? TryParseRecord(string record)
        {
            if (string.IsNullOrWhiteSpace(record)) return null;

            var evt = new C3200Event { RawData = record };

            if (record.Contains('='))
            {
                foreach (var part in record.Split(new[] { ',', '\t' }))
                {
                    var trimmed = part.Trim();
                    var eqIdx = trimmed.IndexOf('=');
                    if (eqIdx < 0) continue;

                    var key = trimmed[..eqIdx].Trim().ToLowerInvariant();
                    var val = trimmed[(eqIdx + 1)..].Trim();

                    switch (key)
                    {
                        case "cardno": evt.CardNo = val; break;
                        case "door": if (int.TryParse(val, out var d)) evt.Door = d; break;
                        case "pin": if (int.TryParse(val, out var p)) evt.Pin = p; break;
                        case "eventtype": if (int.TryParse(val, out var et)) evt.EventType = et; break;
                        case "inoutstate": if (int.TryParse(val, out var io)) evt.InOutState = io; break;
                        case "verifymode": if (int.TryParse(val, out var vm)) evt.VerifyMode = vm; break;
                        case "time": evt.Time = val; break;
                    }
                }
                return evt;
            }

            var fields = record.Split(',');
            if (fields.Length >= 7)
            {
                evt.Time = fields[0].Trim();
                int.TryParse(fields[1].Trim(), out var pin); evt.Pin = pin;
                evt.CardNo = fields[2].Trim();
                int.TryParse(fields[3].Trim(), out var door); evt.Door = door;
                int.TryParse(fields[4].Trim(), out var evtType); evt.EventType = evtType;
                int.TryParse(fields[5].Trim(), out var inOut); evt.InOutState = inOut;
                int.TryParse(fields[6].Trim(), out var verify); evt.VerifyMode = verify;
                return evt;
            }

            return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private int GetSdkError()
        {
            try
            {
                lock (_globalSdkLock)
                {
                    return PLPullLastError();
                }
            }
            catch { return -9999; }
        }

        public static (bool Success, int SdkError, string Diagnostic, string[] TriedParams, string DllArch) TestConnectDetailed(string ip, int port, string password, int timeoutMs)
        {
            var tried = new List<string>();
            bool success = false;
            int sdkErr = 0;
            string diag = string.Empty;

            try
            {
                string baseParams = $"protocol=TCP,ipaddress={ip},port={port},timeout={timeoutMs},device=1";
                var candidates = new List<string>();
                if (string.IsNullOrWhiteSpace(password))
                {
                    candidates.Add(baseParams);
                    candidates.Add(baseParams + ",password=");
                    candidates.Add(baseParams + ",passwd=");
                }
                else
                {
                    candidates.Add(baseParams + $",password={password}");
                    candidates.Add(baseParams + $",passwd={password}");
                    candidates.Add(baseParams);
                }

                foreach (var p in candidates)
                {
                    tried.Add(p);
                    try
                    {
                        IntPtr handle;
                        lock (_globalSdkLock)
                        {
                            handle = PLConnect(p);
                        }
                        if (handle != IntPtr.Zero)
                        {
                            try 
                            { 
                                lock (_globalSdkLock)
                                {
                                    PLDisconnect(handle); 
                                }
                            } 
                            catch { }
                            success = true;
                            sdkErr = 0;
                            break;
                        }
                        else
                        {
                            try 
                            { 
                                lock (_globalSdkLock)
                                {
                                    sdkErr = PLPullLastError(); 
                                }
                            } 
                            catch { sdkErr = -9999; }
                        }
                    }
                    catch
                    {
                        try 
                        { 
                            lock (_globalSdkLock)
                            {
                                sdkErr = PLPullLastError(); 
                            }
                        } 
                        catch { sdkErr = -9999; }
                    }
                }

                try
                {
                    var temp = Instance?.GetDiagnosticText();
                    if (!string.IsNullOrEmpty(temp)) diag = temp;
                }
                catch { }
            }
            catch { }

            string dllArch = DetectPlcommproArch();

            return (success, sdkErr, diag, tried.ToArray(), dllArch);
        }

        private static string DetectPlcommproArch()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = Path.Combine(baseDir, "plcommpro.dll");
                if (!File.Exists(path)) return "MISSING";

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);
                fs.Seek(0x3C, SeekOrigin.Begin);
                int peOffset = br.ReadInt32();
                fs.Seek(peOffset + 4, SeekOrigin.Begin);
                ushort machine = br.ReadUInt16();
                return machine == 0x8664 ? "x64" : machine == 0x14c ? "x86" : ("0x" + machine.ToString("X"));
            }
            catch { return "UNKNOWN"; }
        }

        public string GetDiagnosticText() =>
            $"Target: {_ip}:{_port}\n" +
            $"Connected: {IsConnected}\n" +
            $"BarrierDuration: {_barrierDuration}s\n" +
            $"LastError: {LastError}";
    }
}
