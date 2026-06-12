using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services
{
    public class LicenseMonitorService
    {
        private static readonly LicenseMonitorService _instance = new();
        public static LicenseMonitorService Instance => _instance;

        private readonly string _licenseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.lic");
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private System.Threading.Timer? _heartbeatTimer;
        private Action<string>? _onInvalidated;
        private readonly object _lock = new();

        private LicenseMonitorService()
        {
        }

        public void Start(Action<string> onInvalidated)
        {
            lock (_lock)
            {
                if (_heartbeatTimer != null) return;

                _onInvalidated = onInvalidated;

                // Run every 1 minute for fast testing & immediate response to revocation
                _heartbeatTimer = new System.Threading.Timer(ExecuteCheck, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                Serilog.Log.Information("LicenseMonitorService started.");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;
                Serilog.Log.Information("LicenseMonitorService stopped.");
            }
        }

        private void ExecuteCheck(object? state)
        {
            try
            {
                if (!File.Exists(_licenseFilePath))
                {
                    NotifyInvalid("Không tìm thấy file bản quyền local.");
                    return;
                }

                string json = File.ReadAllText(_licenseFilePath);
                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var license = JsonConvert.DeserializeObject<LicenseBlock>(json, settings);
                if (license == null)
                {
                    NotifyInvalid("File bản quyền không hợp lệ.");
                    return;
                }

                // Call heartbeat API on server
                bool isServerOnline = TryHeartbeat(license, out var isLicenseValid, out var reason);
                if (isServerOnline)
                {
                    if (!isLicenseValid)
                    {
                        // Revoked, expired, or deleted by admin
                        try
                        {
                            if (File.Exists(_licenseFilePath))
                            {
                                File.Delete(_licenseFilePath);
                            }
                        }
                        catch { }

                        string reasonMsg = reason switch
                        {
                            "LICENSE_REVOKED" => "License Key này đã bị thu hồi từ quản trị viên.",
                            "MACHINE_REVOKED" => "Thiết bị này đã bị thu hồi quyền truy cập.",
                            "EXPIRED" => "License Key đã hết hạn sử dụng.",
                            "LICENSE_NOT_FOUND" => "License Key không tồn tại trên hệ thống.",
                            "MACHINE_NOT_FOUND" => "Thiết bị chưa được kích hoạt cho key này.",
                            _ => $"Bản quyền không hợp lệ ({reason})."
                        };

                        NotifyInvalid(reasonMsg);
                    }
                    else
                    {
                        // Heartbeat successful and valid -> Update local check time
                        LicenseValidationService.Instance.UpdateLastServerCheckTime();
                    }
                }
                else
                {
                    // Server is offline -> Perform local grace period check
                    DateTime? lastCheck = LicenseValidationService.Instance.DecryptDate(license.LastServerCheckEncrypted ?? string.Empty);
                    if (lastCheck == null)
                    {
                        NotifyInvalid("Thiết bị đang chạy ngoại tuyến và không tìm thấy thông tin xác thực an toàn trước đó.");
                        return;
                    }

                    if (DateTime.UtcNow < lastCheck.Value - TimeSpan.FromMinutes(10))
                    {
                        NotifyInvalid("Lỗi đồng hồ hệ thống: Thời gian hiện tại nhỏ hơn thời gian xác thực an toàn cuối cùng.");
                        return;
                    }

                    var offlineDuration = DateTime.UtcNow - lastCheck.Value;
                    if (offlineDuration > TimeSpan.FromHours(24))
                    {
                        NotifyInvalid($"Đã quá giới hạn hoạt động ngoại tuyến (24 giờ). Thời gian ngoại tuyến: {offlineDuration.TotalHours:F1} giờ. Vui lòng kết nối Internet.");
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"Exception in LicenseMonitorService ExecuteCheck: {ex.Message}");
            }
        }

        private bool TryHeartbeat(LicenseBlock license, out bool isLicenseValid, out string reason)
        {
            isLicenseValid = false;
            reason = string.Empty;
            try
            {
                var localFingerprint = LicenseValidationService.Instance.GetLocalFingerprint();
                var requestBody = new
                {
                    LicenseKey = license.LicenseKey,
                    MachineFingerprint = localFingerprint
                };

                var url = $"{LicenseValidationService.Instance.GetServerUrl()}/api/license/heartbeat";
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                using (var cts = new CancellationTokenSource(3000))
                {
                    var response = _httpClient.PostAsync(url, content, cts.Token).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        var result = JsonConvert.DeserializeAnonymousType(responseString, new { Valid = false, Reason = "" });
                        if (result != null)
                        {
                            isLicenseValid = result.Valid;
                            reason = result.Reason;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Network error, timeout, server down
                return false;
            }
            return false;
        }

        private void NotifyInvalid(string errorMsg)
        {
            Serilog.Log.Warning($"License invalidated: {errorMsg}");
            Stop();
            _onInvalidated?.Invoke(errorMsg);
        }
    }
}
