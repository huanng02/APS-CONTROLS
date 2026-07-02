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
            string cpuId   = GetCpuId();
            string diskId  = GetDiskId();
            string macAddr = GetStableMacAddress();

            string raw = $"{cpuId.Trim()}#{diskId.Trim()}#{macAddr.Trim()}";
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Thu thập thông tin phần cứng đầy đủ để gửi lên server khi kích hoạt.
        /// </summary>
        public static Dictionary<string, string> GetHardwareInfo()
        {
            return new Dictionary<string, string>
            {
                ["MachineName"]  = Environment.MachineName,
                ["CpuId"]        = GetCpuId(),
                ["DiskSerial"]   = GetDiskId(),
                ["MacAddress"]   = GetStableMacAddress(),
                ["OsVersion"]    = Environment.OSVersion.VersionString,
                ["Fingerprint"]  = GetFingerprint()
            };
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

        /// <summary>
        /// Lấy MAC address ổn định nhất của máy.
        /// Ưu tiên: Ethernet vật lý (loại trừ WiFi, Bluetooth, Virtual).
        /// Chọn interface có tốc độ cao nhất trong cùng loại để đảm bảo tính nhất quán.
        /// </summary>
        private static string GetStableMacAddress()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic =>
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        nic.GetPhysicalAddress() != null &&
                        nic.GetPhysicalAddress().ToString().Length >= 12)
                    .Where(nic =>
                    {
                        // Loại bỏ interface ảo / không ổn định
                        var desc = nic.Description.ToLowerInvariant();
                        var name = nic.Name.ToLowerInvariant();
                        return !desc.Contains("virtual") &&
                               !desc.Contains("vmware") &&
                               !desc.Contains("virtualbox") &&
                               !desc.Contains("hyper-v") &&
                               !desc.Contains("bluetooth") &&
                               !desc.Contains("vpn") &&
                               !desc.Contains("pseudo") &&
                               !desc.Contains("miniport") &&
                               !desc.Contains("wan") &&
                               !name.Contains("vethernet") &&
                               !name.Contains("loopback");
                    })
                    .ToList();

                // Ưu tiên 1: Ethernet vật lý (cáp)
                var ethernet = candidates
                    .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .OrderByDescending(n => n.Speed)  // chọn cái tốc độ cao nhất → ổn định nhất
                    .FirstOrDefault();
                if (ethernet != null)
                    return ethernet.GetPhysicalAddress().ToString();

                // Ưu tiên 2: Bất kỳ interface nào khác (loại trừ WiFi)
                var other = candidates
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    .OrderByDescending(n => n.Speed)
                    .FirstOrDefault();
                if (other != null)
                    return other.GetPhysicalAddress().ToString();

                // Fallback cuối: WiFi (ít ổn định nhất)
                var wifi = candidates
                    .OrderByDescending(n => n.Speed)
                    .FirstOrDefault();
                if (wifi != null)
                    return wifi.GetPhysicalAddress().ToString();
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
        private readonly string _offlineLicenseFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "APS", "license.bin");
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Thời gian tối đa máy client được hoạt động offline mà không cần liên lạc server.
        /// 72h = 3 ngày (đủ để qua weekend / mất điện server ngắn hạn).
        /// </summary>
        private const int OfflineGraceHours = 72;

        private LicenseValidationService() { }

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

        public string GetLocalFingerprint() => MachineFingerprint.GetFingerprint();

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
                byte[] plaintextBytes  = ProtectedData.Unprotect(ciphertextBytes, Entropy, DataProtectionScope.LocalMachine);
                string dateStr = Encoding.UTF8.GetString(plaintextBytes);
                if (DateTime.TryParse(dateStr, out var date))
                    return date.ToUniversalTime();
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

        public void UpdateOfflineLicenseLastTimeUsed()
        {
            try
            {
                if (!File.Exists(_offlineLicenseFilePath)) return;
                var json = File.ReadAllText(_offlineLicenseFilePath, Encoding.UTF8);
                var license = JsonConvert.DeserializeObject<OfflineLicense>(json);
                if (license != null)
                {
                    license.LastTimeUsedEncrypted = EncryptDate(DateTime.UtcNow);
                    var updatedJson = JsonConvert.SerializeObject(license, Formatting.Indented);
                    File.WriteAllText(_offlineLicenseFilePath, updatedJson, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"Failed to update offline license last time used: {ex.Message}");
            }
        }

        public double CalculateHardwareMatchScore(HardwareDetails local, HardwareDetails licensed)
        {
            double score = 0;
            if (string.Equals(local.Motherboard, licensed.Motherboard, StringComparison.OrdinalIgnoreCase)) score += 50;
            if (string.Equals(local.Cpu, licensed.Cpu, StringComparison.OrdinalIgnoreCase)) score += 30;
            if (string.Equals(local.Disk, licensed.Disk, StringComparison.OrdinalIgnoreCase)) score += 15;
            if (string.Equals(local.Bios, licensed.Bios, StringComparison.OrdinalIgnoreCase)) score += 5;
            return score;
        }

        public bool VerifyOfflineLicense(OfflineLicense license, out string errorMsg)
        {
            errorMsg = string.Empty;

            if (license == null)
            {
                errorMsg = "File bản quyền không hợp lệ hoặc bị lỗi (Corrupted).";
                LicenseManager.CurrentStatus = LicenseStatus.Corrupted;
                return false;
            }

            if (license.Version != 1)
            {
                errorMsg = $"Phiên bản bản quyền không được hỗ trợ (Version: {license.Version}).";
                LicenseManager.CurrentStatus = LicenseStatus.Invalid;
                return false;
            }

            // 1. Verify RSA Signature
            try
            {
                var sortedFeatures = license.Features.OrderBy(f => f).ToList();
                var payload = $"{license.Version}|{license.Product}|{license.Customer}|{license.MachineId}|{license.LicenseType}|{string.Join(",", sortedFeatures)}|{license.Hardware.Cpu}|{license.Hardware.Motherboard}|{license.Hardware.Disk}|{license.Hardware.Bios}|{license.CreatedDate}|{license.ExpirationDate}";

                bool isSignatureValid = false;
                using (var rsa = new RSACryptoServiceProvider(2048))
                {
                    rsa.FromXmlString(PublicKeyXml);
                    var payloadBytes = Encoding.UTF8.GetBytes(payload);
                    var sigBytes     = Convert.FromBase64String(license.Signature);
                    isSignatureValid = rsa.VerifyData(payloadBytes, CryptoConfig.MapNameToOID("SHA256")!, sigBytes);
                }

                if (!isSignatureValid)
                {
                    errorMsg = "Chữ ký bản quyền không hợp lệ hoặc đã bị thay đổi (SignatureInvalid).";
                    LicenseManager.CurrentStatus = LicenseStatus.SignatureInvalid;
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMsg = $"Lỗi xác thực chữ ký bản quyền: {ex.Message}";
                LicenseManager.CurrentStatus = LicenseStatus.SignatureInvalid;
                return false;
            }

            // 2. Verify Hardware Match
            var localHw = HardwareFingerprintService.GetHardwareDetails();
            double score = CalculateHardwareMatchScore(localHw, license.Hardware);
            if (score < 70)
            {
                errorMsg = $"Lỗi phần cứng không khớp (Hardware Match Score: {score}% < 70%).";
                LicenseManager.CurrentStatus = LicenseStatus.HardwareMismatch;
                
                Serilog.Log.Warning("LICENSE_HARDWARE_MISMATCH: Score {Score}%. " +
                                    "Local: Cpu={LCpu}, Mobo={LMob}, Disk={LDisk}, Bios={LBios}. " +
                                    "Licensed: Cpu={RCpu}, Mobo={RMob}, Disk={RDisk}, Bios={RBios}.",
                                    score, localHw.Cpu, localHw.Motherboard, localHw.Disk, localHw.Bios,
                                    license.Hardware.Cpu, license.Hardware.Motherboard, license.Hardware.Disk, license.Hardware.Bios);
                return false;
            }

            // 3. Check Expiration and Clock Tampering
            if (DateTime.TryParse(license.CreatedDate, out var createdDate))
            {
                if (DateTime.UtcNow < createdDate.ToUniversalTime() - TimeSpan.FromMinutes(10))
                {
                    errorMsg = "Lỗi đồng hồ hệ thống: Thời gian hiện tại nhỏ hơn thời gian bắt đầu bản quyền. Vui lòng cập nhật thời gian chính xác.";
                    LicenseManager.CurrentStatus = LicenseStatus.Invalid;
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(license.LastTimeUsedEncrypted))
            {
                var lastTimeUsed = DecryptDate(license.LastTimeUsedEncrypted);
                if (lastTimeUsed.HasValue)
                {
                    if (DateTime.UtcNow < lastTimeUsed.Value - TimeSpan.FromMinutes(10))
                    {
                        errorMsg = "Lỗi đồng hồ hệ thống: Thời gian hiện tại nhỏ hơn thời gian sử dụng gần nhất. Vui lòng cập nhật thời gian chính xác.";
                        LicenseManager.CurrentStatus = LicenseStatus.Invalid;
                        return false;
                    }
                }
            }

            if (DateTime.TryParse(license.ExpirationDate, out var expireDate))
            {
                if (expireDate.ToUniversalTime() < DateTime.UtcNow)
                {
                    errorMsg = $"Bản quyền ngoại tuyến đã hết hạn sử dụng vào ngày: {expireDate.ToLocalTime():yyyy-MM-dd HH:mm:ss} (Expired).";
                    LicenseManager.CurrentStatus = LicenseStatus.Expired;
                    return false;
                }
            }
            else
            {
                errorMsg = "Định dạng thời gian hết hạn không chính xác.";
                LicenseManager.CurrentStatus = LicenseStatus.Invalid;
                return false;
            }

            // Activated successfully
            LicenseManager.CurrentLicense = license;
            LicenseManager.CurrentStatus = LicenseStatus.Activated;
            return true;
        }

        public (bool Success, string ErrorMsg) ImportOfflineLicense(string sourceFilePath)
        {
            try
            {
                if (!File.Exists(sourceFilePath))
                {
                    return (false, "Không tìm thấy file bản quyền nguồn.");
                }

                string json = File.ReadAllText(sourceFilePath, Encoding.UTF8);
                var license = JsonConvert.DeserializeObject<OfflineLicense>(json);
                
                string errorMsg;
                if (!VerifyOfflineLicense(license, out errorMsg))
                {
                    return (false, errorMsg);
                }

                string dir = Path.GetDirectoryName(_offlineLicenseFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(_offlineLicenseFilePath, json, Encoding.UTF8);
                Serilog.Log.Information("LICENSE_IMPORT_SUCCESS: Activated successfully offline for customer {Customer}.", license.Customer);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"LICENSE_IMPORT_EXCEPTION: {ex.Message}");
                return (false, $"Lỗi nhập bản quyền: {ex.Message}");
            }
        }

        public bool CheckLicenseOffline(out string errorMsg)
        {
            errorMsg = string.Empty;

            // ── FLOW 1: Check Offline license.bin ──────────────────────────────────
            if (File.Exists(_offlineLicenseFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_offlineLicenseFilePath, Encoding.UTF8);
                    var license = JsonConvert.DeserializeObject<OfflineLicense>(json);
                    
                    if (VerifyOfflineLicense(license, out errorMsg))
                    {
                        Serilog.Log.Information("LICENSE_VALID_OFFLINE: Offline license is active and validated.");
                        try
                        {
                            license.LastTimeUsedEncrypted = EncryptDate(DateTime.UtcNow);
                            var updatedJson = JsonConvert.SerializeObject(license, Formatting.Indented);
                            File.WriteAllText(_offlineLicenseFilePath, updatedJson, Encoding.UTF8);
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error($"Failed to update last time used in offline license: {ex.Message}");
                        }
                        return true;
                    }
                    else
                    {
                        Serilog.Log.Warning("LICENSE_INVALID_OFFLINE: {Error}", errorMsg);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMsg = $"File bản quyền bị hỏng hoặc không đúng định dạng (Corrupted). Chi tiết: {ex.Message}";
                    LicenseManager.CurrentStatus = LicenseStatus.Corrupted;
                    Serilog.Log.Error("LICENSE_READ_OFFLINE_ERROR: {Error}", ex.Message);
                    return false;
                }
            }

            // ── FLOW 2: Fallback to old Online license.lic (Backward Compatibility) ──
            if (!File.Exists(_licenseFilePath))
            {
                errorMsg = "Không tìm thấy file bản quyền (license.bin hoặc license.lic). Hệ thống cần được kích hoạt.";
                LicenseManager.CurrentStatus = LicenseStatus.NotActivated;
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
                    LicenseManager.CurrentStatus = LicenseStatus.Corrupted;
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
                    var sigBytes     = Convert.FromBase64String(license.Signature);
                    isSignatureValid = rsa.VerifyData(payloadBytes, CryptoConfig.MapNameToOID("SHA256")!, sigBytes);
                }

                if (!isSignatureValid)
                {
                    errorMsg = "Bản quyền đã bị chỉnh sửa hoặc chữ ký không hợp lệ! Vui lòng liên hệ nhà quản trị.";
                    LicenseManager.CurrentStatus = LicenseStatus.SignatureInvalid;
                    return false;
                }

                // 2. Check Expiration
                if (DateTime.TryParse(license.ExpireAt, out var expireDate))
                {
                    if (expireDate.ToUniversalTime() < DateTime.UtcNow)
                    {
                        errorMsg = $"Bản quyền đã hết hạn vào ngày: {expireDate.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";
                        LicenseManager.CurrentStatus = LicenseStatus.Expired;
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
                    // ── KHÔNG XÓA file license.lic ──────────────────────────────────────
                    // Trước đây: xóa file → buộc nhập lại key → tạo device mới trong DB
                    // Bây giờ: chỉ báo lỗi, để App hiển thị dialog kích hoạt lại với key hiện tại
                    var hw = MachineFingerprint.GetHardwareInfo();
                    Serilog.Log.Warning(
                        "LICENSE_FINGERPRINT_MISMATCH: Fingerprint máy này ({Fingerprint}) không khớp danh sách đã đăng ký. " +
                        "MachineName={MachineName}, CPU={CpuId}, Disk={DiskSerial}, MAC={MacAddress}, OS={OsVersion}",
                        localFingerprint,
                        hw.GetValueOrDefault("MachineName"),
                        hw.GetValueOrDefault("CpuId"),
                        hw.GetValueOrDefault("DiskSerial"),
                        hw.GetValueOrDefault("MacAddress"),
                        hw.GetValueOrDefault("OsVersion"));

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
                        // Server nói invalid (bị thu hồi, hết hạn trên server...) → block nhưng KHÔNG xóa file
                        // Để lại file để user có thể xem thông tin và liên hệ admin
                        Serilog.Log.Warning("LICENSE_SERVER_REJECTED: {Error}", serverError);
                        errorMsg = serverError;
                        return false;
                    }

                    // Server online và xác nhận hợp lệ → cập nhật timestamp
                    UpdateLastServerCheckTime();
                }
                else
                {
                    // Server offline/không kết nối được → kiểm tra grace period
                    if (lastCheck == null)
                    {
                        errorMsg = "Hệ thống đang ngoại tuyến và không tìm thấy thông tin xác thực an toàn trước đó. Vui lòng kết nối Internet để kích hoạt lần đầu.";
                        return false;
                    }

                    if (DateTime.UtcNow < lastCheck.Value - TimeSpan.FromMinutes(10))
                    {
                        errorMsg = "Lỗi đồng hồ hệ thống: Thời gian hiện tại nhỏ hơn thời gian xác thực an toàn cuối cùng. Vui lòng cập nhật thời gian chính xác.";
                        return false;
                    }

                    var offlineDuration = DateTime.UtcNow - lastCheck.Value;
                    if (offlineDuration > TimeSpan.FromHours(OfflineGraceHours))
                    {
                        errorMsg = $"Đã quá giới hạn hoạt động ngoại tuyến ({OfflineGraceHours} giờ). " +
                                   $"Thời gian ngoại tuyến hiện tại: {offlineDuration.TotalHours:F1} giờ. " +
                                   $"Vui lòng kết nối Internet để xác thực lại bản quyền.";
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
            serverError  = string.Empty;
            wasContacted = false;
            try
            {
                var localFingerprint = GetLocalFingerprint();
                var requestBody = new
                {
                    LicenseKey         = licenseKey,
                    MachineFingerprint = localFingerprint
                };

                var url     = $"{GetServerUrl()}/api/license/validate";
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
                return true; // Server không liên lạc được → dùng grace period
            }
            return true;
        }

        public async Task<(bool Success, string ErrorMsg)> ActivateOnlineAsync(string licenseKey)
        {
            try
            {
                var localFingerprint = GetLocalFingerprint();
                var hardwareInfo     = MachineFingerprint.GetHardwareInfo();

                // Log hardware info khi kích hoạt để có thể debug sau này
                Serilog.Log.Information(
                    "LICENSE_ACTIVATE: Attempting activation for key {Key}. " +
                    "Fingerprint={Fingerprint}, Machine={MachineName}, CPU={CpuId}, Disk={DiskSerial}, MAC={MacAddress}, OS={OsVersion}",
                    licenseKey,
                    localFingerprint,
                    hardwareInfo.GetValueOrDefault("MachineName"),
                    hardwareInfo.GetValueOrDefault("CpuId"),
                    hardwareInfo.GetValueOrDefault("DiskSerial"),
                    hardwareInfo.GetValueOrDefault("MacAddress"),
                    hardwareInfo.GetValueOrDefault("OsVersion"));

                var requestBody = new
                {
                    LicenseKey         = licenseKey,
                    MachineFingerprint = localFingerprint,
                    HardwareInfo       = hardwareInfo     // Gửi thông tin phần cứng lên server
                };

                var url      = $"{GetServerUrl()}/api/license/activate";
                var content  = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string serverMsg = "Kích hoạt thất bại từ máy chủ.";
                    try
                    {
                        var errObj = JsonConvert.DeserializeAnonymousType(responseString, new { Message = "" });
                        if (errObj != null && !string.IsNullOrEmpty(errObj.Message))
                            serverMsg = errObj.Message;
                    }
                    catch { }

                    return (false, serverMsg);
                }

                var settings     = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var licenseBlock = JsonConvert.DeserializeObject<LicenseBlock>(responseString, settings);
                if (licenseBlock == null || string.IsNullOrEmpty(licenseBlock.Signature))
                    return (false, "Không thể giải mã bản quyền trả về từ máy chủ.");

                licenseBlock.LastServerCheckEncrypted = EncryptDate(DateTime.UtcNow);
                var updatedJson = JsonConvert.SerializeObject(licenseBlock, Formatting.Indented);
                File.WriteAllText(_licenseFilePath, updatedJson, Encoding.UTF8);

                Serilog.Log.Information("LICENSE_ACTIVATE_SUCCESS: Key {Key} activated for fingerprint {Fingerprint}", licenseKey, localFingerprint);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi kết nối máy chủ kích hoạt: {ex.Message}");
            }
        }
    }
}
