using System;
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
        private readonly object _manualOpenLock = new();
        private readonly System.Collections.Generic.Dictionary<int, DateTime> _lastManualOpen = new();
        private readonly Dictionary<string, DateTime> _lastScanByUid = new();

        private CameraService _cameraService = new CameraService();
        private Dictionary<string, Bitmap> _currentFrames = new();
        private bool _isProcessingAuto = false;
        private DateTime _lastAutoScanTime = DateTime.MinValue;
        private readonly GateControlService _gateControlService = new GateControlService();
        private readonly Dictionary<string, Window> _activeModuleWindows = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            this.Loaded += MainWindow_Loaded;
            
            // Register standard Handlers with centralized RFID Event Router
            var vehicleAccessHandler = new VehicleAccessHandler();
            vehicleAccessHandler.OnVehicleAccessTriggered += async (readerNo, uid) =>
            {
                await Dispatcher.InvokeAsync(() => XuLyQuetThe(uid, readerNo));
            };
            RFIDEventRouterService.Instance.RegisterHandler(vehicleAccessHandler);
            RFIDEventRouterService.Instance.RegisterHandler(new CardEnrollmentHandler());
            RFIDEventRouterService.Instance.RegisterHandler(new AdminOverrideHandler());

            RFIDService.Instance.OnCardScanned += RawRfidScanned;
            C3200Service.Instance.OnCardScanned += RawC3200Scanned;
            // subscribe to full RT events to record button presses
            C3200Service.Instance.OnEvent += OnC3200Event;
            
            // UI RBAC
            ApplyPermissions();

            // Shortcut for QA Panel
            this.KeyDown += (s, e) => {
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

            this.KeyDown += (s, e) => {
                if (e.Key == Key.F4)
                {
                    MoC3200Settings_Click(null, null);
                }
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            // IMPORTANT: Unsubscribe from all global events to prevent leaks and duplication!
            RFIDService.Instance.OnCardScanned -= RawRfidScanned;
            C3200Service.Instance.OnCardScanned -= RawC3200Scanned;
            C3200Service.Instance.OnEvent -= OnC3200Event;

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
        }

        private void ApplyPermissions()
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

            btnRFID.Visibility = (PermissionService.Instance.CheckPermission("MANAGE_PRICING") ||
                                  PermissionService.Instance.CheckPermission("RFID_CREATE") ||
                                  PermissionService.Instance.CheckPermission("RFID_UPDATE") ||
                                  PermissionService.Instance.CheckPermission("RFID_DELETE") ||
                                  PermissionService.Instance.CheckPermission("RFID_RENEW")) 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            btnDashboard.Visibility = PermissionService.Instance.CheckPermission("VIEW_DASHBOARD") ? Visibility.Visible : Visibility.Collapsed;
            btnTopologySettings.Visibility = PermissionService.Instance.CheckPermission("CONFIG_SYSTEM") ? Visibility.Visible : Visibility.Collapsed;

            btnSQLTool.Visibility = PermissionService.Instance.CheckPermission("DATABASE_EXPLORER") ? Visibility.Visible : Visibility.Collapsed;
            btnBackupRestore.Visibility = PermissionService.Instance.CheckPermission("BACKUP_RESTORE") ? Visibility.Visible : Visibility.Collapsed;
            btnCameraSettings.Visibility = PermissionService.Instance.CheckPermission("CONFIG_SYSTEM") ? Visibility.Visible : Visibility.Collapsed;
            btnC3200Settings.Visibility = PermissionService.Instance.CheckPermission("CONFIG_CONTROLLER") ? Visibility.Visible : Visibility.Collapsed;
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

        private async void MoC3200Settings_Click(object sender, RoutedEventArgs e)
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

            new C3200SettingsWindow().ShowDialog();
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshSettings();
                // Determine site based on saved config and set it
                var cfg = AppConfig.Load();
                var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var activeController = controllers.FirstOrDefault(c => c.IpAddress == cfg.ZKTeco.IpAddress);
                ParkingSite activeSite = null;
                if (activeController != null)
                {
                    var activeGate = gates.FirstOrDefault(g => g.Id == activeController.GateId);
                    if (activeGate != null)
                        activeSite = sites.FirstOrDefault(s => s.Id == activeGate.SiteId);
                }
                if (activeSite != null)
                    vm.SelectedSite = activeSite;
                // Update vehicle count to reflect new site/zone configuration immediately
                vm.UpdateVehicleCount();
            }
            RestoreSidebarSelection();
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
                // Đẩy sang Service xử lý ngầm, không làm treo UI
                Task.Run(() => _gateControlService.ProcessGateActionAsync(evt.Door, _currentFrames, "BUTTON_PRESS"));
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

        // ── Xử lý quẹt thẻ (dùng chung cho RFID USB + C3-200) ───────────────────

        private void XuLyQuetThe(string uid, int readerNo = 1)
        {
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (DataContext is not MainViewModel vm)
                    return;

                try
                {
                    uid = RFIDService.ChuanHoaUID(uid);

                    var cfg = AppConfig.Load();

                    int cooldown = cfg.ZKTeco.CardCooldownMs > 0
                        ? cfg.ZKTeco.CardCooldownMs
                        : 2000;

                    if (!_lastScanByUid.TryGetValue(uid, out var last))
                        last = DateTime.MinValue;

                    if ((DateTime.Now - last).TotalMilliseconds < cooldown)
                        return;

                    _lastScanByUid[uid] = DateTime.Now;
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

                    Task.Run(async () =>
                    {
                        try
                        {
                            var lane = await ParkingTopologyService.Instance.GetLaneByIdAsync(mapping.LaneId);

                            if (lane == null)
                                return;

                            var (cam1, cam2) = GetCameraKeys(lane.Direction);

                            lock (_currentFrames)
                            {
                                if (_currentFrames.TryGetValue(cam1, out var bmp1))
                                {
                                    var img1 = ConvertBitmap((System.Drawing.Bitmap)bmp1.Clone());

                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        vm.UpdateLaneSnapshot(uiLaneIndex, 1, img1);
                                    }));
                                }

                                if (_currentFrames.TryGetValue(cam2, out var bmp2))
                                {
                                    var img2 = ConvertBitmap((System.Drawing.Bitmap)bmp2.Clone());

                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        vm.UpdateLaneSnapshot(uiLaneIndex, 2, img2);
                                    }));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggingService.Instance.LogError(
                                "XuLyQuetThe",
                                "MainWindow",
                                $"Lỗi xử lý snapshot cho Reader {readerNo}",
                                ex);
                        }
                    });
                }

                await vm.ProcessScanFromReaderAsync(readerNo, uid);
            }));
        }

        private (string cam1, string cam2) GetCameraKeys(string direction)
        {
            return direction?.ToUpper() == "IN"
                ? ("Vao1", "Vao2")
                : ("Ra1", "Ra2");
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi phân quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string reason = "Mở từ giao diện phần mềm";
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

            // Gọi Service xử lý trọn gói: Chụp ảnh -> Mở cổng -> Ghi Log
            await _gateControlService.ProcessGateActionAsync(doorNumber, _currentFrames, "MANUAL_OPEN", reason);

            // (Tùy chọn) Cập nhật trạng thái lên UI để người dùng biết
            if (DataContext is MainViewModel vm)
            {
                string status = $"✅ Đã gửi lệnh mở cổng {doorNumber}";
                if (doorNumber == 1) vm.Lane1TrangThai = status;
                else vm.Lane2TrangThai = status;
            }
        }

        // ── Camera (4 cam: 2 per gate) ───────────────────────────────────────

        private void MoCameras()
        {
            var cfg = AppConfig.Load().Cameras;
            _cameraService.Initialize();

            // Đăng ký sự kiện xử lý ảnh
            _cameraService.NewFrameReceived += (s, data) =>
            {
                Bitmap bmpForUI = null;
                lock (data.Frame)
                {
                    bmpForUI = data.Frame.Clone(new Rectangle(0, 0, data.Frame.Width, data.Frame.Height),
                                     System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                }

                lock (_currentFrames)
                {
                    if (_currentFrames.TryGetValue(data.CamKey, out var old)) old.Dispose();
                    _currentFrames[data.CamKey] = (Bitmap)bmpForUI.Clone();
                }

                // 3. Chuyển đổi ảnh (vẫn ở luồng phụ của Camera)
                var uiImage = ConvertBitmap(bmpForUI);
                bmpForUI.Dispose(); // Dùng xong bản cho UI thì hủy ngay

                // 4. Chỉ đẩy kết quả cuối cùng lên màn hình
                if (uiImage != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var parkingView = FindVisualChild<ParkingView>(MainContentHost);
                        if (parkingView != null)
                        {
                            parkingView.UpdateCamera(data.CamKey, uiImage);
                        }
                    }));
                }
                
                // Plate recognition on Vao1 (Entrance)
                if (data.CamKey == "Vao1")
                {
                    RunAutoDetection(data.Frame);
                }
            };

            // Start all configured cameras
            if (!string.IsNullOrEmpty(cfg.VaoToanCanh)) _cameraService.StartIpCamera("Vao1", cfg.VaoToanCanh);
            if (!string.IsNullOrEmpty(cfg.VaoBienSo)) _cameraService.StartIpCamera("Vao2", cfg.VaoBienSo);
            if (!string.IsNullOrEmpty(cfg.RaToanCanh)) _cameraService.StartIpCamera("Ra1", cfg.RaToanCanh);
            if (!string.IsNullOrEmpty(cfg.RaBienSo)) _cameraService.StartIpCamera("Ra2", cfg.RaBienSo);
            
            // Fallback for debug if no config
            if (string.IsNullOrEmpty(cfg.VaoToanCanh)) {
                string debugUrl = "rtsp://192.168.1.121:554/user=admin&password=tlJwpbo6&channel=0&stream=0.sdp";
                _cameraService.StartIpCamera("Vao1", debugUrl);
            }
        }
        private async void RunAutoDetection(Bitmap originalBitmap)
        {
            if (_isProcessingAuto) return;

            // Chặn 1 giây 1 lần
            if ((DateTime.Now - _lastAutoScanTime).TotalMilliseconds < 1000) return;

            _isProcessingAuto = true;
            try
            {
                Bitmap bmpToProcess = null;
                lock (originalBitmap)
                {
                    bmpToProcess = new Bitmap(originalBitmap.Width, originalBitmap.Height);
                    using (Graphics g = Graphics.FromImage(bmpToProcess))
                    {
                        g.DrawImage(originalBitmap, 0, 0);
                    }
                }

                // 1. Gửi ảnh lên server lấy biển số
                string plate = await ApiService.SendImageAsync(bmpToProcess);
                bmpToProcess.Dispose();

                _lastAutoScanTime = DateTime.Now;

                // 2. Kiểm tra nếu có biển số trả về hợp lệ
                if (!string.IsNullOrEmpty(plate) && plate.Length > 4 && !plate.Contains("Lỗi"))
                {
                    // 3. Đẩy dữ liệu về UI Thread
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (this.DataContext is MainViewModel vm)
                        {
                            // Gán vào ô "Biển số nhập" và khung hiển thị làn 1
                            vm.BienSoNhap = plate.Trim().ToUpper();
                            vm.Lane1BienSo = vm.BienSoNhap;

                            // (Tùy chọn) Thông báo trạng thái để người dùng biết đã nhận diện xong
                            vm.Lane1TrangThai = "Đã nhận diện: " + vm.BienSoNhap;
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
                if (_currentFrames.TryGetValue("Vao1", out var bitmapToProcess) && bitmapToProcess != null)
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
                        // Dùng Dispatcher để đảm bảo UI nhận được giá trị mới ngay lập tức
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            vm.BienSoNhap = plate?.Trim() ?? "";
                            if (vm.XeVaoCommand.CanExecute(null))
                            {
                                vm.XeVaoCommand.Execute(null);
                            }
                        }));
                    }

                    // Giải phóng ảnh tạm sau khi đã gửi xong
                    finalBitmap.Dispose();
                }
                else
                {
                    MessageBox.Show("Không tìm thấy dữ liệu hình ảnh từ Camera Vao1!");
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
            var win = new Views.PermissionSummaryWindow
            {
                Owner = this
            };
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

        private void MoRealtimeLog_Click(object sender, RoutedEventArgs e) =>
            ShowModuleModal("📋 Nhật ký hệ thống", () => new RealtimeLogWindow());

        private void MoQuanLyNguoiDung_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AuthorizationGuard.Protect("USER_VIEW", "User Management Panel");
                
                using var frm = new QuanLyGiuXe.Views.UserManagementForm();
                frm.ShowDialog();
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
                        content = new QuanLyGiuXe.Views.RFIDCardView();
                        title = "Quản lý RFID";
                        break;
                    case "BangGia":
                        content = new QuanLyGiuXe.Views.BangGiaView();
                        title = "Bảng giá (Quản trị)";
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
