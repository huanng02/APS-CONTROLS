using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DatabaseService db = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ── Properties ────────────────────────────────────────────────────────────

        private string _bienSoNhap = "";
        public string BienSoNhap
        {
            get => _bienSoNhap;
            set { _bienSoNhap = value; OnPropertyChanged(nameof(BienSoNhap)); }
        }

        private string _tienHienThi = "";
        public string TienHienThi
        {
            get => _tienHienThi;
            set { _tienHienThi = value; OnPropertyChanged(nameof(TienHienThi)); }
        }

        private string _tuKhoaTimKiem = "";
        public string TuKhoaTimKiem
        {
            get => _tuKhoaTimKiem;
            set { _tuKhoaTimKiem = value; OnPropertyChanged(nameof(TuKhoaTimKiem)); TimKiemXe(); }
        }

        public string LastScannedUID { get; set; } = "";

        public object CurrentView { get; set; }
        public ObservableCollection<Xe> DanhSachXe { get; set; }

        // ── Làn Vào ──────────────────────────────────────────────────────────────

        private string _lane1BienSo = "";
        public string Lane1BienSo
        {
            get => _lane1BienSo;
            set { _lane1BienSo = value; OnPropertyChanged(nameof(Lane1BienSo)); }
        }

        private string _lane1TrangThai = "Chờ xe...";
        public string Lane1TrangThai
        {
            get => _lane1TrangThai;
            set { _lane1TrangThai = value; OnPropertyChanged(nameof(Lane1TrangThai)); }
        }

        private string _lane1UID = "";
        public string Lane1UID
        {
            get => _lane1UID;
            set { _lane1UID = value; OnPropertyChanged(nameof(Lane1UID)); }
        }

        private string _lane1Tien = "";
        public string Lane1Tien
        {
            get => _lane1Tien;
            set { _lane1Tien = value; OnPropertyChanged(nameof(Lane1Tien)); }
        }

        private string _lane1ThoiGianVao = "";
        public string Lane1ThoiGianVao
        {
            get => _lane1ThoiGianVao;
            set { _lane1ThoiGianVao = value; OnPropertyChanged(nameof(Lane1ThoiGianVao)); }
        }

        private string _lane1ThoiGianTrongBai = "";
        public string Lane1ThoiGianTrongBai
        {
            get => _lane1ThoiGianTrongBai;
            set { _lane1ThoiGianTrongBai = value; OnPropertyChanged(nameof(Lane1ThoiGianTrongBai)); }
        }

        // ── Lane 2 (Right UI) ─────────────────────────────────────────────────
        private string _lane2BienSo = "";
        public string Lane2BienSo
        {
            get => _lane2BienSo;
            set { _lane2BienSo = value; OnPropertyChanged(nameof(Lane2BienSo)); }
        }

        private string _lane2TrangThai = "Chờ xe...";
        public string Lane2TrangThai
        {
            get => _lane2TrangThai;
            set { _lane2TrangThai = value; OnPropertyChanged(nameof(Lane2TrangThai)); }
        }

        private string _lane2UID = "";
        public string Lane2UID
        {
            get => _lane2UID;
            set { _lane2UID = value; OnPropertyChanged(nameof(Lane2UID)); }
        }

        private string _lane2Tien = "";
        public string Lane2Tien
        {
            get => _lane2Tien;
            set { _lane2Tien = value; OnPropertyChanged(nameof(Lane2Tien)); }
        }

        private string _lane2ThoiGianVao = "";
        public string Lane2ThoiGianVao
        {
            get => _lane2ThoiGianVao;
            set { _lane2ThoiGianVao = value; OnPropertyChanged(nameof(Lane2ThoiGianVao)); }
        }

        private string _lane2ThoiGianTrongBai = "";
        public string Lane2ThoiGianTrongBai
        {
            get => _lane2ThoiGianTrongBai;
            set { _lane2ThoiGianTrongBai = value; OnPropertyChanged(nameof(Lane2ThoiGianTrongBai)); }
        }

        private string _trangThaiKetNoi = "C3200: Đang kết nối...";
        public string TrangThaiKetNoi
        {
            get => _trangThaiKetNoi;
            set { _trangThaiKetNoi = value; OnPropertyChanged(nameof(TrangThaiKetNoi)); }
        }

        // ── Dynamic Lane Configuration ───────────────────────────────────────────
        private string _lane1Title = "LÀN 1";
        public string Lane1Title
        {
            get => _lane1Title;
            set { _lane1Title = value; OnPropertyChanged(nameof(Lane1Title)); }
        }

        private string _lane2Title = "LÀN 2";
        public string Lane2Title
        {
            get => _lane2Title;
            set { _lane2Title = value; OnPropertyChanged(nameof(Lane2Title)); }
        }

        private System.Windows.Media.Brush _lane1Color = (System.Windows.Media.Brush)Application.Current.Resources["APSBlueBrush"];
        public System.Windows.Media.Brush Lane1Color
        {
            get => _lane1Color;
            set { _lane1Color = value; OnPropertyChanged(nameof(Lane1Color)); }
        }

        private System.Windows.Media.Brush _lane2Color = (System.Windows.Media.Brush)Application.Current.Resources["APSRedBrush"];
        public System.Windows.Media.Brush Lane2Color
        {
            get => _lane2Color;
            set { _lane2Color = value; OnPropertyChanged(nameof(Lane2Color)); }
        }

        private string _lane1ButtonText = "MỞ CỔNG 1";
        public string Lane1ButtonText
        {
            get => _lane1ButtonText;
            set { _lane1ButtonText = value; OnPropertyChanged(nameof(Lane1ButtonText)); }
        }

        private string _lane2ButtonText = "MỞ CỔNG 2";
        public string Lane2ButtonText
        {
            get => _lane2ButtonText;
            set { _lane2ButtonText = value; OnPropertyChanged(nameof(Lane2ButtonText)); }
        }

        private bool _isLane1Inbound = true;
        public bool IsLane1Inbound
        {
            get => _isLane1Inbound;
            set { _isLane1Inbound = value; OnPropertyChanged(nameof(IsLane1Inbound)); }
        }

        private bool _isLane2Inbound = false;
        public bool IsLane2Inbound
        {
            get => _isLane2Inbound;
            set { _isLane2Inbound = value; OnPropertyChanged(nameof(IsLane2Inbound)); }
        }

        private string _lane1InfoLabel = "THÔNG TIN XE VÀO";
        public string Lane1InfoLabel
        {
            get => _lane1InfoLabel;
            set { _lane1InfoLabel = value; OnPropertyChanged(nameof(Lane1InfoLabel)); }
        }

        private string _lane2InfoLabel = "THÔNG TIN XE RA";
        public string Lane2InfoLabel
        {
            get => _lane2InfoLabel;
            set { _lane2InfoLabel = value; OnPropertyChanged(nameof(Lane2InfoLabel)); }
        }

        public Visibility Lane1FeeVisibility => IsLane1Inbound ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Lane2FeeVisibility => IsLane2Inbound ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Lane1TimeVisibility => IsLane1Inbound ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Lane2TimeVisibility => IsLane2Inbound ? Visibility.Collapsed : Visibility.Visible;

        public string Lane1ReaderMappingDisplay => GetReaderMappingString(1);
        public string Lane2ReaderMappingDisplay => GetReaderMappingString(2);

        private string GetReaderMappingString(int laneIndex)
        {
            var readers = ReaderLaneMappingService.Instance.GetAll()
                .Where(m => m.LaneIndex == laneIndex)
                .Select(m => (m.Door == 2 ? m.ReaderIndex + 2 : m.ReaderIndex)) // Display 1-4 for user
                .OrderBy(r => r);

            return readers.Any() ? $"MAPPED: Reader {string.Join(" & ", readers)}" : "UNMAPPED";
        }

        private int _totalXeTrongBai = 0;
        public string SoXeTrongBai => $"Xe trong bãi: {_totalXeTrongBai}";

        private bool _isUserPopupOpen;
        public bool IsUserPopupOpen
        {
            get => _isUserPopupOpen;
            set { _isUserPopupOpen = value; OnPropertyChanged(nameof(IsUserPopupOpen)); }
        }

        private bool _isSidebarExpanded = true;
        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set
            {
                _isSidebarExpanded = value;
                OnPropertyChanged(nameof(IsSidebarExpanded));
            }
        }

        public string CurrentUserTen => QuanLyGiuXe.Models.CurrentUser.Ten ?? "Nhân viên";
        public string CurrentUserRole => QuanLyGiuXe.Models.CurrentUser.Role ?? "Người vận hành";
        public string CurrentUserUsername => QuanLyGiuXe.Models.CurrentUser.Username ?? "user";

        public async void UpdateVehicleCount()
        {
            try
            {
                int count = await Task.Run(() => db.GetTotalXeTrongBaiCount());
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() => {
                    _totalXeTrongBai = count;
                    OnPropertyChanged(nameof(SoXeTrongBai));
                }));
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("UpdateCount", "MainViewModel", "Lỗi cập nhật số xe", ex);
            }
        }



        // ── Connection status indicators (Phase 2) ──────────────────────────────────

        private Services.Connection.ConnectionState _dbState = Services.Connection.ConnectionState.Disconnected;
        public Services.Connection.ConnectionState DbState
        {
            get => _dbState;
            set { _dbState = value; OnPropertyChanged(nameof(DbState)); }
        }

        private Services.Connection.ConnectionState _c3State = Services.Connection.ConnectionState.Disconnected;
        public Services.Connection.ConnectionState C3State
        {
            get => _c3State;
            set { _c3State = value; OnPropertyChanged(nameof(C3State)); }
        }

        private Services.Connection.ConnectionState _camVaoState = Services.Connection.ConnectionState.Disconnected;
        public Services.Connection.ConnectionState CamVaoState
        {
            get => _camVaoState;
            set { _camVaoState = value; OnPropertyChanged(nameof(CamVaoState)); }
        }

        private Services.Connection.ConnectionState _camRaState = Services.Connection.ConnectionState.Disconnected;
        public Services.Connection.ConnectionState CamRaState
        {
            get => _camRaState;
            set { _camRaState = value; OnPropertyChanged(nameof(CamRaState)); }
        }

        private bool _isDbConnected;
        public bool IsDbConnected
        {
            get => _isDbConnected;
            set { _isDbConnected = value; OnPropertyChanged(nameof(IsDbConnected)); }
        }

        private bool _isC3Connected;
        public bool IsC3Connected
        {
            get => _isC3Connected;
            set { _isC3Connected = value; OnPropertyChanged(nameof(IsC3Connected)); }
        }

        private string _dbStatusLabel = "Database";
        public string DbStatusLabel
        {
            get => _dbStatusLabel;
            set { _dbStatusLabel = value; OnPropertyChanged(nameof(DbStatusLabel)); }
        }

        private string _c3StatusLabel = "C3-200";
        public string C3StatusLabel
        {
            get => _c3StatusLabel;
            set { _c3StatusLabel = value; OnPropertyChanged(nameof(C3StatusLabel)); }
        }





        // ── Ảnh biển số ─────────────────────────────────────────────────────────────

        private ImageSource? _anhBienSoVao;
        public ImageSource? AnhBienSoVao
        {
            get => _anhBienSoVao;
            set { _anhBienSoVao = value; OnPropertyChanged(nameof(AnhBienSoVao)); }
        }

        private ImageSource? _anhBienSoRaVao;
        public ImageSource? AnhBienSoRaVao
        {
            get => _anhBienSoRaVao;
            set { _anhBienSoRaVao = value; OnPropertyChanged(nameof(AnhBienSoRaVao)); }
        }

        private ImageSource? _anhBienSoRaRa;
        public ImageSource? AnhBienSoRaRa
        {
            get => _anhBienSoRaRa;
            set { _anhBienSoRaRa = value; OnPropertyChanged(nameof(AnhBienSoRaRa)); }
        }

        // ── Ảnh chụp từ 2 cam (snapshot khi xe vào/ra) ──────────────────────────

        private ImageSource? _anhChupVao1;
        public ImageSource? AnhChupVao1
        {
            get => _anhChupVao1;
            set { _anhChupVao1 = value; OnPropertyChanged(nameof(AnhChupVao1)); }
        }

        private ImageSource? _anhChupVao2;
        public ImageSource? AnhChupVao2
        {
            get => _anhChupVao2;
            set { _anhChupVao2 = value; OnPropertyChanged(nameof(AnhChupVao2)); }
        }

        private ImageSource? _anhChupRa1;
        public ImageSource? AnhChupRa1
        {
            get => _anhChupRa1;
            set { _anhChupRa1 = value; OnPropertyChanged(nameof(AnhChupRa1)); }
        }

        private ImageSource? _anhChupRa2;
        public ImageSource? AnhChupRa2
        {
            get => _anhChupRa2;
            set { _anhChupRa2 = value; OnPropertyChanged(nameof(AnhChupRa2)); }
        }

        // ── Helper methods for UI updates ─────────────────────────────────────

        public void UpdateLaneSnapshot(int lane, int cameraIndex, ImageSource source)
        {
            if (lane == 1) // Lane 1 (Left)
            {
                if (cameraIndex == 1) AnhChupVao1 = source;
                else if (cameraIndex == 2) AnhChupVao2 = source;
            }
            else if (lane == 2) // Lane 2 (Right)
            {
                if (cameraIndex == 1) AnhChupRa1 = source;
                else if (cameraIndex == 2) AnhChupRa2 = source;
            }
        }

        public void SetLanePlate(int lane, string plate)
        {
            if (lane == 1) Lane1BienSo = plate;
            else if (lane == 2) Lane2BienSo = plate;
        }



        // ── Commands ──────────────────────────────────────────────────────────────

        public ICommand XeVaoCommand { get; }
        public ICommand XeRaCommand { get; }
        public ICommand XeChiTietCommand { get; }
        public ICommand TrangChuCommand { get; }
        public ICommand TimKiemCommand { get; }
        public ICommand LichSuCommand { get; }
        public ICommand DatabaseExplorerCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ToggleUserPopupCommand { get; }
        public ICommand EditProfileCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand BackupRestoreCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────────

        public MainViewModel()
        {
            var cfg = AppConfig.Load();

            DanhSachXe = new ObservableCollection<Xe>();
            DanhSachXe.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SoXeTrongBai));

            XeVaoCommand = new RelayCommand(async _ => await ProcessActionAsync(1, IsLane1Inbound, LastScannedUID));
            XeRaCommand = new RelayCommand(async _ => await ProcessActionAsync(2, IsLane2Inbound, LastScannedUID));
            XeChiTietCommand = new RelayCommand<Xe>(XeChiTiet);

            C3200Service.Instance.OnConnectionChanged += OnC3200ConnectionChanged;
            
            CurrentView = new TrangChuViewModel();
            
            TrangChuCommand = new RelayCommand(_ => SetView(new TrangChuViewModel()));
            TimKiemCommand = new RelayCommand(_ => SetView(new TimKiemViewModel()));
            LichSuCommand = new RelayCommand(_ => SetView(new LichSuViewModel()));
            DatabaseExplorerCommand = new RelayCommand(_ => SetView(new DatabaseExplorerViewModel()));
            ToggleUserPopupCommand = new RelayCommand(_ => IsUserPopupOpen = !IsUserPopupOpen);
            EditProfileCommand = new RelayCommand(_ =>
            {
                try
                {
                    IsUserPopupOpen = false;

                    var window = new Views.UserProfileWindow();

                    window.ShowDialog();

                    // Refresh UI user info
                    RefreshCurrentUserInfo();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError(
                        "OpenProfileWindow",
                        "MainViewModel",
                        "Failed to open profile window",
                        ex);

                    MessageBox.Show(
                        "Không thể mở thông tin cá nhân",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });

            ChangePasswordCommand = new RelayCommand(_ =>
            {
                try
                {
                    IsUserPopupOpen = false;

                    var window = new Views.ChangePasswordWindow();

                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError(
                        "OpenChangePasswordWindow",
                        "MainViewModel",
                        "Failed to open change password window",
                        ex);

                    MessageBox.Show(
                        "Không thể mở đổi mật khẩu",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });

            BackupRestoreCommand = new RelayCommand(_ =>
            {
                try
                {
                    IsUserPopupOpen = false;
                    var window = new Views.BackupRestoreWindow();
                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("OpenBackupWindow", "MainViewModel", "Failed to open backup window", ex);
                    MessageBox.Show("Không thể mở cửa sổ Quản lý Backup", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
            LogoutCommand = new RelayCommand(_ => {
                IsUserPopupOpen = false;
                UnsubscribeEvents(); // Clean up this VM
                if (Application.Current is App app)
                {
                    app.PerformLogout();
                }
            });

            // Kick off heavy initialization in the background
            Task.Run(async () => await InitializeAsync(cfg));
            
            RefreshSettings();
        }

        public void RefreshSettings()
        {
            try
            {
                var cfg = AppConfig.Load();
                var blue = (System.Windows.Media.Brush)Application.Current.Resources["APSBlueBrush"];
                var red = (System.Windows.Media.Brush)Application.Current.Resources["APSRedBrush"];

                if (cfg.ZKTeco.ForceAllIn)
                {
                    Lane1Title = "LÀN 1";
                    Lane2Title = "LÀN 2";
                    Lane1Color = blue;
                    Lane2Color = blue;
                    IsLane1Inbound = true;
                    IsLane2Inbound = true;
                    Lane1ButtonText = "MỞ CỔNG 1";
                    Lane2ButtonText = "MỞ CỔNG 2";
                    Lane1InfoLabel = "THÔNG TIN LÀN 1";
                    Lane2InfoLabel = "THÔNG TIN LÀN 2";
                }
                else if (cfg.ZKTeco.ForceAllOut)
                {
                    Lane1Title = "LÀN 1";
                    Lane2Title = "LÀN 2";
                    Lane1Color = red;
                    Lane2Color = red;
                    IsLane1Inbound = false;
                    IsLane2Inbound = false;
                    Lane1ButtonText = "MỞ CỔNG 1";
                    Lane2ButtonText = "MỞ CỔNG 2";
                    Lane1InfoLabel = "THÔNG TIN LÀN 1";
                    Lane2InfoLabel = "THÔNG TIN LÀN 2";
                }
                else
                {
                    Lane1Title = "LÀN 1";
                    Lane2Title = "LÀN 2";
                    Lane1Color = blue;
                    Lane2Color = red;
                    IsLane1Inbound = true;
                    IsLane2Inbound = false;
                    Lane1ButtonText = "MỞ CỔNG 1";
                    Lane2ButtonText = "MỞ CỔNG 2";
                    Lane1InfoLabel = "THÔNG TIN LÀN 1";
                    Lane2InfoLabel = "THÔNG TIN LÀN 2";
                }

                // Notify visibility changes
                OnPropertyChanged(nameof(Lane1FeeVisibility));
                OnPropertyChanged(nameof(Lane2FeeVisibility));
                OnPropertyChanged(nameof(Lane1TimeVisibility));
                OnPropertyChanged(nameof(Lane2TimeVisibility));
                // Refresh reader mappings
                ReaderLaneMappingService.Instance.Load();
                OnPropertyChanged(nameof(Lane1ReaderMappingDisplay));
                OnPropertyChanged(nameof(Lane2ReaderMappingDisplay));
            }
            catch { }
        }

        private async Task InitializeAsync(AppConfig cfg)
        {
            try
            {
                // 1. ZKTeco/C3200 Init
                var zk = cfg.ZKTeco;
                C3200Service.Instance.Configure(
                    ip: zk.IpAddress, port: zk.TcpPort,
                    password: zk.Password, timeoutMs: zk.Timeout,
                    barrierDuration: zk.BarrierDuration);
                
                await C3200Service.Instance.ConnectAsync();

                // 2. Heavy data loading removed from startup (Load on demand)
                UpdateVehicleCount();



                // 4. Connection monitor: reset UI + restart loop (login lại / VM mới)
                await Application.Current.Dispatcher.InvokeAsync(ResetStatus);
                await StartConnectionCheck();

                // 5. Initialize Auto Reconnect (Phase 2)
                InitializeAutoReconnect(cfg);
                
                // 6. Khởi động Backup Scheduler
                Services.Backup.BackupScheduler.Instance.Start();
                
                // 7. Khởi động SQL Connectivity Monitoring (New Service)
                ConnectivityStateService.Instance.Start();
                
                // 8. Khởi động Auto Sync Engine (Phase 6.2)
                AutoSyncService.Instance.Start();
                
                LoggingService.Instance.LogInfo("VMInit", "MainViewModel", "Async initialization complete");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("VMInitError", "MainViewModel", "Async init failed", ex);
            }
        }

        private void InitializeAutoReconnect(AppConfig cfg)
        {
            var reconnectService = Services.Connection.AutoReconnectService.Instance;
            
            // Đăng ký DB và C3
            reconnectService.RegisterResource(new Services.Connection.DatabaseResource());
            reconnectService.RegisterResource(new Services.Connection.C3200Resource());
            
            // Đăng ký Cameras
            // Trong project này, mỗi View tự tạo CameraService. 
            // Ta sẽ dùng một instance chung cho Monitor AutoReconnect.
            var monitorCamService = new CameraService(); 
            reconnectService.RegisterResource(new Services.Connection.CameraResource("VaoToanCanh", monitorCamService));
            reconnectService.RegisterResource(new Services.Connection.CameraResource("VaoBienSo", monitorCamService));
            reconnectService.RegisterResource(new Services.Connection.CameraResource("RaToanCanh", monitorCamService));
            reconnectService.RegisterResource(new Services.Connection.CameraResource("RaBienSo", monitorCamService));

            // Lắng nghe thay đổi trạng thái để cập nhật UI
            Services.Connection.ConnectionStateService.Instance.PropertyChanged += (s, e) =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    var svc = Services.Connection.ConnectionStateService.Instance;
                    switch (e.PropertyName)
                    {
                        case "Database":
                            DbState = svc.GetState("Database");
                            IsDbConnected = DbState == Services.Connection.ConnectionState.Connected;
                            DbStatusLabel = DbState switch
                            {
                                Services.Connection.ConnectionState.Connected => "Database",
                                Services.Connection.ConnectionState.Reconnecting => "DB (Đang thử lại...)",
                                _ => "DB (Mất kết nối)"
                            };
                            break;
                        case "C3200":
                            C3State = svc.GetState("C3200");
                            IsC3Connected = C3State == Services.Connection.ConnectionState.Connected;
                            C3StatusLabel = C3State switch
                            {
                                Services.Connection.ConnectionState.Connected => "C3-200",
                                Services.Connection.ConnectionState.Reconnecting => "C3-200 (Đang thử lại...)",
                                _ => "C3-200 (Mất kết nối)"
                            };
                            break;
                        case "Camera_VaoToanCanh":
                        case "Camera_VaoBienSo":
                            // Cập nhật trạng thái cụm camera vào
                            var s1 = svc.GetState("Camera_VaoToanCanh");
                            var s2 = svc.GetState("Camera_VaoBienSo");
                            CamVaoState = (s1 == Services.Connection.ConnectionState.Connected && s2 == Services.Connection.ConnectionState.Connected) 
                                ? Services.Connection.ConnectionState.Connected : Services.Connection.ConnectionState.Disconnected;
                            break;
                    }
                }));
            };

            reconnectService.Start();
        }

        // ── Connection Monitor handler ─────────────────────────────────────────

        /// <summary>
        /// Đặt chỉ báo DB/C3 về màu vàng "Đang kiểm tra". Gọi trên UI thread (Dispatcher).
        /// </summary>
        public void ResetStatus()
        {
            DbStatusLabel = "Database — Đang kiểm tra";
            C3StatusLabel = "C3-200 — Đang kiểm tra";
        }

        /// <summary>
        /// Đăng ký handler và hủy task monitor cũ (nếu có), chạy vòng kiểm tra mới.
        /// </summary>
        public async Task StartConnectionCheck()
        {
            ConnectionMonitorService.Instance.StatusChanged -= OnConnectionStatusChanged;
            ConnectionMonitorService.Instance.StatusChanged += OnConnectionStatusChanged;
            await Task.Run(() => ConnectionMonitorService.Instance.Restart()).ConfigureAwait(false);
        }

        private void OnConnectionStatusChanged(ConnectionStatus status)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                // UI cập nhật Label đã được chuyển sang ConnectionStateService PropertyChanged
            }));

            // Toast notification (có thể gọi từ bất kỳ thread, service tự marshal)
            if (status.DatabaseChanged)
            {
                ToastNotificationService.Instance.ShowToast(
                    status.IsDatabaseConnected
                        ? "Database connected successfully"
                        : "Database connection lost",
                    status.IsDatabaseConnected ? ToastType.Success : ToastType.Error
                );
            }

            if (status.C3Changed)
            {
                ToastNotificationService.Instance.ShowToast(
                    status.IsC3Connected
                        ? "C3-200 connected successfully"
                        : "C3-200 disconnected",
                    status.IsC3Connected ? ToastType.Success : ToastType.Error
                );
            }
        }



        public void SetView(object view)
        {
            CurrentView = view;
            OnPropertyChanged(nameof(CurrentView));
        }

        // LoadXeTrongBai removed - use UpdateVehicleCount for dashboard instead


        // ── Xe Vào / Ra ──────────────────────────────────────────────────────────

        // ── Xe Vào / Ra (Dynamic Lane Support) ──────────────────────────────────

        public async Task ProcessActionAsync(int laneIndex, bool isInbound, string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                SetLaneStatus(laneIndex, "❌ Vui lòng quét thẻ RFID!");
                return;
            }

            try
            {
                LoggingService.Instance.LogInfo("ProcessAction", "MainViewModel", $"Lane={laneIndex} In={isInbound} UID={uid}");

                // Verify card
                var card = db.GetRFIDCardByUid(uid);
                if (card == null || card.Id == 0)
                {
                    SetLaneStatus(laneIndex, $"❌ Thẻ {uid} chưa đăng ký!");
                    return;
                }

                if (isInbound)
                {
                    await ProcessInboundAsync(laneIndex, card, uid);
                }
                else
                {
                    await ProcessOutboundAsync(laneIndex, card, uid);
                }

                UpdateVehicleCount();
                LastScannedUID = string.Empty;
            }
            catch (Exception ex)
            {
                SetLaneStatus(laneIndex, $"❌ Lỗi xử lý: {ex.Message}");
                LoggingService.Instance.LogError("ProcessActionError", "MainViewModel", $"Lane={laneIndex}", ex);
            }
        }

        private async Task ProcessInboundAsync(int laneIndex, RFIDCard card, string uid)
        {
            string plate = card.BienSo ?? string.Empty;

            try
            {
                db.ThemXe(card.Id, string.IsNullOrEmpty(plate) ? null : plate, "");
                
                // Update UI for the specific lane
                SetLanePlate(laneIndex, plate);
                SetLaneUID(laneIndex, uid);
                
                bool opened = await C3200Service.Instance.OpenBarrierAsync(laneIndex);
                SetLaneStatus(laneIndex, opened ? $"✅ Xe vào lúc {DateTime.Now:HH:mm}" : "⚠ Xe vào – barrier lỗi");

                // Add to list
                DanhSachXe.Add(new Xe { BienSo = plate, ThoiGianVao = DateTime.Now });
            }
            catch (Exception ex)
            {
                SetLaneStatus(laneIndex, $"❌ Lỗi ghi DB: {ex.Message}");
            }
        }

        private async Task ProcessOutboundAsync(int laneIndex, RFIDCard card, string uid)
        {
            var rec = db.GetXeTrongBaiRecordByCardId(card.Id);
            if (rec == null)
            {
                SetLaneStatus(laneIndex, "⚠ Không tìm thấy xe trong bãi");
                return;
            }

            var (id, plate, timeIn) = rec.Value;
            var duration = DateTime.Now - timeIn;
            double fee = db.TinhTien(card.LoaiXeId, card.LoaiVeId, timeIn, DateTime.Now);

            try
            {
                db.UpdateXeRaById(id, DateTime.Now);
                db.LuuLichSu(plate, timeIn, DateTime.Now, fee, "", uid);
                db.XoaXeByCardId(card.Id);

                // Update UI
                SetLanePlate(laneIndex, plate);
                SetLaneUID(laneIndex, uid);
                SetLaneTimeInfo(laneIndex, timeIn, duration);
                SetLaneFee(laneIndex, fee);

                bool opened = await C3200Service.Instance.OpenBarrierAsync(laneIndex);
                SetLaneStatus(laneIndex, opened ? $"✅ Xe ra lúc {DateTime.Now:HH:mm}" : "⚠ Xe ra – barrier lỗi");

                // Remove from list
                if (!string.IsNullOrEmpty(plate))
                {
                    var xe = DanhSachXe.FirstOrDefault(x => x.BienSo == plate);
                    if (xe != null) DanhSachXe.Remove(xe);
                }
            }
            catch (Exception ex)
            {
                SetLaneStatus(laneIndex, $"❌ Lỗi DB: {ex.Message}");
            }
        }

        // ── UI Helper Methods (Lane Aware) ─────────────────────────────────────

        private void SetLaneStatus(int lane, string msg)
        {
            if (lane == 1) Lane1TrangThai = msg;
            else Lane2TrangThai = msg;
        }

        private void SetLaneUID(int lane, string uid)
        {
            if (lane == 1) Lane1UID = uid;
            else Lane2UID = uid;
        }

        private void SetLaneTimeInfo(int lane, DateTime timeIn, TimeSpan duration)
        {
            string vaoStr = $"Vào: {timeIn:HH:mm} │ {duration.Hours}h{duration.Minutes:D2}m";
            string trongStr = $"Thời gian trong bãi: {duration.Days}d {duration.Hours}h{duration.Minutes:D2}m";

            if (lane == 1)
            {
                Lane1ThoiGianVao = vaoStr;
                Lane1ThoiGianTrongBai = trongStr;
            }
            else
            {
                Lane2ThoiGianVao = vaoStr;
                Lane2ThoiGianTrongBai = trongStr;
            }
        }

        private void SetLaneFee(int lane, double fee)
        {
            string feeStr = $"💰 {fee:N0} VNĐ";
            if (lane == 1) Lane1Tien = feeStr;
            else Lane2Tien = feeStr;
        }

        // ── Tìm kiếm / Chi tiết ──────────────────────────────────────────────────

        private async void TimKiemXe()
        {
            try
            {
                var keyword = TuKhoaTimKiem?.Trim().ToLower();
                var source = await Task.Run(() => 
                {
                    var data = db.LayXeTrongBai().AsEnumerable();
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        data = data.Where(r => r["BienSo"].ToString()!.ToLower().Contains(keyword));
                    }
                    return data.ToList();
                });

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    DanhSachXe.Clear();
                    foreach (var row in source)
                    {
                        DanhSachXe.Add(new Xe
                        {
                            BienSo = row["BienSo"].ToString()!,
                            ThoiGianVao = Convert.ToDateTime(row["ThoiGianVao"])
                        });
                    }
                }));
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("TimKiem", "MainViewModel", "Lỗi tìm kiếm xe", ex);
            }
        }

        public void XeChiTiet(Xe xe)
        {
            if (xe == null) return;
            new Views.VehicleDetailWindow(xe).ShowDialog();
        }

        public void UnsubscribeEvents()
        {
            try
            {
                // Unsubscribe from global services to prevent memory leaks and background crashes
                C3200Service.Instance.OnConnectionChanged -= OnC3200ConnectionChanged;
                ConnectionMonitorService.Instance.StatusChanged -= OnConnectionStatusChanged;
            }
            catch { }
        }

        private void OnC3200ConnectionChanged(bool online)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
                TrangThaiKetNoi = online ? "C3200: Online ●" : "C3200: Offline ○");
        }
        public void RefreshCurrentUserInfo()
        {
            OnPropertyChanged(nameof(CurrentUserTen));
            OnPropertyChanged(nameof(CurrentUserUsername));
            OnPropertyChanged(nameof(CurrentUserRole));
        }

        public void Dispose()
        {
            UnsubscribeEvents();
        }
    }
}
