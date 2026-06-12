using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services
{
    public static class MachineFingerprint
    {
        public static string GetFingerprint()
        {
            string cpuId = GetCpuId();
            string diskId = GetDiskId();
            string macAddress = GetMacAddress();

            string raw = $"{cpuId.Trim()}#{diskId.Trim()}#{macAddress.Trim()}";
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static string GetCpuId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var val = obj["ProcessorId"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }
            return "CPU_UNKNOWN";
        }

        private static string GetDiskId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID = 'C:'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var val = obj["VolumeSerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }
            return "DISK_UNKNOWN";
        }

        private static string GetMacAddress()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.OperationalStatus == OperationalStatus.Up && 
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        var addr = nic.GetPhysicalAddress().ToString();
                        if (!string.IsNullOrWhiteSpace(addr)) return addr;
                    }
                }
            }
            catch { }
            return "MAC_UNKNOWN";
        }
    }

    public class LicenseBlock
    {
        public int LicenseId { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public string ExpireAt { get; set; } = string.Empty; // ISO format
        public int MaxMachines { get; set; } = 4;
        public List<string> RegisteredFingerprints { get; set; } = new();
        public string Signature { get; set; } = string.Empty;
        public string? LastServerCheckEncrypted { get; set; }
    }

    public class LicenseValidationService
    {
        private static readonly LicenseValidationService _instance = new();
        public static LicenseValidationService Instance => _instance;

        private const string PublicKeyXml = @"<RSAKeyValue><Modulus>ojILooC76YJpMh60RLTCKsgoxexHbG0fKZ0qt1TV32MEu3dDh8DWnuoVL8m6ZcJrS2GOG9837mqc/G435R2mO/+WwgvBysK93kZzrjZZ4iVBaRQ6VXsTeYd+Aj8WawQTkOhDujKC77qYMs2DETbsdp8GGcyf02NTzgV4C43SsYD2CTKdD5IgJ2okUBOIAq2G8lsqS294w6J0hLuelmfOLPd53mHZPX0ufnipU7jwJQp38zljvL5Sjp4QvrjJc3EWAVrunC3hR1LKZs7BLUbjdZWCyLSiBBaSsErW8pSU6KSEC9vjsgfrsCG5bpIvI8WkLelHmyc4uVFprb5InR5J9Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private readonly string _licenseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.lic");
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        private LicenseValidationService()
        {
        }

        public string GetServerUrl()
        {
            try
            {
                var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                var config = builder.Build();
                return config["LicenseServerUrl"] ?? "http://localhost:5000";
            }
            catch
            {
                return "http://localhost:5000";
            }
        }

        public string GetLocalFingerprint()
        {
            return MachineFingerprint.GetFingerprint();
        }

        private static readonly byte[] Entropy = new byte[] { 67, 51, 80, 97, 114, 107, 105, 110, 103, 76, 105, 99, 101, 110, 115, 101 }; // "C3ParkingLicense"

        public string EncryptDate(DateTime date)
        {
            try
            {
                string dateStr = date.ToString("O");
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(dateStr);
                byte[] ciphertextBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(ciphertextBytes);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"DPAPI Protect failed: {ex.Message}");
                return string.Empty;
            }
        }

        public DateTime? DecryptDate(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return null;
            try
            {
                byte[] ciphertextBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plaintextBytes = ProtectedData.Unprotect(ciphertextBytes, Entropy, DataProtectionScope.LocalMachine);
                string dateStr = Encoding.UTF8.GetString(plaintextBytes);
                if (DateTime.TryParse(dateStr, out var date))
                {
                    return date.ToUniversalTime();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"DPAPI Unprotect failed: {ex.Message}");
            }
            return null;
        }

        public void UpdateLastServerCheckTime()
        {
            try
            {
                if (!File.Exists(_licenseFilePath)) return;
                var json = File.ReadAllText(_licenseFilePath);
                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var license = JsonConvert.DeserializeObject<LicenseBlock>(json, settings);
                if (license != null)
                {
                    license.LastServerCheckEncrypted = EncryptDate(DateTime.UtcNow);
                    var updatedJson = JsonConvert.SerializeObject(license, Formatting.Indented);
                    File.WriteAllText(_licenseFilePath, updatedJson, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"Failed to update last server check time: {ex.Message}");
            }
        }

        public bool CheckLicenseOffline(out string errorMsg)
        {
            errorMsg = string.Empty;

            if (!File.Exists(_licenseFilePath))
            {
                errorMsg = "Không tìm thấy file bản quyền (license.lic). Hệ thống cần được kích hoạt.";
                return false;
            }

            try
            {
                var json = File.ReadAllText(_licenseFilePath);
                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var license = JsonConvert.DeserializeObject<LicenseBlock>(json, settings);

                if (license == null)
                {
                    errorMsg = "File bản quyền không đúng định dạng.";
                    return false;
                }

                // 1. Verify RSA Signature
                var sortedFingerprints = license.RegisteredFingerprints.OrderBy(f => f).ToList();
                var payload = $"{license.LicenseId}|{license.LicenseKey}|{license.ExpireAt}|{license.MaxMachines}|{string.Join(",", sortedFingerprints)}";

                bool isSignatureValid = false;
                using (var rsa = new RSACryptoServiceProvider(2048))
                {
                    rsa.FromXmlString(PublicKeyXml);
                    var payloadBytes = Encoding.UTF8.GetBytes(payload);
                    var sigBytes = Convert.FromBase64String(license.Signature);
                    isSignatureValid = rsa.VerifyData(payloadBytes, CryptoConfig.MapNameToOID("SHA256")!, sigBytes);
                }

                if (!isSignatureValid)
                {
                    errorMsg = "Bản quyền đã bị chỉnh sửa hoặc chữ ký không hợp lệ! Vui lòng liên hệ nhà quản trị.";
                    return false;
                }

                // 2. Check Expiration
                if (DateTime.TryParse(license.ExpireAt, out var expireDate))
                {
                    if (expireDate < DateTime.UtcNow)
                    {
                        errorMsg = $"Bản quyền đã hết hạn vào ngày: {expireDate.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";
                        return false;
                    }
                }
                else
                {
                    errorMsg = "Định dạng thời gian hết hạn không chính xác.";
                    return false;
                }

                // 3. Check machine limit
                if (license.MaxMachines <= 0)
                {
                    errorMsg = "Bản quyền không hợp lệ: Giới hạn thiết bị không hợp lệ.";
                    return false;
                }

                // 4. Verify machine fingerprint list count
                if (license.RegisteredFingerprints.Count > license.MaxMachines)
                {
                    errorMsg = $"Bản quyền bị vượt giới hạn số máy hoạt động ({license.RegisteredFingerprints.Count}/{license.MaxMachines}).";
                    return false;
                }

                // 5. Verify local fingerprint is in the registered list
                var localFingerprint = GetLocalFingerprint();
                if (!license.RegisteredFingerprints.Contains(localFingerprint))
                {
                    errorMsg = "Thiết bị này chưa được đăng ký trong danh sách bản quyền của License Key này.";
                    return false;
                }

                // 6. Decrypt and check Offline Grace Period
                DateTime? lastCheck = DecryptDate(license.LastServerCheckEncrypted ?? string.Empty);
                
                bool isServerOnline = TryCheckServerValidation(license.LicenseKey, out var serverError, out var wasContacted);
                
                if (wasContacted)
                {
                    if (!isServerOnline)
                    {
                        errorMsg = serverError;
                        try
                        {
                            if (File.Exists(_licenseFilePath))
                            {
                                File.Delete(_licenseFilePath);
                            }
                        }
                        catch { }
                        return false;
                    }
                    
                    // Server is online and verified -> update the timestamp!
                    UpdateLastServerCheckTime();
                }
                else
                {
                    // Server is offline/unreachable -> perform grace check
                    if (lastCheck == null)
                    {
                        errorMsg = "Hệ thống đang ngoại tuyến và không tìm thấy thông tin xác thực an toàn trước đó. Vui lòng kết nối Internet.";
                        return false;
                    }

                    if (DateTime.UtcNow < lastCheck.Value - TimeSpan.FromMinutes(10))
                    {
                        errorMsg = "Lỗi đồng hồ hệ thống: Thời gian hiện tại nhỏ hơn thời gian xác thực an toàn cuối cùng. Vui lòng cập nhật thời gian chính xác.";
                        return false;
                    }

                    var offlineDuration = DateTime.UtcNow - lastCheck.Value;
                    if (offlineDuration > TimeSpan.FromHours(24))
                    {
                        errorMsg = $"Đã quá giới hạn hoạt động ngoại tuyến (24 giờ). Thời gian ngoại tuyến hiện tại: {offlineDuration.TotalHours:F1} giờ. Vui lòng kết nối Internet.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = $"Lỗi kiểm tra bản quyền: {ex.Message}";
                return false;
            }
        }

        private bool TryCheckServerValidation(string licenseKey, out string serverError, out bool wasContacted)
        {
            serverError = string.Empty;
            wasContacted = false;
            try
            {
                var localFingerprint = GetLocalFingerprint();
                var requestBody = new
                {
                    LicenseKey = licenseKey,
                    MachineFingerprint = localFingerprint
                };

                var url = $"{GetServerUrl()}/api/license/validate";
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                using (var cts = new System.Threading.CancellationTokenSource(2000))
                {
                    var responseTask = _httpClient.PostAsync(url, content, cts.Token);
                    responseTask.Wait(cts.Token);
                    var response = responseTask.Result;

                    wasContacted = true;
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = response.Content.ReadAsStringAsync().Result;
                        var result = JsonConvert.DeserializeAnonymousType(responseString, new { IsValid = false, Message = "" });
                        if (result != null && !result.IsValid)
                        {
                            serverError = !string.IsNullOrEmpty(result.Message)
                                ? $"Bản quyền bị từ chối từ máy chủ: {result.Message}"
                                : "Bản quyền đã bị thu hồi hoặc thiết bị này đã bị xóa khỏi hệ thống.";
                            return false;
                        }
                    }
                    else
                    {
                        serverError = $"Máy chủ bản quyền báo lỗi: {response.StatusCode}";
                        return false;
                    }
                }
            }
            catch
            {
                wasContacted = false;
                return true; // Fallback to true, meaning we check offline grace period
            }
            return true;
        }

        public async Task<(bool Success, string ErrorMsg)> ActivateOnlineAsync(string licenseKey)
        {
            try
            {
                var localFingerprint = GetLocalFingerprint();
                var requestBody = new
                {
                    LicenseKey = licenseKey,
                    MachineFingerprint = localFingerprint
                };

                var url = $"{GetServerUrl()}/api/license/activate";
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string serverMsg = "Kích hoạt thất bại từ máy chủ.";
                    try
                    {
                        var errObj = JsonConvert.DeserializeAnonymousType(responseString, new { Message = "" });
                        if (errObj != null && !string.IsNullOrEmpty(errObj.Message))
                        {
                            serverMsg = errObj.Message;
                        }
                    }
                    catch { }

                    return (false, serverMsg);
                }

                var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var licenseBlock = JsonConvert.DeserializeObject<LicenseBlock>(responseString, settings);
                if (licenseBlock == null || string.IsNullOrEmpty(licenseBlock.Signature))
                {
                    return (false, "Không thể giải mã bản quyền trả về từ máy chủ.");
                }

                licenseBlock.LastServerCheckEncrypted = EncryptDate(DateTime.UtcNow);
                var updatedJson = JsonConvert.SerializeObject(licenseBlock, Formatting.Indented);

                File.WriteAllText(_licenseFilePath, updatedJson, Encoding.UTF8);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi kết nối máy chủ kích hoạt: {ex.Message}");
            }
        }
    }
}
