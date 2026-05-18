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
            
            RFIDService.Instance.OnCardScanned += OnRfidScanned;
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
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
            RFIDService.Instance.OnCardScanned -= OnRfidScanned;
            C3200Service.Instance.OnCardScanned -= OnC3200Scanned;
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
            string role = CurrentUser.Role?.ToUpper() ?? "";

            // --- 1. CATEGORIES LEVEL VISIBILITY ---

            // Logs & Monitor (Vận hành) -> Visible to: ADMIN, SUPERVISOR, OPERATOR, TECHNICIAN
            MenuVanHanh.Visibility = (role == "ADMIN" || role == "SUPERVISOR" || role == "OPERATOR" || role == "TECHNICIAN") 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // History (Báo cáo) -> Visible to: ADMIN, SUPERVISOR, CASHIER
            MenuBaoCao.Visibility = (role == "ADMIN" || role == "SUPERVISOR" || role == "CASHIER") 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // Management (Nhân viên/Cấu hình) -> Visible to: ADMIN, SUPERVISOR
            MenuAdmin.Visibility = (role == "ADMIN" || role == "SUPERVISOR") 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // System & Tools (Hệ thống) -> Visible to: ADMIN, SUPERVISOR, TECHNICIAN
            MenuTools.Visibility = (role == "ADMIN" || role == "SUPERVISOR" || role == "TECHNICIAN") 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // --- 2. GRANULAR BUTTON LEVEL VISIBILITY (Inside Categories) ---

            // Inside MenuAdmin (Management):
            // - Người dùng (User Management): ONLY Admin can manage users!
            btnNguoiDung.Visibility = (role == "ADMIN") ? Visibility.Visible : Visibility.Collapsed;
            // - LoaiXe, LoaiVe, RFID, BangGia: Admin and Supervisor can manage.
            btnLoaiXe.Visibility = (role == "ADMIN" || role == "SUPERVISOR") ? Visibility.Visible : Visibility.Collapsed;
            btnLoaiVe.Visibility = (role == "ADMIN" || role == "SUPERVISOR") ? Visibility.Visible : Visibility.Collapsed;
            btnRFID.Visibility = (role == "ADMIN" || role == "SUPERVISOR") ? Visibility.Visible : Visibility.Collapsed;
            btnBangGia.Visibility = (role == "ADMIN" || role == "SUPERVISOR") ? Visibility.Visible : Visibility.Collapsed;

            // Inside MenuTools (System & Tools):
            // - SQL Tool: ONLY Admin can execute direct SQL queries!
            btnSQLTool.Visibility = (role == "ADMIN") ? Visibility.Visible : Visibility.Collapsed;
            
            // - Backup / Restore: Admin and Supervisor can backup/restore!
            btnBackupRestore.Visibility = (role == "ADMIN" || role == "SUPERVISOR") ? Visibility.Visible : Visibility.Collapsed;

            // - Camera & System Configuration (C3-200): Admin and Technician can configure hardware!
            btnCameraSettings.Visibility = (role == "ADMIN" || role == "TECHNICIAN") ? Visibility.Visible : Visibility.Collapsed;
            btnC3200Settings.Visibility = (role == "ADMIN" || role == "TECHNICIAN") ? Visibility.Visible : Visibility.Collapsed;

            // - Resiliency QA (QA Panel): Admin and Technician can view/simulate recoveries!
            btnQAPanel.Visibility = (role == "ADMIN" || role == "TECHNICIAN") ? Visibility.Visible : Visibility.Collapsed;
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

        private void MoC3200Settings_Click(object sender, RoutedEventArgs e)
        {
            new C3200SettingsWindow().ShowDialog();
            if (DataContext is MainViewModel vm)
            {
                vm.RefreshSettings();
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

        // ── Xử lý quẹt thẻ (dùng chung cho RFID USB + C3-200) ───────────────────

        private void XuLyQuetThe(string uid, int readerNo = 1)
        {
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (DataContext is not MainViewModel vm) return;

                try
                {
                    uid = RFIDService.ChuanHoaUID(uid);
                    var cfg = AppConfig.Load();
                    int cooldown = cfg.ZKTeco.CardCooldownMs > 0 ? cfg.ZKTeco.CardCooldownMs : 2000;
                    if (!_lastScanByUid.TryGetValue(uid, out var last)) last = DateTime.MinValue;
                    if ((DateTime.Now - last).TotalMilliseconds < cooldown) return;

                    _lastScanByUid[uid] = DateTime.Now;
                }
                catch { }

                var mapping = ReaderLaneMappingService.Instance.GetMappingByReader(readerNo);
                if (mapping != null)
                {
                    int laneIndex = mapping.LaneIndex;
                    Task.Run(() => {
                        try {
                            string cam1 = (laneIndex == 1) ? "Vao1" : "Ra1";
                            string cam2 = (laneIndex == 1) ? "Vao2" : "Ra2";
                            
                            lock (_currentFrames) {
                                if (_currentFrames.TryGetValue(cam1, out var bmp1)) {
                                    var img1 = ConvertBitmap((System.Drawing.Bitmap)bmp1.Clone());
                                    Dispatcher.BeginInvoke(new Action(() => vm.UpdateLaneSnapshot(laneIndex, 1, img1)));
                                }
                                if (_currentFrames.TryGetValue(cam2, out var bmp2)) {
                                    var img2 = ConvertBitmap((System.Drawing.Bitmap)bmp2.Clone());
                                    Dispatcher.BeginInvoke(new Action(() => vm.UpdateLaneSnapshot(laneIndex, 2, img2)));
                                }
                            }
                        } catch { }
                    });
                }

                await vm.ProcessScanFromReaderAsync(readerNo, uid);
            }));
        }


        // ── Quản lý thẻ ──────────────────────────────────────────────────────────

        private void MoQuanLyThe(object sender, RoutedEventArgs e)
        {
            RFIDService.Instance.OnCardScanned -= OnRfidScanned;
            C3200Service.Instance.OnCardScanned -= OnC3200Scanned;

            new QuanLyThe().ShowDialog();

            RFIDService.Instance.OnCardScanned += OnRfidScanned;
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
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
                var win = creator();
                win.Title = title;
                win.Owner = this;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                win.ShowDialog();
                
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
                if (vm.CurrentView is DashboardViewModel)
                    btnDashboard.IsChecked = true;
                else
                    btnParkingView.IsChecked = true;
            }
        }

        private async Task OpenGateAsync(int doorNumber)
        {
            // Gọi Service xử lý trọn gói: Chụp ảnh -> Mở cổng -> Ghi Log
            await _gateControlService.ProcessGateActionAsync(doorNumber, _currentFrames, "MANUAL_OPEN", "Mở từ giao diện phần mềm");

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
            var win = new Views.ChangePasswordWindow
            {
                Owner = this,
                DataContext = new ViewModels.ChangePasswordViewModel()
            };
            win.ShowDialog();
        }

        private void MoThongTinCaNhan_Click(object sender, RoutedEventArgs e)
        {
            var win = new Views.UserProfileWindow
            {
                Owner = this,
                DataContext = new ViewModels.UserProfileViewModel()
            };
            win.ShowDialog();
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
            new HistoryWindow().ShowDialog();
            RestoreSidebarSelection();
        }

        private void MoLichSuGiaHan_Click(object sender, RoutedEventArgs e) =>
            ShowModuleModal("📜 Lịch sử gia hạn thẻ", () => new RFIDGiaHanHistoryWindow());

        private async void MoSQLTool_Click(object sender, RoutedEventArgs e) 
        {
            try
            {
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
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MoCameraSettings_Click(object sender, RoutedEventArgs e)
        {
            new CameraSettingsWindow { Owner = this }.ShowDialog();
            RestoreSidebarSelection();
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGrid dg && dg.SelectedItem is Xe xe)
                new VehicleDetailWindow(xe).ShowDialog();
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

        private void MoRealtimeLog_Click(object sender, RoutedEventArgs e) =>
            ShowModuleModal("📋 Nhật ký hệ thống", () => new RealtimeLogWindow());

        private void MoQuanLyNguoiDung_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var frm = new QuanLyGiuXe.Views.UserManagementForm();
                frm.ShowDialog();
                RestoreSidebarSelection();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("UserManagement", "MoQuanLyNguoiDung_Click", "Lỗi mở Quản lý người dùng", ex);
                MessageBox.Show($"Không thể mở Quản lý người dùng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== MODULE CRUD HANDLER =====
        private void OpenModule_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement)?.Tag?.ToString();
            try
            {
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
                MessageBox.Show(ex.ToString(), "Lỗi khi mở module", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var win = new OfflineQADashboard { Owner = this };
            win.Closed += (s, ev) => RestoreSidebarSelection();
            win.Show();
        }

        private void MoBackupRestore_Click(object sender, RoutedEventArgs e)
        {
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
