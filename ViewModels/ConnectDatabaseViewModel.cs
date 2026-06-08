using System;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class ConnectDatabaseViewModel : BaseViewModel
    {
        private DbConnectionConfig _config;

        public string ServerIP
        {
            get => _config.ServerIP;
            set { _config.ServerIP = value; OnPropertyChanged(nameof(ServerIP)); }
        }

        public string Port
        {
            get => _config.Port;
            set { _config.Port = value; OnPropertyChanged(nameof(Port)); }
        }

        public string Database
        {
            get => _config.Database;
            set { _config.Database = value; OnPropertyChanged(nameof(Database)); }
        }

        public string Username
        {
            get => _config.Username;
            set { _config.Username = value; OnPropertyChanged(nameof(Username)); }
        }

        // Password will be passed directly from View to keep it secure
        // but for simplicity and binding, we'll expose a property that View can set
        public string Password
        {
            get => _config.Password;
            set { _config.Password = value; OnPropertyChanged(nameof(Password)); }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        private bool _isSuccessStatus;
        public bool IsSuccessStatus
        {
            get => _isSuccessStatus;
            set { _isSuccessStatus = value; OnPropertyChanged(nameof(IsSuccessStatus)); }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set 
            { 
                _isProcessing = value; 
                OnPropertyChanged(nameof(IsProcessing)); 
                CommandManager.InvalidateRequerySuggested(); 
            }
        }

        private bool _isTestSuccessful;
        public bool IsTestSuccessful
        {
            get => _isTestSuccessful;
            set { _isTestSuccessful = value; OnPropertyChanged(nameof(IsTestSuccessful)); }
        }

        public ICommand PingServerCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand ConnectCommand { get; }

        public Action CloseAction { get; set; }
        public bool DialogResult { get; private set; }

        public ConnectDatabaseViewModel()
        {
            // Đọc config hiện tại từ ConnectionManager (không đọc file lại)
            var current = ConnectionManager.Instance.CurrentConfig;
            _config = new DbConnectionConfig
            {
                ServerIP = current.ServerIP,
                Port     = current.Port,
                Database = current.Database,
                Username = current.Username,
                Password = current.Password,
            };

            StatusMessage  = "Nhập thông tin và kiểm tra kết nối";
            IsSuccessStatus = true;

            PingServerCommand     = new RelayCommand(async _ => await PingServerAsync(), _ => !IsProcessing);
            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsProcessing);
            ConnectCommand        = new RelayCommand(_ => Connect(), _ => !IsProcessing && IsTestSuccessful);
        }

        private async Task PingServerAsync()
        {
            if (string.IsNullOrWhiteSpace(ServerIP))
            {
                SetStatus(false, "Vui lòng nhập IP máy chủ.");
                return;
            }

            IsProcessing = true;
            SetStatus(true, "Đang ping server...");
            LoggingService.Instance.LogInfo("ConnectDB", "Ping", $"Đang ping IP: {ServerIP}");

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ServerIP, 3000);
                
                if (reply.Status == IPStatus.Success)
                {
                    SetStatus(true, $"Ping thành công! Time={reply.RoundtripTime}ms");
                    LoggingService.Instance.LogInfo("ConnectDB", "Ping", $"Ping thành công đến {ServerIP}");
                }
                else
                {
                    SetStatus(false, $"Ping thất bại: {reply.Status}");
                    LoggingService.Instance.LogInfo("ConnectDB", "Ping", $"Ping thất bại: {reply.Status}");
                }
            }
            catch (Exception ex)
            {
                SetStatus(false, $"Lỗi ping: {ex.Message}");
                LoggingService.Instance.LogError("ConnectDB", "Ping", "Lỗi khi ping", ex);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task<bool> CheckConnectionAsync(string connectionString)
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task TestConnectionAsync()
        {
            IsProcessing = true;
            IsTestSuccessful = false;
            SetStatus(true, "Đang kết nối đến CSDL...");
            LoggingService.Instance.LogInfo("ConnectDB", "TestConnection", "Bắt đầu kiểm tra kết nối SQL");

            // Build temp connection string with 5 seconds timeout
            string connString = _config.BuildConnectionString(timeout: 5);

            try
            {
                using var conn = new SqlConnection(connString);
                await conn.OpenAsync();
                
                IsTestSuccessful = true;
                SetStatus(true, "Kết nối CSDL thành công!");
                LoggingService.Instance.LogInfo("ConnectDB", "TestConnection", "Kết nối SQL thành công");
            }
            catch (SqlException ex)
            {
                string errorDetail = "Lỗi kết nối CSDL.";
                if (ex.Number == 53) errorDetail = "Không tìm thấy máy chủ SQL (Sai IP/Port hoặc Server chưa mở).";
                else if (ex.Number == 18456) errorDetail = "Sai Username hoặc Password.";
                
                SetStatus(false, $"{errorDetail}\nChi tiết: {ex.Message}");
                LoggingService.Instance.LogError("ConnectDB", "TestConnection", $"Lỗi kết nối SQL. Mã lỗi: {ex.Number}", ex);
            }
            catch (Exception ex)
            {
                SetStatus(false, $"Lỗi hệ thống: {ex.Message}");
                LoggingService.Instance.LogError("ConnectDB", "TestConnection", "Lỗi kết nối SQL (General)", ex);
            }
            finally
            {
                IsProcessing = false;
                // Notify command manager to re-evaluate Connect command CanExecute
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void Connect()
        {
            // UpdateConnection = save file + cập nhật ConnectionManager + raise ConnectionChanged event
            // → DatabaseExplorerViewModel sẽ tự reload qua event handler
            ConnectionManager.Instance.UpdateConnection(_config);
            DialogResult = true;
            LoggingService.Instance.LogInfo("ConnectDB", "Connect",
                $"Đã đổi connection sang {_config.ServerIP}:{_config.Port}/{_config.Database}");
            CloseAction?.Invoke();
        }

        private void SetStatus(bool isSuccess, string msg)
        {
            IsSuccessStatus = isSuccess;
            StatusMessage = msg;
        }
    }
}
