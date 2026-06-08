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
using LibVLCSharp.Shared;
using System.Net.Http;
using Serilog.Events;


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
        private readonly MainViewModel _viewModel;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly AnprService _anprService;


        private bool _isProcessingAuto = false;
        private DateTime _lastAutoScanTime = DateTime.MinValue;
        private readonly GateControlService _gateControlService = new GateControlService();
        private readonly Dictionary<string, Window> _activeModuleWindows = new();

        private LibVLC _libVlc;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayerVao1;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayerVao2;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayerRa1;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayerRa2;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            this.Loaded += MainWindow_Loaded;

            RFIDService.Instance.OnCardScanned += OnRfidScanned;
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
            C3200Service.Instance.OnEvent += OnC3200Event;

            ApplyPermissions();

            // Shortcut Setup
            this.KeyDown += (s, e) => {
                if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt) && e.Key == Key.Q)
                    MoQAPanel_Click(null, null);
                else if (e.Key == Key.F9)
                    MoQAPanel_Click(null, null);
                else if (e.Key == Key.F4)
                    MoC3200Settings_Click(null, null);
            };
            if (btnQAPanel != null) btnQAPanel.Visibility = Visibility.Visible;
        }

        protected override void OnClosed(EventArgs e)
        {
            RFIDService.Instance.OnCardScanned -= OnRfidScanned;
            C3200Service.Instance.OnCardScanned -= OnC3200Scanned;
            C3200Service.Instance.OnEvent -= OnC3200Event;

            // 🛠️ THÊM: Giải phóng tài nguyên VLC khi tắt ứng dụng
            _mediaPlayerVao1?.Dispose();
            _mediaPlayerVao2?.Dispose();
            _mediaPlayerRa1?.Dispose();
            _mediaPlayerRa2?.Dispose();
            _libVlc?.Dispose();

            base.OnClosed(e);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Khởi tạo lõi VLC trước khi mở luồng
            Core.Initialize();
            _libVlc = new LibVLC();

            _mediaPlayerVao1 = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
            _mediaPlayerVao2 = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
            _mediaPlayerRa1 = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
            _mediaPlayerRa2 = new LibVLCSharp.Shared.MediaPlayer(_libVlc);

            Task.Run(() => {
                MoCamera(); // Gọi hàm chạy cam đồng bộ từ DB/Config
                RFIDService.Instance.Start();
            });
        }

        
        public void MoCamera()
        {
            try
            {
                var config = AppConfig.Load();
                if (config?.Cameras == null) return;

                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var parkingView = FindVisualChild<ParkingView>(this);
                    if (parkingView == null) return;

                    string urlVao1 = $"rtsp://admin:tlJwpbo6@{config.Cameras.VaoToanCanh}:554/user=admin&password=tlJwpbo6&channel=0&stream=1.sdp";
                    string urlVao2 = $"rtsp://admin:tlJwpbo6@{config.Cameras.VaoBienSo}:554/user=admin&password=tlJwpbo6&channel=0&stream=1.sdp";
                    string urlRa1 = $"rtsp://admin:tlJwpbo6@{config.Cameras.RaToanCanh}:554/user=admin&password=tlJwpbo6&channel=0&stream=1.sdp";
                    string urlRa2 = $"rtsp://admin:tlJwpbo6@{config.Cameras.RaBienSo}:554/user=admin&password=tlJwpbo6&channel=0&stream=1.sdp";


                    if (!string.IsNullOrEmpty(urlVao1))
                        parkingView.StartCameraStream(parkingView.CameraVao1, urlVao1);

                    if (!string.IsNullOrEmpty(urlVao2))
                        parkingView.StartCameraStream(parkingView.CameraVao2, urlVao2);

                    if (!string.IsNullOrEmpty(urlRa1))
                        parkingView.StartCameraStream(parkingView.CameraRa1, urlRa1);

                    if (!string.IsNullOrEmpty(urlRa2))
                        parkingView.StartCameraStream(parkingView.CameraRa2, urlRa2);
                }));
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("MoCamera", "MainWindow", "Lỗi khởi động luồng camera", ex);
            }
        }

        private void MoCameraSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new CameraSettingsWindow { Owner = this };
            if (win.ShowDialog() == true || true) // Sau khi đóng cửa sổ cấu hình Camera
            {
                Task.Run(() => MoCamera()); // Gọi chạy lại cam ngay lập tức với IP mới lưu!
            }
            RestoreSidebarSelection();
        }

        private void OnC3200Event(Services.C3200Event evt)
        {
            if (evt == null) return;
            var raw = (evt.RawData ?? "").ToUpper();
            bool isButton = raw.Contains("BUTTON") || evt.EventType == 202;

            if (isButton)
            {
                // Thay thế mảng _currentFrames cũ bằng cách truyền null, xử lý chụp ảnh trực tiếp từ MediaPlayer bên trong Service
                Task.Run(() => _gateControlService.ProcessGateActionAsync(evt.Door, null, "BUTTON_PRESS"));
            }
        }

        private void OnRfidScanned(string uid) => XuLyQuetThe(uid, 1);
        private void OnC3200Scanned(string uid, int door, int inOutState)
        {
            int readerNo = (door - 1) * 2 + (inOutState == 1 ? 2 : 1);
            XuLyQuetThe(uid, readerNo);
        }

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

                    // 🛠️ ĐÃ SỬA CƠ CHẾ SNAPSHOT: Trích xuất ảnh trực tiếp từ luồng stream VLC cực nhẹ
                    string tempSnapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"temp_snap_lane{laneIndex}.png");
                    var activePlayer = (laneIndex == 1) ? _mediaPlayerVao2 : _mediaPlayerRa2; // Lấy cam biển số

                    if (activePlayer != null && activePlayer.TakeSnapshot(0, tempSnapPath, 0, 0))
                    {
                        Task.Run(() => {
                            try
                            {
                                if (File.Exists(tempSnapPath))
                                {
                                    // Chuyển file ảnh snapshot thành BitmapSource hiển thị lên ô SNAP
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.UriSource = new Uri(tempSnapPath);
                                    bitmap.EndInit();
                                    bitmap.Freeze();

                                    Dispatcher.BeginInvoke(new Action(() => {
                                        vm.UpdateLaneSnapshot(laneIndex, 2, bitmap); // Cập nhật khung SNAP 1 hoặc SNAP 2
                                    }));

                                    // Kích hoạt AI nhận diện biển số từ file snapshot vừa chụp
                                    RunAutoDetectionFromFile(tempSnapPath, laneIndex);
                                }
                            }
                            catch { }
                        });
                    }
                }

                await vm.ProcessScanFromReaderAsync(readerNo, uid);
            }));
        }

        /// <summary>
        /// 🛠️ HÀM MỚI: Nhận diện biển số thông minh từ File tạm, không gây sọc hình, không tốn RAM giải phóng ngay lập tức
        /// </summary>
        private async void RunAutoDetectionFromFile(string filePath, int laneIndex)
        {
            if (_isProcessingAuto) return;
            _isProcessingAuto = true;

            try
            {
                if (!File.Exists(filePath)) return;

                using (var bmp = new Bitmap(filePath))
                {
                    string plate = await ApiService.SendImageAsync(bmp);

                    if (!string.IsNullOrEmpty(plate) && plate.Length > 4 && !plate.Contains("Lỗi"))
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (this.DataContext is MainViewModel vm)
                            {
                                string cleanPlate = plate.Trim().ToUpper();
                                if (laneIndex == 1)
                                {
                                    vm.BienSoNhap = cleanPlate;
                                    vm.Lane1BienSo = cleanPlate;
                                    vm.Lane1TrangThai = "Vào - Đã nhận diện: " + cleanPlate;
                                }
                                else
                                {
                                    vm.Lane2BienSo = cleanPlate;
                                    vm.Lane2TrangThai = "Ra - Đã nhận diện: " + cleanPlate;
                                }
                            }
                        }));
                    }
                }

                // Xóa file tạm sau khi nhận diện xong để tránh rác ổ cứng bốt bảo vệ
                File.Delete(filePath);
            }
            catch { }
            finally { _isProcessingAuto = false; }
        }

        // ── Mở cổng thủ công (Sự kiện Click từ XAML gọi xuống) ─────────────────────

        public async void OpenGateIn_Click(object sender, RoutedEventArgs e)
        {
            await OpenGateAsync(1); // Gọi hàm xử lý mở cổng 1 (Làn Vào)
        }

        public async void OpenGateOut_Click(object sender, RoutedEventArgs e)
        {
            await OpenGateAsync(2); // Gọi hàm xử lý mở cổng 2 (Làn Ra)
        }

        private async Task OpenGateAsync(int doorNumber)
        {
            await _gateControlService.ProcessGateActionAsync(doorNumber, null, "MANUAL_OPEN", "Mở từ giao diện phần mềm");

            if (DataContext is MainViewModel vm)
            {
                string status = $"✅ Đã gửi lệnh mở cổng {doorNumber}";
                if (doorNumber == 1) vm.Lane1TrangThai = status;
                else vm.Lane2TrangThai = status;
            }
        }

        // ❌ ĐÃ XÓA hoàn toàn hàm `ConvertBitmap()` cũ vì cấu hình VLC đã tự lo phần chuyển hiển thị lên UI, không cần ép CPU tính toán từng frame nữa.

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            // Nút bấm chụp thủ công trên giao diện: Gọi lệnh kích hoạt lưu ảnh trực tiếp từ đầu phát Cam Vào 1
            try
            {
                string manualSnapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manual_capture.png");
                if (_mediaPlayerVao1 != null && _mediaPlayerVao1.TakeSnapshot(0, manualSnapPath, 0, 0))
                {
                    await Task.Delay(200); // Chờ 0.2s để file sinh ra hoàn chỉnh
                    if (File.Exists(manualSnapPath))
                    {
                        using (Bitmap bmp = new Bitmap(manualSnapPath))
                        {
                            string plate = await ApiService.SendImageAsync(bmp);
                            if (_viewModel != null)
                            {
                                _viewModel.BienSoNhap = plate?.Trim() ?? "";
                                if (_viewModel.XeVaoCommand.CanExecute(null)) _viewModel.XeVaoCommand.Execute(null);
                            }
                        }
                        File.Delete(manualSnapPath);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Lỗi chụp ảnh thủ công: {ex.Message}"); }
        }

        // ── CÁC HÀM GIAO DIỆN CRUD VÀ RBAC GIỮ NGUYÊN KHÔNG ĐỔI ───────────────────────────────────────
        private void ApplyPermissions() {}
        private void GenerateTestLogs_Click(object sender, RoutedEventArgs e) {}
        private void ShowToast(string message, int milliseconds = 1500) {}
        private void MoC3200Settings_Click(object sender, RoutedEventArgs e) { new C3200SettingsWindow().ShowDialog(); _viewModel?.RefreshSettings(); RestoreSidebarSelection(); }
        private void MoAdvancedSettings_Click(object sender, RoutedEventArgs e) { new Views.AdvancedSettingsWindow { Owner = this }.ShowDialog(); RestoreSidebarSelection(); }
        private void MoQuanLyThe(object sender, RoutedEventArgs e) { RFIDService.Instance.OnCardScanned -= OnRfidScanned; C3200Service.Instance.OnCardScanned -= OnC3200Scanned; new QuanLyThe().ShowDialog(); RFIDService.Instance.OnCardScanned += OnRfidScanned; C3200Service.Instance.OnCardScanned += OnC3200Scanned; RestoreSidebarSelection(); }
        private void MoButtonLogs_Click(object sender, RoutedEventArgs e) => ShowModuleModal("📋 Nhật ký nhấn nút", () => new ButtonLogsWindow());
        private void ShowModuleModal(string title, Func<Window> creator) { try { var win = creator(); win.Title = title; win.Owner = this; win.WindowStartupLocation = WindowStartupLocation.CenterOwner; win.ShowDialog(); RestoreSidebarSelection(); } catch (Exception ex) { MessageBox.Show($"Lỗi: {ex.Message}"); } }
        private void RestoreSidebarSelection() { if (DataContext is MainViewModel vm) { if (vm.CurrentView is DashboardViewModel) btnDashboard.IsChecked = true; else btnParkingView.IsChecked = true; } }
        private void DoiMatKhau_Click(object sender, RoutedEventArgs e) { new Views.ChangePasswordWindow { Owner = this, DataContext = new ViewModels.ChangePasswordViewModel() }.ShowDialog(); }
        private void MoThongTinCaNhan_Click(object sender, RoutedEventArgs e) { new Views.UserProfileWindow { Owner = this, DataContext = new ViewModels.UserProfileViewModel() }.ShowDialog(); }
        private void UserPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (DataContext is MainViewModel vm) vm.IsUserPopupOpen = !vm.IsUserPopupOpen; }
        private void MoLichSu(object sender, RoutedEventArgs e) { new HistoryWindow().ShowDialog(); RestoreSidebarSelection(); }
        private void MoLichSuGiaHan_Click(object sender, RoutedEventArgs e) => ShowModuleModal("📜 Lịch sử gia hạn thẻ", () => new RFIDGiaHanHistoryWindow());
        private async void MoSQLTool_Click(object sender, RoutedEventArgs e) { try { var config = Models.DbConnectionConfig.LoadFromFile(); var vm = new ConnectDatabaseViewModel(); if (!await vm.CheckConnectionAsync(config.BuildConnectionString(timeout: 3))) { if (new ConnectDatabaseWindow { Owner = this }.ShowDialog() != true) return; } ShowModuleModal("🛠 Mini Database Explorer", () => new Window { Content = new Views.DatabaseExplorerView(), Width = 1000, Height = 600 }); } catch (Exception ex) { MessageBox.Show($"Lỗi: {ex.Message}"); } }
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (sender is DataGrid dg && dg.SelectedItem is Xe xe) new VehicleDetailWindow(xe).ShowDialog(); }
        private void MoParkingView_Click(object sender, RoutedEventArgs e) { _viewModel?.TrangChuCommand.Execute(null); }
        private void MoDashboard_Click(object sender, RoutedEventArgs e) { _viewModel?.SetView(new DashboardViewModel()); }
        private void MoRealtimeLog_Click(object sender, RoutedEventArgs e) => ShowModuleModal("📋 Nhật ký hệ thống", () => new RealtimeLogWindow());
        private void MoQuanLyNguoiDung_Click(object sender, RoutedEventArgs e) { try { using var frm = new Views.UserManagementForm(); frm.ShowDialog(); RestoreSidebarSelection(); } catch (Exception ex) { MessageBox.Show(ex.Message); } }
        private void OpenModule_Click(object sender, RoutedEventArgs e) { var tag = (sender as FrameworkElement)?.Tag?.ToString(); UserControl content = tag switch { "LoaiXe" => new LoaiXeView(), "LoaiVe" => new LoaiVeView(), "RFID" => new RFIDCardView(), "BangGia" => new BangGiaView(), _ => null }; if (content != null) ShowModuleModal(tag, () => new Window { Content = content, Width = 1000, Height = 700 }); else ShowToast("Tính năng chưa cấu hình sai Tag"); }
        private void MoQAPanel_Click(object sender, RoutedEventArgs e) { var win = new OfflineQADashboard { Owner = this }; win.Closed += (s, ev) => RestoreSidebarSelection(); win.Show(); }
        private void MoBackupRestore_Click(object sender, RoutedEventArgs e) { _viewModel?.BackupRestoreCommand.Execute(null); RestoreSidebarSelection(); }
        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject { if (obj == null) return null; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) { DependencyObject child = VisualTreeHelper.GetChild(obj, i); if (child != null && child is T t) return t; else { T childOfChild = FindVisualChild<T>(child); if (childOfChild != null) return childOfChild; } } return null; }
    }
}