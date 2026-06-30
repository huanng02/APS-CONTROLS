using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using OpenCvSharp;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using QuanLyGiuXe.Views;
#if DEBUG
using QuanLyGiuXe.DebugTools.Views;
#endif

namespace QuanLyGiuXe
{
    public partial class MainWindow : Window
    {
        private Window? _activeScanSessionWindow = null;
        private readonly object _manualOpenLock = new();
        private readonly System.Collections.Generic.Dictionary<int, DateTime> _lastManualOpen = new();
        private readonly Dictionary<string, DateTime> _lastScanByUid = new();

        private CameraService _cameraService = new CameraService();
        private readonly Dictionary<string, WriteableBitmap> _writeableBitmaps = new();
        private volatile bool _isMinimized = false;
        private DateTime _lastUiDiagnosticsTime = DateTime.MinValue;
        private ParkingView? _parkingViewCache = null;
        private readonly Dictionary<string, ParkingView> _lastBoundParkingViews = new(StringComparer.OrdinalIgnoreCase);
        private bool _isProcessingAuto = false;
        private DateTime _lastAutoScanTime = DateTime.MinValue;
        private DateTime _lastAutoScanTime1 = DateTime.MinValue;
        private DateTime _lastAutoScanTime2 = DateTime.MinValue;
        private readonly GateControlService _gateControlService = new GateControlService();
        private readonly Dictionary<string, Window> _activeModuleWindows = new();
        private readonly MainViewModel _mainViewModel;
        private System.Diagnostics.Process? _lprProcess;

        public MainWindow()
        {
            CameraService.Instance = _cameraService;
            InitializeComponent();
            _mainViewModel = new MainViewModel();
            DataContext = _mainViewModel;

            this.Loaded += MainWindow_Loaded;
            this.StateChanged += (s, e) =>
            {
                _isMinimized = this.WindowState == WindowState.Minimized;
            };

            // LPR health check timer (update UI indicator every 15s)
            var lprTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            lprTimer.Tick += async (s, e) =>
            {
                try
                {
                    bool ok = await PlateRecognitionService.Instance.PingAsync();
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (DataContext is MainViewModel vm)
                        {
                            vm.IsLprAvailable = ok;
                            vm.LprStatusLabel = ok ? "APS Vision AI: Hoạt động" : "APS Vision AI: Không phản hồi";
                        }
                    }));
                }
                catch { }
            };
            lprTimer.Start();

            // Cấu hình GC Timer chạy mỗi 2 giây để giải phóng các bộ đệm ảnh dư thừa trên LOH/Gen2 một cách bất đồng bộ (giảm RAM tối đa, không block UI)
            var gcTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            gcTimer.Tick += (s, e) =>
            {
                GC.Collect(2, GCCollectionMode.Optimized, false, false);
            };
            gcTimer.Start();
            
            // Register standard Handlers with centralized RFID Event Router
            var vehicleAccessHandler = new VehicleAccessHandler();
            vehicleAccessHandler.OnVehicleAccessTriggered += async (readerNo, uid) =>
            {
                XuLyQuetThe(uid, readerNo);
            };
            RFIDEventRouterService.Instance.RegisterHandler(vehicleAccessHandler);
            RFIDEventRouterService.Instance.RegisterHandler(new CardEnrollmentHandler());
            RFIDEventRouterService.Instance.RegisterHandler(new AdminOverrideHandler());

            RFIDService.Instance.OnCardScanned += RawRfidScanned;
            C3200Service.Instance.OnCardScannedEx += RawC3200ScannedEx;
            // subscribe to full RT events to record button presses
            C3200Service.Instance.OnEvent += OnC3200Event;

            WorkstationMonitorService.Instance.OnActiveLanesChanged += () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _mainViewModel?.CalculateLaneVisibilities();
                    ReloadCameras();
                }));
            };
            
            // UI RBAC
            ApplyPermissions();

            // Register keyboard shortcuts
            RegisterShortcuts();
        }

        // Whether automatic session dialog should be shown (driven by config)
        public bool AllowShowSession => AppConfig.Load().ZKTeco.ShowSessionDialog;

        private void RegisterShortcuts()
        {
            this.KeyDown += (s, e) =>
            {
                if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt) && e.Key == Key.Q)
                {
                    MoQAPanel_Click(null, null);
                }
                else if (e.Key == Key.F9)
                {
                    MoQAPanel_Click(null, null);
                }
            };

            if (btnQAPanel != null) btnQAPanel.Visibility = Visibility.Visible;

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.F4)
                {
                    MoSystemConfigDeployment_Click(null, null);
                }
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            // IMPORTANT: Unsubscribe from all global events to prevent leaks and duplication!
            RFIDService.Instance.OnCardScanned -= RawRfidScanned;
            C3200Service.Instance.OnCardScannedEx -= RawC3200ScannedEx;
            C3200Service.Instance.OnEvent -= OnC3200Event;

            // Stop LPR process if running
            try
            {
                if (_lprProcess != null && !_lprProcess.HasExited)
                {
                    _lprProcess.Kill();
                    _lprProcess.Dispose();
                }
            }
            catch { }

            // Stop camera to release resources
            try { _cameraService.StopAll(); } catch { }

            base.OnClosed(e);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            // Start heavy components in background to keep UI responsive
            Task.Run(() => {
                MoCameras();
                RFIDService.Instance.Start();
            });

            // Try to auto-start LPR process if configured
            try
            {
                var cfgText = System.IO.File.ReadAllText("config.json");
                dynamic cfg = Newtonsoft.Json.JsonConvert.DeserializeObject(cfgText);
                if (cfg != null && cfg.Lpr != null && cfg.Lpr.AutoStart == true)
                {
                    string cmd = (string)cfg.Lpr.Command;
                    string wd = (string)cfg.Lpr.WorkingDirectory;
                    if (!string.IsNullOrEmpty(cmd))
                    {
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo();
                            // Split command into executable + args
                            var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                            psi.FileName = parts[0];
                            psi.Arguments = parts.Length > 1 ? parts[1] : string.Empty;
                            psi.WorkingDirectory = string.IsNullOrEmpty(wd) ? Environment.CurrentDirectory : wd;
                            psi.UseShellExecute = false;
                            psi.CreateNoWindow = true;
                            psi.RedirectStandardOutput = true;
                            psi.RedirectStandardError = true;

                            _lprProcess = System.Diagnostics.Process.Start(psi);
                            if (_lprProcess != null)
                            {
                                _lprProcess.EnableRaisingEvents = true;
                                _lprProcess.Exited += (s, ev) =>
                                {
                                    this.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        if (DataContext is MainViewModel vm)
                                        {
                                            vm.IsLprAvailable = false;
                                            vm.LprStatusLabel = "APS Vision AI: Stopped";
                                        }
                                    }));
                                };

                                // read output asynchronously (helpful for debugging)
                                _lprProcess.BeginOutputReadLine();
                                _lprProcess.BeginErrorReadLine();

                                if (DataContext is MainViewModel vm)
                                {
                                    vm.LprStatusLabel = "APS Vision AI: Starting";
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }





        // Show session UI anchored to lane layout inside ParkingView
        public void ShowScanSessionForLane(int laneIndex, QuanLyGiuXe.Models.LichSuXe session)
        {
            try
            {
                var parking = GetParkingView();
                if (parking != null)
                {
                    parking.ShowLaneSession(laneIndex, session);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ShowScanSessionForLaneError", "MainWindow", "Failed to show scan session for lane: " + ex.Message, ex);
            }
        }

        public void ApplyPermissions()
        {
            // --- 1. CATEGORIES LEVEL VISIBILITY ---

            MenuVanHanh.Visibility = (PermissionService.Instance.CheckPermission("VIEW_LOG") || 
                                      PermissionService.Instance.CheckPermission("VIEW_REALTIME_LOG") || 
                                      PermissionService.Instance.CheckPermission("OPEN_BARRIER"))
                ? Visibility.Visible 
                : Visibility.Collapsed;

            MenuBaoCao.Visibility = (PermissionService.Instance.CheckPermission("VIEW_REPORT") || 
                                     PermissionService.Instance.CheckPermission("VIEW_REVENUE"))
                ? Visibility.Visible 
                : Visibility.Collapsed;

            MenuAdmin.Visibility = (PermissionService.Instance.CheckPermission("USER_VIEW") || 
                                    PermissionService.Instance.CheckPermission("MANAGE_PRICING") ||
                                    PermissionService.Instance.CheckPermission("RFID_CREATE") ||
                                    PermissionService.Instance.CheckPermission("RFID_UPDATE") ||
                                    PermissionService.Instance.CheckPermission("RFID_DELETE") ||
                                    PermissionService.Instance.CheckPermission("RFID_RENEW"))
                ? Visibility.Visible 
                : Visibility.Collapsed;

            MenuTools.Visibility = (PermissionService.Instance.CheckPermission("CONFIG_SYSTEM") || 
                                    PermissionService.Instance.CheckPermission("CONFIG_CONTROLLER") || 
                                    PermissionService.Instance.CheckPermission("DATABASE_EXPLORER") || 
                                    PermissionService.Instance.CheckPermission("BACKUP_RESTORE") || 
                                    PermissionService.Instance.CheckPermission("SIMULATE_RECOVERY"))
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // --- 2. GRANULAR BUTTON LEVEL VISIBILITY (Inside Categories) ---

            btnNguoiDung.Visibility = PermissionService.Instance.CheckPermission("USER_VIEW") ? Visibility.Visible : Visibility.Collapsed;
            btnLoaiXe.Visibility = PermissionService.Instance.CheckPermission("MANAGE_PRICING") ? Visibility.Visible : Visibility.Collapsed;
            btnLoaiVe.Visibility = PermissionService.Instance.CheckPermission("MANAGE_PRICING") ? Visibility.Visible : Visibility.Collapsed;
            btnBangGia.Visibility = PermissionService.Instance.CheckPermission("MANAGE_PRICING") ? Visibility.Visible : Visibility.Collapsed;

            btnPersonnelExplorer.Visibility = (PermissionService.Instance.CheckPermission("MANAGE_PRICING") ||
                                               PermissionService.Instance.CheckPermission("RFID_CREATE") ||
                                               PermissionService.Instance.CheckPermission("RFID_UPDATE") ||
                                               PermissionService.Instance.CheckPermission("RFID_DELETE") ||
                                               PermissionService.Instance.CheckPermission("RFID_RENEW")) 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            btnRFIDNonRenewable.Visibility = (PermissionService.Instance.CheckPermission("MANAGE_PRICING") ||
                                              PermissionService.Instance.CheckPermission("RFID_CREATE") ||
                                              PermissionService.Instance.CheckPermission("RFID_UPDATE") ||
                                              PermissionService.Instance.CheckPermission("RFID_DELETE")) 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            btnDashboard.Visibility = PermissionService.Instance.CheckPermission("VIEW_DASHBOARD") ? Visibility.Visible : Visibility.Collapsed;
            btnTopologySettings.Visibility = PermissionService.Instance.CheckPermission("CONFIG_SYSTEM") ? Visibility.Visible : Visibility.Collapsed;

            btnSQLTool.Visibility = PermissionService.Instance.CheckPermission("DATABASE_EXPLORER") ? Visibility.Visible : Visibility.Collapsed;
            btnBackupRestore.Visibility = PermissionService.Instance.CheckPermission("BACKUP_RESTORE") ? Visibility.Visible : Visibility.Collapsed;
            btnSystemConfigDeployment.Visibility = PermissionService.Instance.CheckPermission("CONFIG_CONTROLLER") ? Visibility.Visible : Visibility.Collapsed;
            btnQAPanel.Visibility = PermissionService.Instance.CheckPermission("SIMULATE_RECOVERY") ? Visibility.Visible : Visibility.Collapsed;

            // Matrix button visibility (only visible to SuperAdmin, Admin, Manager, Auditor who have VIEW_AUDIT_LOG or ROLE_ASSIGN)
            if (btnMaTranPhanQuyen != null)
            {
                btnMaTranPhanQuyen.Visibility = (PermissionService.Instance.CheckPermission("ROLE_ASSIGN") || 
                                                 PermissionService.Instance.CheckPermission("VIEW_AUDIT_LOG"))
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void GenerateTestLogs_Click(object sender, RoutedEventArgs e)
        {
            // Create several test logs to make sure LoggingService and DB path execute
            try
            {
                LoggingService.Instance.LogInfo("TestRFIDRead", "RFIDService", "Test UID: ABC123", userId: null, plate: null);
                LoggingService.Instance.LogInfo("TestPlateRecognized", "ParkingLogicService", "Plate: TEST123", userId: null, plate: "TEST123");
                LoggingService.Instance.LogError("TestError", "App", "This is a test error", new Exception("Test exception"));
                MessageBox.Show("Test logs generated (check logs folder and AppLogs table).", "Test Logs", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to generate test logs: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Simple toast: create a small window showing message and auto-close after ms
        private void ShowToast(string message, int milliseconds = 1500)
        {
            try
            {
                var toast = new Window
                {
                    Width = 320,
                    Height = 60,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    ShowInTaskbar = false,
                    Topmost = true,
                    ShowActivated = false,
                };

                var border = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 51, 51, 51)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12)
                };
                var tb = new TextBlock { Text = message, Foreground = System.Windows.Media.Brushes.White, FontSize = 14, TextWrapping = TextWrapping.Wrap };
                border.Child = tb;
                toast.Content = border;

                // position bottom-right of primary screen working area
                var wa = SystemParameters.WorkArea;
                toast.Left = wa.Right - toast.Width - 20;
                toast.Top = wa.Bottom - toast.Height - 20;

                toast.Show();

                var _ = Task.Run(async () =>
                {
                    await Task.Delay(milliseconds);
                    try { toast.Dispatcher.BeginInvoke(new Action(() => toast.Close())); } catch { }
                });
            }
            catch { }
        }

        private void MoSystemConfigDeployment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AuthorizationGuard.Protect("CONFIG_CONTROLLER", "C3-200 Hardware Configuration");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không có quyền truy cập", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is MainViewModel vm)
            {
                vm.SetView(new SystemConfigDeploymentViewModel { ActiveTabIndex = 0 });
            }
        }

        private void MoAdvancedSettings_Click(object sender, RoutedEventArgs e)
        {
            new Views.AdvancedSettingsWindow { Owner = this }.ShowDialog();
            RestoreSidebarSelection();
        }

        private void OnC3200Event(Services.C3200Event evt)
        {
            if (evt == null) return;

            // Kiểm tra nếu là sự kiện nhấn nút
            var raw = (evt.RawData ?? "").ToUpper();
            bool isButton = raw.Contains("BUTTON") || evt.EventType == 202;

            if (isButton)
            {
                // Đẩy sang Service xử lý ngầm, tạo snapshot camera và giải phóng sau khi xong để không treo/rò rỉ RAM
                var snapshot = GetCurrentFramesSnapshot();
                Task.Run(async () =>
                {
                    try
                    {
                        await _gateControlService.ProcessGateActionAsync(evt.Door, snapshot, "BUTTON_PRESS");
                    }
                    finally
                    {
                        foreach (var bmp in snapshot.Values) bmp.Dispose();
                    }
                });
            }
        }

        private void OnRfidScanned(string uid) => XuLyQuetThe(uid, 1);
        private void OnC3200Scanned(string uid, int door, int inOutState) 
        {
            int readerNo = (door - 1) * 2 + (inOutState == 1 ? 2 : 1);
            XuLyQuetThe(uid, readerNo);
        }

        private void RawRfidScanned(string uid)
        {
            Task.Run(async () => await RFIDEventRouterService.Instance.RouteEventAsync(uid, 1, 1));
        }

        private void RawC3200Scanned(string uid, int door, int inOutState)
        {
            int readerNo = (door - 1) * 2 + (inOutState == 1 ? 2 : 1);
            Task.Run(async () => await RFIDEventRouterService.Instance.RouteEventAsync(uid, readerNo, door));
        }

        private void RawC3200ScannedEx(string uid, int door, int inOutState, string controllerIp)
        {
            Task.Run(async () =>
            {
                int readerNo = await ResolveReaderNoAsync(controllerIp, door, inOutState);
                await RFIDEventRouterService.Instance.RouteEventAsync(uid, readerNo, door);
            });
        }

        private async Task<int> ResolveReaderNoAsync(string controllerIp, int door, int inOutState)
        {
            try
            {
                var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var allBarriers = await ParkingTopologyService.Instance.GetBarriersAsync();

                var ctrl = allControllers.FirstOrDefault(c => c.IpAddress.Trim().Equals(controllerIp.Trim(), StringComparison.OrdinalIgnoreCase) && c.IsActive);
                if (ctrl == null) return (door - 1) * 2 + (inOutState == 1 ? 2 : 1);

                var barrier = allBarriers.FirstOrDefault(b => b.ControllerId == ctrl.Id && b.RelayNumber == door && b.IsActive);
                if (barrier == null) return (door - 1) * 2 + (inOutState == 1 ? 2 : 1);

                int laneId = barrier.LaneId ?? 0;

                var vm = _mainViewModel;
                if (vm != null)
                {
                    if (vm.GetDbLaneIdForUiIndex(1) == laneId)
                    {
                        return inOutState == 1 ? 2 : 1;
                    }
                    else if (vm.GetDbLaneIdForUiIndex(2) == laneId)
                    {
                        return inOutState == 1 ? 4 : 3;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("FAILOVER", "ResolveReaderNo", "Error resolving reader number for failover event", ex);
            }

            return (door - 1) * 2 + (inOutState == 1 ? 2 : 1);
        }

        // ── Xử lý quẹt thẻ (dùng chung cho RFID USB + C3-200) ───────────────────

        private void XuLyQuetThe(string uid, int readerNo = 1)
        {
            Task.Run(async () =>
            {
                var vm = _mainViewModel;
                if (vm == null)
                    return;

                try
                {
                    uid = RFIDService.ChuanHoaUID(uid);

                    var cfg = AppConfig.Load();

                    int cooldown = cfg.ZKTeco.CardCooldownMs > 0
                        ? cfg.ZKTeco.CardCooldownMs
                        : 2000;

                    bool skip = false;
                    lock (_lastScanByUid)
                    {
                        if (!_lastScanByUid.TryGetValue(uid, out var last))
                            last = DateTime.MinValue;

                        if ((DateTime.Now - last).TotalMilliseconds < cooldown)
                            skip = true;
                        else
                            _lastScanByUid[uid] = DateTime.Now;
                    }
                    if (skip) return;
                }
                catch
                {
                }

                var mapping = ReaderLaneMappingService.Instance.GetMappingByReader(readerNo);

                if (mapping != null && mapping.IsEnabled)
                {
                    // UI hiện tại chỉ support 2 lane hiển thị
                    int uiLaneIndex = 1;
                    if (vm.GetDbLaneIdForUiIndex(2) == mapping.LaneId)
                    {
                        uiLaneIndex = 2;
                    }

                    // Snapshots will be captured in MainViewModel after successful validation & barrier trigger
                }

                await vm.ProcessScanFromReaderAsync(readerNo, uid);
            });
        }




        // ── Quản lý thẻ ──────────────────────────────────────────────────────────

        private void MoQuanLyThe(object sender, RoutedEventArgs e)
        {
            // Context is switched automatically inside QuanLyThe.xaml.cs!
            LoggingService.Instance.LogInfo("TAB_OPEN", "UI", "Mở tab: Quản lý thẻ");
            new QuanLyThe().ShowDialog();
            LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", "Đóng tab: Quản lý thẻ");
            RestoreSidebarSelection();
        }

        // ── Mở cổng thủ công ─────────────────────────────────────────────────────

        public async void OpenGateIn_Click(object sender, RoutedEventArgs e) =>
            await OpenGateAsync(1);

        public async void OpenGateOut_Click(object sender, RoutedEventArgs e) =>
            await OpenGateAsync(2);

        private void MoButtonLogs_Click(object sender, RoutedEventArgs e) =>
            ShowModuleModal("📋 Nhật ký nhấn nút", () => new ButtonLogsWindow());

        private void ShowModuleModal(string title, Func<Window> creator)
        {
            try
            {
                LoggingService.Instance.LogInfo("TAB_OPEN", "UI", $"Mở tab: {title}");
                var win = creator();
                win.Title = title;
                win.Owner = this;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                win.ShowDialog();
                LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", $"Đóng tab: {title}");
                
                // Cập nhật lại nút Sidebar dựa trên View đang hiển thị
                RestoreSidebarSelection();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ShowModuleModal", "MainWindow", $"Lỗi mở cửa sổ {title}", ex);
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreSidebarSelection()
        {
            if (DataContext is MainViewModel vm)
            {
                // Highlight the correct sidebar button based on the active view
                if (vm.CurrentView is DashboardViewModel)
                {
                    btnDashboard.IsChecked = true;
                }
                else if (vm.CurrentView is MonitoringDashboardViewModel)
                {
                    btnMonitoringDashboard.IsChecked = true;
                }
                else if (vm.CurrentView is ParkingTopologyViewModel)
                {
                    btnTopologySettings.IsChecked = true;
                }

                else if (vm.CurrentView is PersonnelExplorerViewModel)
                {
                    btnPersonnelExplorer.IsChecked = true;
                }
                else if (vm.CurrentView is SystemConfigDeploymentViewModel configVm)
                {
                    btnSystemConfigDeployment.IsChecked = true;
                }
                else
                {
                    // Default to the main parking view button for other views
                    btnParkingView.IsChecked = true;
                }
            }
        }

        private async Task OpenGateAsync(int doorNumber)
        {
            try
            {
                // Perform manual gate authorization check
                AuthorizationGuard.Protect("OPEN_BARRIER", $"Manual Open Gate {doorNumber}");
                
                // Perform lane access authorization check
                int readerNo = (doorNumber == 1) ? 1 : 3;
                var mapping = ReaderLaneMappingService.Instance.GetMappingByReader(readerNo);
                int laneId = mapping?.LaneId ?? doorNumber;
                AuthorizationGuard.ProtectLane(laneId, $"Manual Open Gate {doorNumber}");

                // Perform workstation ownership check
                var cfg = AppConfig.Load();
                var controllerIp = cfg.ZKTeco?.IpAddress;
                if (!string.IsNullOrWhiteSpace(controllerIp))
                {
                    var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                    var matchedCtrl = allControllers
                        .FirstOrDefault(c => c.IsActive &&
                            c.IpAddress.Trim().Equals(controllerIp.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (matchedCtrl != null && !string.IsNullOrWhiteSpace(matchedCtrl.PcIp))
                    {
                        if (!C3200Service.IsOwnerOfController(matchedCtrl.PcIp))
                        {
                            var myIps = string.Join(", ", C3200Service.GetLocalIpAddresses());
                            throw new Exception($"Máy trạm này ({myIps}) không được cấu hình để điều khiển bộ điều khiển ({controllerIp}) của máy trạm {matchedCtrl.PcIp}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi phân quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string reason = "Mở từ giao diện phần mềm";
            if (AppConfig.Load().ZKTeco.RequireReasonForManualOpen)
            {
                var dialog = new ReasonInputDialog();
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    reason = dialog.EnteredReason;
                }
                else
                {
                    return; // Operator cancelled
                }
            }

            // Gọi Service xử lý trọn gói: Chụp ảnh -> Mở cổng -> Ghi Log (tạo snapshot và giải phóng ảnh sau khi dọn dẹp để giảm RAM)
            var snapshot = GetCurrentFramesSnapshot();
            try
            {
                await _gateControlService.ProcessGateActionAsync(doorNumber, snapshot, "MANUAL_OPEN", reason);
            }
            finally
            {
                foreach (var bmp in snapshot.Values) bmp.Dispose();
            }

            // (Tùy chọn) Cập nhật trạng thái lên UI để người dùng biết
            if (DataContext is MainViewModel vm)
            {
                string status = $"✅ Đã gửi lệnh mở cổng {doorNumber}";
                if (doorNumber == 1) vm.Lane1TrangThai = status;
                else vm.Lane2TrangThai = status;
            }
        }

        private string MapCameraKeyToUi(string camKey)
        {
            if (camKey.StartsWith("Lane_"))
            {
                var parts = camKey.Split('_');
                if (parts.Length == 3 && int.TryParse(parts[1], out int laneId))
                {
                    string type = parts[2]; // "ToanCanh" or "BienSo"
                    var vm = _mainViewModel;
                    if (vm != null)
                    {
                        if (vm.GetDbLaneIdForUiIndex(1) == laneId)
                        {
                            return type == "ToanCanh" ? "Vao1" : "Vao2";
                        }
                        else if (vm.GetDbLaneIdForUiIndex(2) == laneId)
                        {
                            return type == "ToanCanh" ? "Ra1" : "Ra2";
                        }
                    }
                }
            }
            return camKey;
        }

        private bool HasDynamicConfigForLane(int laneId)
        {
            var cfg = AppConfig.Load().Cameras;
            return cfg.LaneCameras != null && cfg.LaneCameras.Any(lc => lc.LaneId == laneId && (!string.IsNullOrEmpty(lc.ToanCanh) || !string.IsNullOrEmpty(lc.BienSo)));
        }

        private string GetActiveInboundPlateCameraKey()
        {
            var vm = _mainViewModel;
            if (vm != null)
            {
                int? dbLaneId1 = vm.GetDbLaneIdForUiIndex(1);
                if (dbLaneId1.HasValue)
                {
                    var lane = ParkingTopologyService.Instance.GetLanes().FirstOrDefault(l => l.Id == dbLaneId1.Value);
                    if (lane != null && lane.Direction?.ToUpper() == "IN")
                    {
                        return HasDynamicConfigForLane(dbLaneId1.Value) 
                            ? $"Lane_{dbLaneId1.Value}_BienSo" 
                            : "Vao2";
                    }
                }
                
                int? dbLaneId2 = vm.GetDbLaneIdForUiIndex(2);
                if (dbLaneId2.HasValue)
                {
                    var lane = ParkingTopologyService.Instance.GetLanes().FirstOrDefault(l => l.Id == dbLaneId2.Value);
                    if (lane != null && lane.Direction?.ToUpper() == "IN")
                    {
                        return HasDynamicConfigForLane(dbLaneId2.Value) 
                            ? $"Lane_{dbLaneId2.Value}_BienSo" 
                            : "Ra2";
                    }
                }
            }
            return "Vao2"; // ultimate fallback
        }

        private int GetUiLaneIndexForPlateDetection(string camKey)
        {
            var vm = _mainViewModel;
            if (vm != null)
            {
                int? dbLaneId1 = vm.GetDbLaneIdForUiIndex(1);
                if (dbLaneId1.HasValue)
                {
                    var lane = ParkingTopologyService.Instance.GetLanes().FirstOrDefault(l => l.Id == dbLaneId1.Value);
                    if (lane != null && lane.Direction?.ToUpper() == "IN")
                    {
                        if (camKey == $"Lane_{dbLaneId1.Value}_BienSo") return 1;
                        if (camKey == "Vao2" && !HasDynamicConfigForLane(dbLaneId1.Value)) return 1;
                    }
                }
                
                int? dbLaneId2 = vm.GetDbLaneIdForUiIndex(2);
                if (dbLaneId2.HasValue)
                {
                    var lane = ParkingTopologyService.Instance.GetLanes().FirstOrDefault(l => l.Id == dbLaneId2.Value);
                    if (lane != null && lane.Direction?.ToUpper() == "IN")
                    {
                        if (camKey == $"Lane_{dbLaneId2.Value}_BienSo") return 2;
                        if (camKey == "Ra2" && !HasDynamicConfigForLane(dbLaneId2.Value)) return 2;
                    }
                }
            }
            return 0;
        }

        private (string cam1, string cam2) GetCameraKeysForLane(int laneId, string direction)
        {
            if (HasDynamicConfigForLane(laneId))
            {
                return ($"Lane_{laneId}_ToanCanh", $"Lane_{laneId}_BienSo");
            }
            return direction?.ToUpper() == "IN"
                ? ("Vao1", "Vao2")
                : ("Ra1", "Ra2");
        }

        // ── Camera (4 cam: 2 per gate) ───────────────────────────────────────

        private void MoCameras()
        {
            AppConfig.ClearCache();
            var cfg = AppConfig.Load().Cameras;
            _cameraService.Initialize();

            // Đăng ký sự kiện xử lý ảnh
            _cameraService.NewMatFrameReceived += (s, data) =>
            {
                bool shouldLog = false;
                DateTime now = DateTime.UtcNow;
                if ((now - _lastUiDiagnosticsTime).TotalSeconds >= 5)
                {
                    _lastUiDiagnosticsTime = now;
                    shouldLog = true;
                }

                if (shouldLog)
                {
                    try
                    {
                        var vmType = _mainViewModel?.GetType().Name ?? "null";
                        var viewType = _mainViewModel?.CurrentView?.GetType().Name ?? "null";
                        LoggingService.Instance.LogInfo("CAM_DIAG", "UI", 
                            $"Frame received for {data.CamKey}. Minimized={_isMinimized}. DataContext={vmType}. CurrentView={viewType}");
                    }
                    catch { }
                }

                if (_isMinimized) return;

                if (_mainViewModel != null && _mainViewModel.CurrentView is not TrangChuViewModel)
                {
                    if (shouldLog)
                    {
                        try
                        {
                            LoggingService.Instance.LogWarning("CAM_DIAG", "UI", 
                                $"Skipping frame for {data.CamKey} because CurrentView is not TrangChuViewModel. CurrentView type: {_mainViewModel.CurrentView?.GetType().Name ?? "null"}");
                        }
                        catch { }
                    }
                    return;
                }

                string uiCamKey = MapCameraKeyToUi(data.CamKey);

                if (shouldLog && uiCamKey == null)
                {
                    LoggingService.Instance.LogWarning("CAM_DIAG", "UI", $"MapCameraKeyToUi returned null for key: {data.CamKey}");
                }

                // 3. Cập nhật ảnh lên giao diện thông qua WriteableBitmap (giảm cấp phát LOH)
                if (uiCamKey != null)
                {
                    UpdateCameraFrame(uiCamKey, data.Frame, shouldLog);
                }
                
                // Plate recognition on the active inbound lane's plate camera
                int uiLaneForAuto = GetUiLaneIndexForPlateDetection(data.CamKey);
                if (uiLaneForAuto > 0)
                {
                    RunAutoDetection(data.Frame, uiLaneForAuto);
                }
            };

            // Start dynamic lane cameras if any are configured
            bool startedAnyDynamic = false;
            var activeKeys = new System.Collections.Generic.List<string>();
            var activeLaneIds = WorkstationMonitorService.Instance.GetActiveLaneIds();
            if (cfg.LaneCameras != null && cfg.LaneCameras.Count > 0)
            {
                foreach (var lc in cfg.LaneCameras)
                {
                    if (activeLaneIds.Count > 0 && !activeLaneIds.Contains(lc.LaneId))
                        continue;

                    if (!string.IsNullOrEmpty(lc.ToanCanh))
                    {
                        string key = $"Lane_{lc.LaneId}_ToanCanh";
                        _cameraService.StartIpCamera(key, lc.ToanCanh);
                        activeKeys.Add(key);
                        startedAnyDynamic = true;
                    }
                    if (!string.IsNullOrEmpty(lc.BienSo))
                    {
                        string key = $"Lane_{lc.LaneId}_BienSo";
                        _cameraService.StartIpCamera(key, lc.BienSo);
                        activeKeys.Add(key);
                        startedAnyDynamic = true;
                    }
                }
            }

            // Fallback to legacy cameras if no dynamic camera was started
            if (!startedAnyDynamic)
            {
                if (!string.IsNullOrEmpty(cfg.VaoToanCanh)) { _cameraService.StartIpCamera("Vao1", cfg.VaoToanCanh); activeKeys.Add("Vao1"); }
                if (!string.IsNullOrEmpty(cfg.VaoBienSo)) { _cameraService.StartIpCamera("Vao2", cfg.VaoBienSo); activeKeys.Add("Vao2"); }
                if (!string.IsNullOrEmpty(cfg.RaToanCanh)) { _cameraService.StartIpCamera("Ra1", cfg.RaToanCanh); activeKeys.Add("Ra1"); }
                if (!string.IsNullOrEmpty(cfg.RaBienSo)) { _cameraService.StartIpCamera("Ra2", cfg.RaBienSo); activeKeys.Add("Ra2"); }
            }

            Services.Connection.AutoReconnectService.Instance.UpdateCameraResources(activeKeys, _cameraService);
        }

        public void ReloadCameras()
        {
            try
            {
                _cameraService.ClearCache();
                AppConfig.ClearCache();
                var appCfg = AppConfig.Load();
                var cfg = appCfg.Cameras;

                
                // Build target camera configuration list (key -> url)
                var targetConfigs = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool startedAnyDynamic = false;

                var activeLaneIds = WorkstationMonitorService.Instance.GetActiveLaneIds();
                if (cfg.LaneCameras != null && cfg.LaneCameras.Count > 0)
                {
                    foreach (var lc in cfg.LaneCameras)
                    {
                        if (activeLaneIds.Count > 0 && !activeLaneIds.Contains(lc.LaneId))
                            continue;

                        if (!string.IsNullOrEmpty(lc.ToanCanh))
                        {
                            targetConfigs[$"Lane_{lc.LaneId}_ToanCanh"] = lc.ToanCanh;
                            startedAnyDynamic = true;
                        }
                        if (!string.IsNullOrEmpty(lc.BienSo))
                        {
                            targetConfigs[$"Lane_{lc.LaneId}_BienSo"] = lc.BienSo;
                            startedAnyDynamic = true;
                        }
                    }
                }

                // Fallback to legacy cameras if no dynamic camera was configured
                if (!startedAnyDynamic)
                {
                    if (!string.IsNullOrEmpty(cfg.VaoToanCanh)) targetConfigs["Vao1"] = cfg.VaoToanCanh;
                    if (!string.IsNullOrEmpty(cfg.VaoBienSo)) targetConfigs["Vao2"] = cfg.VaoBienSo;
                    if (!string.IsNullOrEmpty(cfg.RaToanCanh)) targetConfigs["Ra1"] = cfg.RaToanCanh;
                    if (!string.IsNullOrEmpty(cfg.RaBienSo)) targetConfigs["Ra2"] = cfg.RaBienSo;
                }

                // Get currently running camera keys and their active URLs
                var runningStates = _cameraService.GetAllRuntimeStates();
                var runningKeys = runningStates.Select(s => s.CameraKey).ToList();

                // 1. Stop cameras that are no longer in the target config or whose URLs have changed
                foreach (var key in runningKeys)
                {
                    string activeUrl = _cameraService.GetUrl(key);
                    if (!targetConfigs.TryGetValue(key, out string targetUrl) || 
                        !string.Equals(activeUrl, targetUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        _cameraService.StopIpCamera(key);
                        
                        // Clear the UI frame for this camera
                        string uiCamKey = MapCameraKeyToUi(key);
                        if (!string.IsNullOrEmpty(uiCamKey))
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                _lastBoundParkingViews.Remove(uiCamKey);
                                var parkingView = GetParkingView();
                                parkingView?.UpdateCamera(uiCamKey, null);
                            }));
                        }
                    }
                }

                // 2. Start new cameras or apply changed URLs
                var activeKeys = new System.Collections.Generic.List<string>();
                foreach (var kvp in targetConfigs)
                {
                    string key = kvp.Key;
                    string url = kvp.Value;
                    activeKeys.Add(key);

                    // Only start if not already running with the exact same URL
                    string activeUrl = _cameraService.GetUrl(key);
                    if (string.IsNullOrEmpty(activeUrl) || !string.Equals(activeUrl, url, StringComparison.OrdinalIgnoreCase))
                    {
                        _cameraService.StartIpCamera(key, url);
                    }
                }

                Services.Connection.AutoReconnectService.Instance.UpdateCameraResources(activeKeys, _cameraService);
                Services.Connection.CameraDiagnosticsService.Instance.TrimMemory();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ReloadCameras", "MainWindow", "Lỗi nạp lại cấu hình camera", ex);
            }
        }

        private async void RunAutoDetection(Mat originalMat, int uiLaneIndex)
        {
            if (_isProcessingAuto) return;

            var vm = _mainViewModel;
            if (vm == null) return;

            // Get DB lane ID
            int? dbLaneId = vm.GetDbLaneIdForUiIndex(uiLaneIndex);
            if (!dbLaneId.HasValue) return;

            // Check if lane is locked
            var laneState = LaneRuntimeManager.Instance.GetLaneState(dbLaneId.Value);
            if (laneState != null && laneState.IsLocked)
            {
                // Skip background LPR if lane is busy processing card scan
                return;
            }

            // Throttling: 1 second per lane
            DateTime now = DateTime.Now;
            if (uiLaneIndex == 1)
            {
                if ((now - _lastAutoScanTime1).TotalMilliseconds < 1000) return;
                _lastAutoScanTime1 = now;
            }
            else
            {
                if ((now - _lastAutoScanTime2).TotalMilliseconds < 1000) return;
                _lastAutoScanTime2 = now;
            }

            _isProcessingAuto = true;
            try
            {
                Mat frameClone;
                lock (originalMat)
                {
                    frameClone = originalMat.Clone();
                }

                using (frameClone)
                {
                    // Call the high-performance OpenCV-based RecognizePlateAsync directly
                    var lprResult = await PlateRecognitionService.Instance.RecognizePlateAsync(frameClone);
                    string plate = lprResult.Plate;

                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var mainVm = _mainViewModel;
                        if (mainVm != null)
                        {
                            if (!string.IsNullOrEmpty(plate) && plate.Length > 4)
                            {
                                string formattedPlate = plate.Trim().ToUpper();
                                mainVm.BienSoNhap = formattedPlate;
                                mainVm.IsLprAvailable = true;

                                if (uiLaneIndex == 1)
                                {
                                    mainVm.Lane1BienSo = formattedPlate;
                                    mainVm.Lane1TrangThai = "Đã nhận diện: " + formattedPlate;
                                }
                                else if (uiLaneIndex == 2)
                                {
                                    mainVm.Lane2BienSo = formattedPlate;
                                    mainVm.Lane2TrangThai = "Đã nhận diện: " + formattedPlate;
                                }
                            }
                            else
                            {
                                mainVm.BienSoNhap = string.Empty;
                                if (uiLaneIndex == 1)
                                {
                                    if (string.IsNullOrEmpty(mainVm.Lane1BienSo))
                                        mainVm.Lane1TrangThai = "Chưa nhận diện biển số";
                                }
                                else if (uiLaneIndex == 2)
                                {
                                    if (string.IsNullOrEmpty(mainVm.Lane2BienSo))
                                        mainVm.Lane2TrangThai = "Chưa nhận diện biển số";
                                }
                            }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi quét tự động: " + ex.Message);
            }
            finally
            {
                _isProcessingAuto = false;
            }
        }

        private Dictionary<string, Bitmap> GetCurrentFramesSnapshot()
        {
            var snapshot = new Dictionary<string, Bitmap>();
            foreach (var key in new[] { "Vao1", "Vao2", "Ra1", "Ra2" })
            {
                using (var mat = _cameraService.GetLatestFrame(key))
                {
                    if (mat != null && !mat.Empty())
                    {
                        snapshot[key] = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat);
                    }
                }
            }
            return snapshot;
        }

        private ParkingView? GetParkingView()
        {
            if (_parkingViewCache != null && _parkingViewCache.IsLoaded)
            {
                return _parkingViewCache;
            }
            _parkingViewCache = FindVisualChild<ParkingView>(MainContentHost);
            return _parkingViewCache;
        }

        private void UpdateCameraFrame(string uiCamKey, Mat mat, bool logDiagnostics = false)
        {
            if (string.IsNullOrEmpty(uiCamKey) || mat == null) return;

            try
            {
                int width;
                int height;
                PixelFormat wpfFormat;
                int stride;
                int bufferSize;

                lock (mat)
                {
                    if (mat.IsDisposed || mat.Empty()) return;
                    width = mat.Width;
                    height = mat.Height;
                    int channels = mat.Channels();
                    switch (channels)
                    {
                        case 1:
                            wpfFormat = PixelFormats.Gray8;
                            break;
                        case 3:
                            wpfFormat = PixelFormats.Bgr24;
                            break;
                        case 4:
                            wpfFormat = PixelFormats.Bgr32;
                            break;
                        default:
                            wpfFormat = PixelFormats.Bgr24;
                            break;
                    }
                    stride = (int)mat.Step();
                    bufferSize = stride * height;
                }

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        var parkingView = GetParkingView();
                        if (parkingView == null)
                        {
                            if (logDiagnostics)
                            {
                                LoggingService.Instance.LogWarning("CAM_DIAG", "UI", $"GetParkingView() returned null for {uiCamKey}");
                            }
                            return;
                        }

                        bool needBind = false;
                        _lastBoundParkingViews.TryGetValue(uiCamKey, out var lastBoundForCam);
                        if (parkingView != lastBoundForCam)
                        {
                            _lastBoundParkingViews[uiCamKey] = parkingView;
                            needBind = true;
                            if (logDiagnostics)
                            {
                                LoggingService.Instance.LogInfo("CAM_DIAG", "UI", $"ParkingView changed to new instance for {uiCamKey}");
                            }
                        }

                        if (!_writeableBitmaps.TryGetValue(uiCamKey, out var wBmp) ||
                            wBmp.PixelWidth != width ||
                            wBmp.PixelHeight != height ||
                            wBmp.Format != wpfFormat)
                        {
                            wBmp = new WriteableBitmap(width, height, 96, 96, wpfFormat, null);
                            _writeableBitmaps[uiCamKey] = wBmp;
                            needBind = true;
                            if (logDiagnostics)
                            {
                                LoggingService.Instance.LogInfo("CAM_DIAG", "UI", $"Created/re-created WriteableBitmap for {uiCamKey} ({width}x{height})");
                            }
                        }

                        if (needBind)
                        {
                            parkingView.UpdateCamera(uiCamKey, wBmp);
                        }

                        lock (mat)
                        {
                            if (!mat.IsDisposed && !mat.Empty())
                            {
                                wBmp.WritePixels(new Int32Rect(0, 0, width, height), mat.Data, bufferSize, stride);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("UpdateCameraFrame_Dispatcher", "UI", $"Lỗi cập nhật WriteableBitmap từ Mat cho {uiCamKey}", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("UpdateCameraFrame", "UI", $"Lỗi trong UpdateCameraFrame cho {uiCamKey}", ex);
            }
        }

        private static BitmapSource ConvertBitmap(Bitmap bitmap)
        {
            if (bitmap == null) return null;

            try
            {
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                // Tự động chọn định dạng WPF tương ứng với Bitmap gốc
                System.Windows.Media.PixelFormat wpfFormat;
                switch (bitmap.PixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        wpfFormat = PixelFormats.Bgr24;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                    case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                        wpfFormat = PixelFormats.Bgr32;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                        wpfFormat = PixelFormats.Gray8;
                        break;
                    default:
                        // Nếu là định dạng lạ, ta ép về Bgr24 nhưng có thể gây sọc
                        wpfFormat = PixelFormats.Bgr24;
                        break;
                }

                var bitmapSource = BitmapSource.Create(
                    bitmapData.Width, bitmapData.Height,
                    bitmap.HorizontalResolution, bitmap.VerticalResolution,
                    wpfFormat,
                    null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride);

                bitmap.UnlockBits(bitmapData);

                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi Convert: " + ex.Message);
                return null;
            }
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
                {
            try
            {
                string targetCamKey = GetActiveInboundPlateCameraKey();
                using (var mat = _cameraService.GetLatestFrame(targetCamKey))
                {
                    if (mat != null && !mat.Empty())
                    {
                        using (var bitmapToProcess = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat))
                        {
                            // BƯỚC 1: TẠO DEEP COPY (Quan trọng nhất cho x64)
                            // Việc tạo mới Bitmap(width, height) này đảm bảo tách rời hoàn toàn khỏi Camera
                            Bitmap finalBitmap = new Bitmap(bitmapToProcess.Width, bitmapToProcess.Height);
                            using (Graphics g = Graphics.FromImage(finalBitmap))
                            {
                                g.DrawImage(bitmapToProcess, 0, 0);
                            }

                            // BƯỚC 2: GỌI API (Vẫn dùng await)
                            // Trong lúc API chạy, finalBitmap này sẽ an toàn, không bị camera ghi đè
                            string plate = await ApiService.SendImageAsync(finalBitmap);

                            // BƯỚC 3: CẬP NHẬT GIAO DIỆN
                            if (DataContext is MainViewModel vm)
                            {
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    string formattedPlate = plate?.Trim()?.ToUpper() ?? "";
                                    if (!string.IsNullOrEmpty(formattedPlate) && formattedPlate.Length > 4 && !formattedPlate.Contains("Lỗi"))
                                    {
                                        vm.BienSoNhap = formattedPlate;
                                        vm.IsLprAvailable = true;

                                        int? dbLaneId1 = vm.GetDbLaneIdForUiIndex(1);
                                        var lane1 = dbLaneId1.HasValue ? ParkingTopologyService.Instance.GetLanes().FirstOrDefault(l => l.Id == dbLaneId1.Value) : null;
                                        if (lane1 != null && lane1.Direction?.ToUpper() == "IN")
                                        {
                                            vm.Lane1BienSo = formattedPlate;
                                        }
                                        else
                                        {
                                            int? dbLaneId2 = vm.GetDbLaneIdForUiIndex(2);
                                            var lane2 = dbLaneId2.HasValue ? ParkingTopologyService.Instance.GetLanes().FirstOrDefault(l => l.Id == dbLaneId2.Value) : null;
                                            if (lane2 != null && lane2.Direction?.ToUpper() == "IN")
                                            {
                                                vm.Lane2BienSo = formattedPlate;
                                            }
                                        }

                                        if (vm.XeVaoCommand.CanExecute(null))
                                        {
                                            vm.XeVaoCommand.Execute(null);
                                        }
                                    }
                                    else
                                    {
                                        // LPR failed
                                        vm.BienSoNhap = string.Empty;
                                        vm.IsLprAvailable = false;
                                        MessageBox.Show("Chưa nhận diện được biển số (LPR)", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }));
                            }

                            // Giải phóng ảnh tạm sau khi đã gửi xong
                            finalBitmap.Dispose();
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Không tìm thấy dữ liệu hình ảnh từ Camera {targetCamKey}!");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi thực thi: {ex.Message}");
            }
        }

        // ── Khác ─────────────────────────────────────────────────────────────────

        private void DoiMatKhau_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.Instance.LogInfo("TAB_OPEN", "UI", "Mở tab: Đổi mật khẩu");
            var win = new Views.ChangePasswordWindow
            {
                Owner = this,
                DataContext = new ViewModels.ChangePasswordViewModel()
            };
            win.ShowDialog();
            LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", "Đóng tab: Đổi mật khẩu");
        }

        private void MoThongTinCaNhan_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.Instance.LogInfo("TAB_OPEN", "UI", "Mở tab: Thông tin cá nhân");
            var win = new Views.UserProfileWindow
            {
                Owner = this,
                DataContext = new ViewModels.UserProfileViewModel()
            };
            win.ShowDialog();
            LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", "Đóng tab: Thông tin cá nhân");
        }

        private void MoMaTranPhanQuyen_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.Instance.LogInfo("TAB_OPEN", "UI", "Mở tab: Ma trận phân quyền");
            var win = new Views.RolePermissionMatrixWindow();
            win.Owner = this;
            win.ShowDialog();
            LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", "Đóng tab: Ma trận phân quyền");
        }

        private void UserPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.IsUserPopupOpen = !vm.IsUserPopupOpen;
            }
        }

        private void MoLichSu(object sender, RoutedEventArgs e)
        {
            LoggingService.Instance.LogInfo("TAB_OPEN", "UI", "Mở tab: Lịch sử xe");
            new HistoryWindow().ShowDialog();
            LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", "Đóng tab: Lịch sử xe");
            RestoreSidebarSelection();
        }

        private void MoLichSuGiaHan_Click(object sender, RoutedEventArgs e) =>
            ShowModuleModal("📜 Lịch sử gia hạn thẻ", () => new RFIDGiaHanHistoryWindow());

        private async void MoSQLTool_Click(object sender, RoutedEventArgs e) 
        {
            try
            {
                AuthorizationGuard.Protect("DATABASE_EXPLORER", "SQL Query Tool");
                
                var config = Models.DbConnectionConfig.LoadFromFile();
                var vm = new ConnectDatabaseViewModel();
                
                string connStr = config.BuildConnectionString(timeout: 3);
                bool isConnected = await vm.CheckConnectionAsync(connStr);

                if (!isConnected)
                {
                    LoggingService.Instance.LogInfo("SQLTool", "CheckConnection", "Không thể kết nối với cấu hình hiện tại. Mở form cấu hình.");
                    var connectWindow = new ConnectDatabaseWindow { Owner = this };
                    bool? result = connectWindow.ShowDialog();
                    if (result != true)
                    {
                        return; // User cancelled
                    }
                }
                
                ShowModuleModal("🛠 Mini Database Explorer", () => new Window
                {
                    Content = new QuanLyGiuXe.Views.DatabaseExplorerView(),
                    Width = 1000,
                    Height = 600
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SQLTool", "MoSQLTool_Click", "Lỗi khi mở SQL Tool", ex);
                MessageBox.Show(ex.Message, "Không thể thực hiện", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MoCameraSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AuthorizationGuard.Protect("CONFIG_SYSTEM", "Camera Settings Window");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không có quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoggingService.Instance.LogInfo("TAB_OPEN", "UI", "Mở tab: Cấu hình camera");
            new CameraSettingsWindow { Owner = this }.ShowDialog();
            LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", "Đóng tab: Cấu hình camera");
            RestoreSidebarSelection();
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGrid dg && dg.SelectedItem is Xe xe)
            {
                LoggingService.Instance.LogInfo("TAB_OPEN", "UI", $"Mở tab: Chi tiết xe ({xe.BienSo})");
                new VehicleDetailWindow(xe).ShowDialog();
                LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", $"Đóng tab: Chi tiết xe ({xe.BienSo})");
            }
        }



        // ===== SIDEBAR HANDLERS =====
        private void MoParkingView_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TrangChuCommand.Execute(null);
            }
        }

        private void MoDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetView(new DashboardViewModel());
            }
        }

        private void MoMonitoringDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetView(new MonitoringDashboardViewModel());
            }
        }

        private void MoRealtimeEventFeed_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetView(new RealtimeEventFeedViewModel());
            }
        }

        private void MoTopologySettings_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetView(new ParkingTopologyViewModel());
            }
        }

        private void MoPersonnelExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetView(new PersonnelExplorerViewModel());
            }
        }





        private void MoRealtimeLog_Click(object sender, RoutedEventArgs e) =>
            ShowModuleModal("📋 Nhật ký hệ thống", () => new RealtimeLogWindow());

        private void MoQuanLyNguoiDung_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AuthorizationGuard.Protect("USER_VIEW", "User Management Panel");
                
                var win = new QuanLyGiuXe.Views.UserManagementWindow();
                win.Owner = this;
                win.ShowDialog();
                RestoreSidebarSelection();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("UserManagement", "MoQuanLyNguoiDung_Click", "Lỗi mở Quản lý người dùng", ex);
                MessageBox.Show(ex.Message, "Không có quyền truy cập", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ===== MODULE CRUD HANDLER =====
        private void OpenModule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AuthorizationGuard.Protect("MANAGE_PRICING", "Pricing / Category Configuration");
                
                var tag = (sender as FrameworkElement)?.Tag?.ToString();
                UserControl content = null;
                string title = "";
                switch (tag)
                {
                    case "LoaiXe":
                        content = new QuanLyGiuXe.Views.LoaiXeView();
                        title = "Quản lý Loại Xe";
                        break;
                    case "LoaiVe":
                        content = new QuanLyGiuXe.Views.LoaiVeView();
                        title = "Quản lý Loại Vé";
                        break;
                    case "RFID":
                        content = new QuanLyGiuXe.Views.RFIDCardView(false);
                        title = "Quản lý RFID";
                        break;
                    case "RFID_NonRenewable":
                        content = new QuanLyGiuXe.Views.RFIDCardView(true);
                        title = "Quản lý Thẻ Không Gia Hạn";
                        break;
                    case "BangGia":
                        content = new QuanLyGiuXe.Views.BangGiaView();
                        title = "Bảng giá (Quản trị)";
                        break;
                    case "Company":
                        content = new QuanLyGiuXe.Views.CompanyView();
                        title = "Quản lý Công Ty";
                        break;
                    case "Department":
                        content = new QuanLyGiuXe.Views.DepartmentView();
                        title = "Quản lý Phòng Ban";
                        break;
                    case "Position":
                        content = new QuanLyGiuXe.Views.PositionView();
                        title = "Quản lý Chức Vụ";
                        break;
                    case "Employee":
                        content = new QuanLyGiuXe.Views.EmployeeView();
                        title = "Quản lý Nhân Sự";
                        break;
                }

                if (content != null)
                {
                    ShowModuleModal(title, () => new Window
                    {
                        Content = content,
                        Width = 1000,
                        Height = 700
                    });
                }
                else
                {
                    ShowToast("Tính năng chưa có giao diện hoặc sai Tag: " + (tag ?? "(null)"));
                }
            }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModuleOpenErrors.txt"), DateTime.Now.ToString("o") + "\t" + ex.ToString() + "\n\n"); } catch { }
                MessageBox.Show(ex.Message, "Lỗi phân quyền", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //private DatabaseService db = new DatabaseService();

        //private void ThemLoaiXe_Click(object sender, RoutedEventArgs e)
        //{
        //    if (string.IsNullOrWhiteSpace(txtTenLoai.Text))
        //        return;

        //    db.ThemLoaiXe(txtTenLoai.Text);
        //    txtTenLoai.Text = "";

        //    LoadLoaiXe();
        //}

        //private void LoadLoaiXe()
        //{
        //    dgLoaiXe.ItemsSource = db.GetLoaiXe().DefaultView;
        //}

        //private void OpenModule_Click(object sender, RoutedEventArgs e)
        //{
        //    var btn = sender as Button;
        //    string tag = btn.Tag.ToString();

        //    if (tag == "LoaiXe")
        //    //{
        //        MainContent.Content = new Views.LoaiXeView();
        //    }
        //}
        private void MoQAPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AuthorizationGuard.Protect("SIMULATE_RECOVERY", "QA Resiliency Dashboard");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không có quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoggingService.Instance.LogInfo("TAB_OPEN", "UI", "Mở tab: QA Resiliency Dashboard");
            var win = new OfflineQADashboard { Owner = this };
            win.Closed += (s, ev) => {
                LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", "Đóng tab: QA Resiliency Dashboard");
                RestoreSidebarSelection();
            };
            win.Show();
        }

        private void MoBackupRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AuthorizationGuard.Protect("BACKUP_RESTORE", "Backup / Restore dialog");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không có quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is MainViewModel vm)
            {
                vm.BackupRestoreCommand.Execute(null);
                RestoreSidebarSelection();
            }
        }

        private void OpenQAPanel()
        {
#if DEBUG
            var win = new DebugToolsView { Owner = this };
            win.Closed += (s, e) => RestoreSidebarSelection();
            win.Show();
#endif
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T t)
                    return t;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
    }
}
