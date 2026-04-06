using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private static extern int PLConnect(string parameters);

        [DllImport("plcommpro.dll", EntryPoint = "Disconnect",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int PLDisconnect(int handle);

        [DllImport("plcommpro.dll", EntryPoint = "GetRTLog",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int PLGetRTLog(int handle, StringBuilder buffer, int bufferSize);

        // ControlDevice(handle, operationID, param1, param2, param3, param4, options)
        [DllImport("plcommpro.dll", EntryPoint = "ControlDevice",
            CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern int PLControlDevice(int handle, int operationID,
            int param1, int param2, int param3, int param4, string options);

        [DllImport("plcommpro.dll", EntryPoint = "PullLastError",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int PLPullLastError();

        // ──────────────────────────────────────────────────────────────────────────

        private int _handle;
        private CancellationTokenSource? _cts;
        private readonly object _sdkLock = new();

        private string _ip = "192.168.1.201";
        private int _port = 4370;
        private string _password = "";
        private int _timeoutMs = 4000;
        private int _barrierDuration = 5;

        public bool IsConnected => _handle > 0;
        public string LastError { get; private set; } = "";

        /// <summary>Sự kiện quẹt thẻ: (cardNo, doorNumber).</summary>
        public event Action<string, int>? OnCardScanned;

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
        }

        // ── Kết nối ───────────────────────────────────────────────────────────────

        public Task<bool> ConnectAsync()
        {
            Disconnect();

            try
            {
                foreach (var parameters in BuildConnectCandidates())
                {
                    _handle = PLConnect(parameters);
                    if (_handle > 0) break;
                }

                if (_handle <= 0)
                {
                    LastError = $"Kết nối thất bại (sdkError={GetSdkError()})";
                    _handle = 0;
                    OnConnectionChanged?.Invoke(false);
                    return Task.FromResult(false);
                }

                OnConnectionChanged?.Invoke(true);
                StartPolling();
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _handle = 0;
                OnConnectionChanged?.Invoke(false);
                return Task.FromResult(false);
            }
        }

        public void Disconnect()
        {
            StopPolling();
            if (_handle > 0)
            {
                try { PLDisconnect(_handle); } catch { }
                _handle = 0;
            }
        }

        private string[] BuildConnectCandidates()
        {
            string baseParams = $"protocol=TCP,ipaddress={_ip},port={_port},timeout={_timeoutMs},device=1";

            if (string.IsNullOrWhiteSpace(_password))
                return [baseParams, baseParams + ",password=", baseParams + ",passwd="];

            return [
                baseParams + $",password={_password}",
                baseParams + $",passwd={_password}",
                baseParams
            ];
        }

        // ── Mở / Đóng barrier ────────────────────────────────────────────────────

        /// <summary>Mở barrier. doorNumber: 1 = cửa vào, 2 = cửa ra.</summary>
        public async Task<bool> OpenBarrierAsync(int doorNumber = 1)
        {
            if (!IsConnected && !await ConnectAsync())
                return false;

            try
            {
                StopPolling();

                // Gửi lệnh ngay trên kết nối hiện tại
                int r;
                lock (_sdkLock)
                {
                    r = PLControlDevice(_handle, 1, doorNumber, 1, _barrierDuration, 0, "");
                }

                // Nếu thất bại → reconnect rồi thử lại 1 lần
                if (r < 0)
                {
                    Disconnect();
                    await Task.Delay(50);
                    if (!await ConnectAsync())
                    {
                        LastError = "Reconnect thất bại";
                        return false;
                    }

                    StopPolling();
                    lock (_sdkLock)
                    {
                        r = PLControlDevice(_handle, 1, doorNumber, 1, _barrierDuration, 0, "");
                    }
                }

                StartPolling();

                if (r < 0)
                {
                    LastError = $"ControlDevice thất bại (ret={r}, sdkError={GetSdkError()})";
                    return false;
                }

                LastError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        /// <summary>Đóng barrier. doorNumber: 1 = cửa vào, 2 = cửa ra.</summary>
        public Task<bool> CloseBarrierAsync(int doorNumber = 1)
        {
            if (!IsConnected) return Task.FromResult(false);

            try
            {
                // operationID=2 (CancelAlarm / close)
                int r = PLControlDevice(_handle, 2, doorNumber, 0, 0, 0, "");
                if (r >= 0) return Task.FromResult(true);

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

        private void StartPolling()
        {
            _cts = new CancellationTokenSource();
            _ = PollRTLogAsync(_cts.Token);
        }

        private void StopPolling()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private async Task PollRTLogAsync(CancellationToken token)
        {
            var buffer = new StringBuilder(4096);

            while (!token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    buffer.Clear();
                    int result;
                    lock (_sdkLock)
                    {
                        result = PLGetRTLog(_handle, buffer, buffer.Capacity);
                    }

                    if (result < 0)
                    {
                        _handle = 0;
                        OnConnectionChanged?.Invoke(false);
                        break;
                    }

                    if (result > 0)
                        ParseEvent(buffer.ToString());
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    _handle = 0;
                    OnConnectionChanged?.Invoke(false);
                    break;
                }

                await Task.Delay(200, token);
            }
        }

        private void ParseEvent(string data)
        {
            foreach (var record in data.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
            {
                var evt = TryParseRecord(record);
                if (evt == null) continue;

                Debug.WriteLine($"📡 C3200 Event: card={evt.CardNo}, door={evt.Door}, " +
                    $"event={evt.EventType}, inout={evt.InOutState}, verify={evt.VerifyMode}, time={evt.Time}");

                OnEvent?.Invoke(evt);

                if (!string.IsNullOrEmpty(evt.CardNo) && evt.CardNo != "0")
                    OnCardScanned?.Invoke(evt.CardNo, evt.Door);
            }
        }

        /// <summary>
        /// Parse RTLog record. C3-200 trả 2 format:
        /// CSV: "2024-01-01 12:00:00,Pin,CardNo,Door,EventType,InOutState,VerifyMode"
        /// Key=Value: "time=...,cardno=...,door=...,..."
        /// </summary>
        private static C3200Event? TryParseRecord(string record)
        {
            if (string.IsNullOrWhiteSpace(record)) return null;

            var evt = new C3200Event { RawData = record };

            // Key=value format
            if (record.Contains('='))
            {
                foreach (var part in record.Split([',', '\t']))
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

            // CSV format: Time,Pin,CardNo,Door,EventType,InOutState,VerifyMode
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
            try { return PLPullLastError(); }
            catch { return -9999; }
        }

        public string GetDiagnosticText() =>
            $"Target: {_ip}:{_port}\n" +
            $"Connected: {IsConnected}\n" +
            $"BarrierDuration: {_barrierDuration}s\n" +
            $"LastError: {LastError}";
    }
}
