using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RecentConnectionItem
    {
        public string ServerIP { get; set; } = "";
        public string Port { get; set; } = "1433";
        public string Username { get; set; } = "";
        public string Database { get; set; } = "";
        public string DisplayText => $"{ServerIP}:{Port} - {Database}";
    }

    public class ConnectDatabaseViewModel : BaseViewModel
    {
        private ConnectionConfig _config;

        public string ServerIP
        {
            get => _config.ServerIP;
            set 
            { 
                _config.ServerIP = value; 
                OnPropertyChanged(nameof(ServerIP)); 
                IsTestSuccessful = false;
            }
        }

        public string Port
        {
            get => _config.Port;
            set 
            { 
                _config.Port = value; 
                OnPropertyChanged(nameof(Port)); 
                IsTestSuccessful = false;
            }
        }

        public string Database
        {
            get => _config.Database;
            set 
            { 
                _config.Database = value; 
                OnPropertyChanged(nameof(Database)); 
                IsTestSuccessful = false;
            }
        }

        public string Username
        {
            get => _config.Username;
            set 
            { 
                _config.Username = value; 
                OnPropertyChanged(nameof(Username)); 
                IsTestSuccessful = false;
            }
        }

        // Mật khẩu thực tế chạy trong session (thô)
        public string Password
        {
            get => _config.Password;
            set 
            { 
                _config.Password = value; 
                OnPropertyChanged(nameof(Password)); 
                IsTestSuccessful = false;
            }
        }

        public string SecondaryServerIP
        {
            get => _config.SecondaryServerIP;
            set 
            { 
                _config.SecondaryServerIP = value; 
                OnPropertyChanged(nameof(SecondaryServerIP)); 
                IsTestSuccessful = false;
            }
        }

        public string SecondaryPort
        {
            get => _config.SecondaryPort;
            set 
            { 
                _config.SecondaryPort = value; 
                OnPropertyChanged(nameof(SecondaryPort)); 
                IsTestSuccessful = false;
            }
        }

        public string SecondaryDatabase
        {
            get => _config.SecondaryDatabase;
            set 
            { 
                _config.SecondaryDatabase = value; 
                OnPropertyChanged(nameof(SecondaryDatabase)); 
                IsTestSuccessful = false;
            }
        }

        public string SecondaryUsername
        {
            get => _config.SecondaryUsername;
            set 
            { 
                _config.SecondaryUsername = value; 
                OnPropertyChanged(nameof(SecondaryUsername)); 
                IsTestSuccessful = false;
            }
        }

        public string SecondaryPassword
        {
            get => _config.SecondaryPassword;
            set 
            { 
                _config.SecondaryPassword = value; 
                OnPropertyChanged(nameof(SecondaryPassword)); 
                IsTestSuccessful = false;
            }
        }

        private ObservableCollection<DatabaseItem> _databases = new ObservableCollection<DatabaseItem>();
        public ObservableCollection<DatabaseItem> Databases
        {
            get => _databases;
            set { _databases = value; OnPropertyChanged(nameof(Databases)); }
        }

        private DatabaseItem _selectedDatabaseItem;
        private CancellationTokenSource _dbValidationCts;
        public DatabaseItem SelectedDatabaseItem
        {
            get => _selectedDatabaseItem;
            set
            {
                _selectedDatabaseItem = value;
                OnPropertyChanged(nameof(SelectedDatabaseItem));
                if (value != null)
                {
                    Database = value.Name;
                    
                    // Cancel the previous validation task to prevent race conditions and background task pileups
                    try
                    {
                        _dbValidationCts?.Cancel();
                        _dbValidationCts?.Dispose();
                    }
                    catch { }
                    
                    _dbValidationCts = new CancellationTokenSource();
                    var token = _dbValidationCts.Token;

                    // Chạy logic kiểm tra sâu database được chọn
                    Task.Run(async () => await ValidateSelectedDatabaseAsync(value, token), token);
                }
            }
        }

        private ObservableCollection<RecentConnectionItem> _recentConnections = new ObservableCollection<RecentConnectionItem>();
        public ObservableCollection<RecentConnectionItem> RecentConnections
        {
            get => _recentConnections;
            set { _recentConnections = value; OnPropertyChanged(nameof(RecentConnections)); }
        }

        private RecentConnectionItem _selectedRecentConnection;
        public RecentConnectionItem SelectedRecentConnection
        {
            get => _selectedRecentConnection;
            set
            {
                _selectedRecentConnection = value;
                OnPropertyChanged(nameof(SelectedRecentConnection));
                if (value != null)
                {
                    SelectRecentConnection(value);
                }
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        private string _statusState = "warning"; // "success", "warning", "loading", "error"
        public string StatusState
        {
            get => _statusState;
            set { _statusState = value; OnPropertyChanged(nameof(StatusState)); }
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
                RunOnUI(() =>
                {
                    _isProcessing = value; 
                    OnPropertyChanged(nameof(IsProcessing)); 
                    CommandManager.InvalidateRequerySuggested(); 
                });
            }
        }

        private string _loadingMessage = "Đang xử lý...";
        public string LoadingMessage
        {
            get => _loadingMessage;
            set { RunOnUI(() => { _loadingMessage = value; OnPropertyChanged(nameof(LoadingMessage)); }); }
        }

        private bool _isTestSuccessful;
        public bool IsTestSuccessful
        {
            get => _isTestSuccessful;
            set { _isTestSuccessful = value; OnPropertyChanged(nameof(IsTestSuccessful)); }
        }

        private bool _isDatabaseComboEnabled;
        public bool IsDatabaseComboEnabled
        {
            get => _isDatabaseComboEnabled;
            set { _isDatabaseComboEnabled = value; OnPropertyChanged(nameof(IsDatabaseComboEnabled)); }
        }

        private bool _isPasswordVisible;
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set { _isPasswordVisible = value; OnPropertyChanged(nameof(IsPasswordVisible)); }
        }

        private bool _rememberConnection = true;
        public bool RememberConnection
        {
            get => _rememberConnection;
            set { _rememberConnection = value; OnPropertyChanged(nameof(RememberConnection)); }
        }
        public ICommand PingServerCommand { get; }
        public ICommand LoadDatabasesCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }

        public Action CloseAction { get; set; }
        public bool DialogResult { get; private set; }
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public void CancelPendingTasks()
        {
            try
            {
                _cts.Cancel();
                try
                {
                    _dbValidationCts?.Cancel();
                    _dbValidationCts?.Dispose();
                }
                catch { }
                LoggingService.Instance.LogInfo("ConnectDB", "CancelPendingTasks", "Đã gửi tín hiệu hủy toàn bộ tác vụ kết nối SQL Server đang chạy nền.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConnectDB", "CancelPendingTasks", "Lỗi khi hủy tác vụ chạy ngầm", ex);
            }
        }

        public ConnectDatabaseViewModel()
        {
            // Đọc cấu hình hiện tại từ ConnectionManager
            var current = ConnectionManager.Instance.CurrentConfig;
            
            // Giải mã mật khẩu an toàn từ file dbconfig.json bằng DPAPI
            string decryptedPassword = CredentialEncryptionService.Decrypt(current.Password);
            string decryptedSecondaryPassword = CredentialEncryptionService.Decrypt(current.SecondaryPassword);

            _config = new ConnectionConfig
            {
                ServerIP = current.ServerIP,
                Port     = current.Port,
                Database = current.Database,
                Username = current.Username,
                Password = decryptedPassword,
                RememberConnection = true,
                SecondaryServerIP = current.SecondaryServerIP,
                SecondaryPort = current.SecondaryPort,
                SecondaryDatabase = current.SecondaryDatabase,
                SecondaryUsername = current.SecondaryUsername,
                SecondaryPassword = decryptedSecondaryPassword
            };

            StatusMessage  = "Chưa kết nối";
            StatusState = "warning";
            IsSuccessStatus = false;
            IsDatabaseComboEnabled = false;

            PingServerCommand         = new RelayCommand(async _ => await PingServerAsync(), _ => !IsProcessing);
            LoadDatabasesCommand      = new RelayCommand(async _ => await LoadDatabasesAsync(showSuccessStatus: true), _ => !IsProcessing);
            TestConnectionCommand     = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsProcessing);
            ConnectCommand            = new RelayCommand(_ => Connect(), _ => !IsProcessing && IsTestSuccessful);
            TogglePasswordVisibilityCommand = new RelayCommand(_ => IsPasswordVisible = !IsPasswordVisible);

            // Tải danh sách kết nối gần đây
            LoadRecentConnections();

            // Tải ngầm danh sách database khi khởi tạo nếu đã có sẵn IP và User
            if (!string.IsNullOrWhiteSpace(ServerIP) && !string.IsNullOrWhiteSpace(Username))
            {
                Task.Run(async () => await LoadDatabasesAsync(showSuccessStatus: false, silent: true));
            }
        }

        private async Task PingServerAsync()
        {
            if (string.IsNullOrWhiteSpace(ServerIP))
            {
                SetStatus("error", "Vui lòng nhập IP máy chủ.");
                return;
            }
            if (_cts.Token.IsCancellationRequested) return;

            IsProcessing = true;
            LoadingMessage = "Đang kết nối SQL Server...";
            SetStatus("loading", "Đang kiểm tra server...");
            LoggingService.Instance.LogInfo("ConnectDB", "Ping", $"Đang ping IP: {ServerIP}");

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ServerIP, 2000);
                if (_cts.Token.IsCancellationRequested) return;
                
                if (reply.Status == IPStatus.Success)
                {
                    LoggingService.Instance.LogInfo("ConnectDB", "Ping", $"Ping thành công đến {ServerIP}");
                    SetStatus("loading", "Đã kết nối IP. Đang tải danh sách cơ sở dữ liệu...");
                    await LoadDatabasesAsync(showSuccessStatus: false, automaticFromPing: true);
                }
                else
                {
                    SetStatus("error", "Không tìm thấy SQL Server");
                }
            }
            catch (Exception ex)
            {
                if (_cts.Token.IsCancellationRequested) return;
                SetStatus("error", "Không tìm thấy SQL Server");
                LoggingService.Instance.LogError("ConnectDB", "Ping", "Lỗi khi ping", ex);
            }
            finally
            {
                if (!_cts.Token.IsCancellationRequested)
                {
                    IsProcessing = false;
                }
            }
        }

        private async Task LoadDatabasesAsync(bool showSuccessStatus = false, bool automaticFromPing = false, bool silent = false)
        {
            if (string.IsNullOrWhiteSpace(ServerIP))
            {
                if (!silent) SetStatus("error", "Vui lòng nhập IP máy chủ.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Username))
            {
                if (!silent) SetStatus("error", "Vui lòng nhập tài khoản SQL.");
                return;
            }
            if (_cts.Token.IsCancellationRequested) return;

            bool wasProcessing = IsProcessing;
            if (!wasProcessing) IsProcessing = true;
            
            LoadingMessage = "Đang tải danh sách database...";
            if (!automaticFromPing) SetStatus("loading", "Đang tải danh sách databases...");

            try
            {
                var dbs = await DatabaseDiscoveryService.DiscoverDatabasesAsync(_config, _cts.Token);
                if (_cts.Token.IsCancellationRequested) return;
                
                var dbItems = new ObservableCollection<DatabaseItem>();

                foreach (var dbName in dbs)
                {
                    dbItems.Add(new DatabaseItem
                    {
                        Name = dbName,
                        Status = DatabaseStatus.Unknown
                    });
                }

                if (_cts.Token.IsCancellationRequested) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (_cts.Token.IsCancellationRequested) return;
                    
                    Databases = dbItems;
                    IsDatabaseComboEnabled = dbItems.Count > 0;

                    // Nếu có database hiện tại trong danh sách thì tự chọn
                    if (dbItems.Count > 0)
                    {
                        DatabaseItem matched = null;
                        foreach (var item in dbItems)
                        {
                            if (item.Name.Equals(Database, StringComparison.OrdinalIgnoreCase))
                            {
                                matched = item;
                                break;
                            }
                        }

                        if (matched != null)
                        {
                            SelectedDatabaseItem = matched;
                        }
                        else
                        {
                            SelectedDatabaseItem = dbItems[0];
                        }

                        string msg = automaticFromPing 
                            ? $"Đã kết nối SQL Server và tự động tải {dbItems.Count} databases" 
                            : $"Đã tải {dbItems.Count} databases thành công";
                        SetStatus("success", msg);
                    }
                    else
                    {
                        SetStatus("warning", "Không tìm thấy database khả dụng");
                    }
                });
            }
            catch (SqlException ex)
            {
                LoggingService.Instance.LogError("ConnectDB", "LoadDatabases", "Lỗi SQL", ex);
                string errorDetail = "Lỗi kết nối CSDL.";
                if (ex.Number == 53 || ex.Number == -1 || ex.Number == 2) 
                    errorDetail = "Không tìm thấy SQL Server";
                else if (ex.Number == 18456) 
                    errorDetail = "Sai tài khoản hoặc mật khẩu";

                if (!silent) SetStatus("error", errorDetail);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConnectDB", "LoadDatabases", "Lỗi chung", ex);
                if (!silent) SetStatus("error", "Không thể tải cơ sở dữ liệu");
            }
            finally
            {
                if (!wasProcessing) IsProcessing = false;
            }
        }

        private async Task ValidateSelectedDatabaseAsync(DatabaseItem item, CancellationToken token = default)
        {
            if (token == default) token = _cts.Token;

            if (token.IsCancellationRequested) return;

            bool wasProcessing = IsProcessing;
            if (!wasProcessing) IsProcessing = true;
            LoadingMessage = "Đang kiểm tra cấu trúc hệ thống...";

            try
            {
                var checkConfig = new ConnectionConfig
                {
                    ServerIP = ServerIP,
                    Port = Port,
                    Username = Username,
                    Password = Password,
                    Database = item.Name
                };

                var status = await DatabaseValidationService.ValidateDatabaseAsync(checkConfig, token);
                if (token.IsCancellationRequested) return;
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    
                    item.Status = status;
                    
                    // Cập nhật giao diện nút bấm và thông báo tương ứng
                    IsTestSuccessful = status == DatabaseStatus.Valid;

                    switch (status)
                    {
                        case DatabaseStatus.Valid:
                            SetStatus("success", $"Database {item.Name} tương thích hoàn toàn! ✅ Compatible");
                            break;
                        case DatabaseStatus.Empty:
                            SetStatus("error", $"Database {item.Name} trống (Chưa có bảng) - Không được phép kết nối! ❌ Empty");
                            break;
                        case DatabaseStatus.NeedMigration:
                            SetStatus("error", $"Database {item.Name} cũ (Cần nâng cấp) - Không được phép kết nối! ❌ Need Migration");
                            break;
                        case DatabaseStatus.Invalid:
                            SetStatus("error", $"Database {item.Name} không hợp lệ hoặc thiếu quyền! ❌ Invalid");
                            break;
                        default:
                            SetStatus("warning", "Database có trạng thái chưa xác định.");
                            break;
                    }
                    
                    CommandManager.InvalidateRequerySuggested();
                });
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                LoggingService.Instance.LogError("ConnectDB", "ValidateSelectedDatabase", "Lỗi", ex);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    if (!wasProcessing) IsProcessing = false;
                }
            }
        }


        private async Task TestConnectionAsync()
        {
            if (SelectedDatabaseItem == null)
            {
                SetStatus("error", "Vui lòng chọn cơ sở dữ liệu.");
                return;
            }

            IsProcessing = true;
            LoadingMessage = "Đang kết nối SQL Server...";
            SetStatus("loading", $"Đang thử kết nối cơ sở dữ liệu {Database}...");

            try
            {
                await ValidateSelectedDatabaseAsync(SelectedDatabaseItem, _cts.Token);
            }
            catch (Exception ex)
            {
                SetStatus("error", $"Lỗi: {ex.Message}");
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

        private void Connect()
        {
            // Mã hóa mật khẩu DPAPI trước khi lưu file cấu hình nếu RememberConnection = true
            string encryptedPassword = RememberConnection ? CredentialEncryptionService.Encrypt(Password) : "";
            string encryptedSecondaryPassword = RememberConnection ? CredentialEncryptionService.Encrypt(SecondaryPassword) : "";

            var configToSave = new DbConnectionConfig
            {
                ServerIP = ServerIP,
                Port = Port,
                Database = Database,
                Username = Username,
                Password = encryptedPassword,
                SecondaryServerIP = SecondaryServerIP,
                SecondaryPort = SecondaryPort,
                SecondaryDatabase = SecondaryDatabase,
                SecondaryUsername = SecondaryUsername,
                SecondaryPassword = encryptedSecondaryPassword
            };

            var currentConfig = new DbConnectionConfig
            {
                ServerIP = ServerIP,
                Port = Port,
                Database = Database,
                Username = Username,
                Password = Password, // Thô để chạy trong bộ nhớ
                SecondaryServerIP = SecondaryServerIP,
                SecondaryPort = SecondaryPort,
                SecondaryDatabase = SecondaryDatabase,
                SecondaryUsername = SecondaryUsername,
                SecondaryPassword = SecondaryPassword // Thô để chạy trong bộ nhớ
            };

            // Cập nhật cấu hình thô chạy trong bộ nhớ
            ConnectionManager.Instance.UpdateConnection(currentConfig);
            // Ghi đè file lưu trữ bằng cấu hình có mật khẩu mã hóa DPAPI an toàn
            DbConnectionConfig.SaveToFile(configToSave);

            // Ghi nhận kết nối gần đây
            SaveToRecentConnections(ServerIP, Port, Username, Database);

            DialogResult = true;
            LoggingService.Instance.LogInfo("ConnectDB", "Connect",
                $"Đã thiết lập kết nối an toàn đến database: {ServerIP}:{Port}/{Database}");
            CloseAction?.Invoke();
        }

        private void LoadRecentConnections()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recent_connections.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var items = JsonSerializer.Deserialize<List<RecentConnectionItem>>(json);
                    if (items != null)
                    {
                        RecentConnections = new ObservableCollection<RecentConnectionItem>(items);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("RECENT", "Load", "Lỗi tải Recent Connections", ex);
            }
            RecentConnections = new ObservableCollection<RecentConnectionItem>();
        }

        private void SaveToRecentConnections(string server, string port, string user, string db)
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recent_connections.json");
                var list = new List<RecentConnectionItem>();
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var items = JsonSerializer.Deserialize<List<RecentConnectionItem>>(json);
                    if (items != null) list = items;
                }

                // Xóa trùng lặp
                list.RemoveAll(x => x.ServerIP.Equals(server, StringComparison.OrdinalIgnoreCase) && x.Database.Equals(db, StringComparison.OrdinalIgnoreCase));

                // Thêm vào đầu danh sách
                list.Insert(0, new RecentConnectionItem
                {
                    ServerIP = server,
                    Port = port,
                    Username = user,
                    Database = db
                });

                // Giới hạn 5 kết nối gần đây
                if (list.Count > 5) list = list.GetRange(0, 5);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string outputJson = JsonSerializer.Serialize(list, options);
                File.WriteAllText(filePath, outputJson);

                RecentConnections = new ObservableCollection<RecentConnectionItem>(list);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("RECENT", "Save", "Lỗi lưu Recent Connections", ex);
            }
        }

        private void SelectRecentConnection(RecentConnectionItem item)
        {
            ServerIP = item.ServerIP;
            Port = item.Port;
            Username = item.Username;
            Database = item.Database;
            Password = ""; // Yêu cầu nhập lại mật khẩu khi click nhanh để đảm bảo bảo mật tuyệt đối
            
            SetStatus("warning", $"Đã tải kết nối gần đây: {item.ServerIP}:{item.Port}. Hãy nhập mật khẩu.");
        }

        private void SetStatus(string state, string msg)
        {
            RunOnUI(() =>
            {
                StatusState = state;
                StatusMessage = msg;
                IsSuccessStatus = state == "success";
            });
        }

        private void RunOnUI(Action action)
        {
            if (Application.Current?.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(action);
                }
            }
            else
            {
                action();
            }
        }
    }
}


