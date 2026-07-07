using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Singleton – nguồn sự thật duy nhất cho connection string trong toàn bộ ứng dụng.
    /// Khi connection thay đổi, raise event ConnectionChanged để các subscriber tự reload.
    /// </summary>
    public sealed class ConnectionManager
    {
        // ──────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────
        private static readonly Lazy<ConnectionManager> _lazy =
            new Lazy<ConnectionManager>(() => new ConnectionManager());

        public static ConnectionManager Instance => _lazy.Value;

        // ──────────────────────────────────────────────
        // State
        // ──────────────────────────────────────────────
        private DbConnectionConfig _currentConfig;

        /// <summary>Config hiện tại (ServerIP, Port, Database, …)</summary>
        public DbConnectionConfig CurrentConfig => _currentConfig;

        /// <summary>Connection string đã build sẵn, dùng để tạo SqlConnection.</summary>
        public string CurrentConnectionString { get; private set; }

        // ──────────────────────────────────────────────
        // Event
        // ──────────────────────────────────────────────

        /// <summary>
        /// Được raise sau khi connection string thay đổi thành công và đã lưu vào file.
        /// ViewModel lắng nghe event này để reload dữ liệu.
        /// </summary>
        public event EventHandler ConnectionChanged;

        // ──────────────────────────────────────────────
        // Constructor – private, load config từ file
        // ──────────────────────────────────────────────
        private ConnectionManager()
        {
            _currentConfig = DbConnectionConfig.LoadFromFile();
            // Giải mã mật khẩu DPAPI sang dạng thô chạy trong bộ nhớ
            _currentConfig.Password = CredentialEncryptionService.Decrypt(_currentConfig.Password);
            _currentConfig.SecondaryPassword = CredentialEncryptionService.Decrypt(_currentConfig.SecondaryPassword);
            CurrentConnectionString = _currentConfig.BuildConnectionString();
            LoggingService.Instance.LogInfo("ConnectionManager", "Init",
                $"Đã load config: {_currentConfig.ServerIP}:{_currentConfig.Port}/{_currentConfig.Database}");
        }

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Tạo và mở SqlConnection MỚI từ connection string hiện tại.
        /// Caller phải đặt trong using block.
        /// KHÔNG giữ connection dùng lại.
        /// </summary>
        public SqlConnection GetOpenConnection()
        {
            var conn = new SqlConnection(CurrentConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Tạo SqlConnection MỚI (async). Caller phải đặt trong using block.
        /// </summary>
        public async Task<SqlConnection> GetOpenConnectionAsync()
        {
            var conn = new SqlConnection(CurrentConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        /// <summary>
        /// Kiểm tra kết nối với config được truyền vào (không thay đổi config hiện tại).
        /// Dùng timeout ngắn 5 giây để không block UI.
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> TestConnectionAsync(DbConnectionConfig config)
        {
            string connStr = config.BuildConnectionString(timeout: 5);
            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                return (true, string.Empty);
            }
            catch (SqlException ex)
            {
                string detail = ex.Number switch
                {
                    53    => "Không tìm thấy máy chủ SQL (sai IP/Port hoặc server chưa bật).",
                    18456 => "Sai Username hoặc Password.",
                    4060  => "Database không tồn tại hoặc không có quyền truy cập.",
                    _     => ex.Message
                };
                return (false, detail);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Cập nhật connection mới: lưu file → cập nhật state → raise ConnectionChanged.
        /// Phải gọi sau khi TestConnection đã thành công.
        /// </summary>
        public void UpdateConnection(DbConnectionConfig newConfig)
        {
            // Đảm bảo lưu file luôn là bản mật khẩu được mã hóa DPAPI an toàn
            var configToSave = new DbConnectionConfig
            {
                ServerIP = newConfig.ServerIP,
                Port     = newConfig.Port,
                Database = newConfig.Database,
                Username = newConfig.Username,
                Password = CredentialEncryptionService.Encrypt(CredentialEncryptionService.Decrypt(newConfig.Password)),

                SecondaryServerIP = newConfig.SecondaryServerIP,
                SecondaryPort     = newConfig.SecondaryPort,
                SecondaryDatabase = newConfig.SecondaryDatabase,
                SecondaryUsername = newConfig.SecondaryUsername,
                SecondaryPassword = string.IsNullOrEmpty(newConfig.SecondaryPassword) ? "" : CredentialEncryptionService.Encrypt(CredentialEncryptionService.Decrypt(newConfig.SecondaryPassword))
            };
            DbConnectionConfig.SaveToFile(configToSave);

            // Cập nhật cấu hình thô chạy trong bộ nhớ
            _currentConfig = newConfig;
            _currentConfig.Password = CredentialEncryptionService.Decrypt(newConfig.Password);
            _currentConfig.SecondaryPassword = CredentialEncryptionService.Decrypt(newConfig.SecondaryPassword);
            CurrentConnectionString = _currentConfig.BuildConnectionString();

            LoggingService.Instance.LogInfo("ConnectionManager", "UpdateConnection",
                $"Connection đã đổi sang: {_currentConfig.ServerIP}:{_currentConfig.Port}/{_currentConfig.Database}");

            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SwitchToPrimary()
        {
            string primaryConnStr = _currentConfig.BuildConnectionString();
            if (CurrentConnectionString != primaryConnStr)
            {
                CurrentConnectionString = primaryConnStr;
                LoggingService.Instance.LogInfo("ConnectionManager", "Switch", "Switched database connection string to Primary Server.");
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SwitchToSecondary()
        {
            string secondaryConnStr = _currentConfig.BuildSecondaryConnectionString();
            if (!string.IsNullOrEmpty(secondaryConnStr) && CurrentConnectionString != secondaryConnStr)
            {
                CurrentConnectionString = secondaryConnStr;
                LoggingService.Instance.LogInfo("ConnectionManager", "Switch", "Switched database connection string to Secondary Server.");
                ConnectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Trả về chuỗi hiển thị người dùng, ví dụ: "Connected to: 192.168.2.13:1433 / BaiXe"
        /// </summary>
        public string GetDisplayInfo()
            => $"Connected to: {_currentConfig.ServerIP}:{_currentConfig.Port} / {_currentConfig.Database}";
    }
}
