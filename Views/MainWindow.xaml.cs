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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            MoCameras();

            RFIDService.Instance.OnCardScanned += OnRfidScanned;
            RFIDService.Instance.Start();
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
            // subscribe to full RT events to record button presses
            C3200Service.Instance.OnEvent += OnC3200Event;
            this.Closing += (s, e) => C3200Service.Instance.OnEvent -= OnC3200Event;
            // UI: logs menu is defined in XAML (TopPanel) — no dynamic button needed here
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
                    try { toast.Dispatcher.Invoke(() => toast.Close()); } catch { }
                });
            }
            catch { }
        }

        private void MoC3200Settings_Click(object sender, RoutedEventArgs e)
        {
            new C3200SettingsWindow().ShowDialog();
        }

        private void MoAdvancedSettings_Click(object sender, RoutedEventArgs e)
        {
            new Views.AdvancedSettingsWindow { Owner = this }.ShowDialog();
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

        private void OnRfidScanned(string uid) => XuLyQuetThe(uid);
        private void OnC3200Scanned(string uid, int door) => XuLyQuetThe(uid, door);

        // ── Xử lý quẹt thẻ (dùng chung cho RFID USB + C3-200) ───────────────────

        private void XuLyQuetThe(string uid, int door = 0)
        {
            Dispatcher.Invoke(() =>
            {
                if (DataContext is not MainViewModel vm) return;

                var cfg = AppConfig.Load();

                int logicalDoor = 0;
                if (door != 0)
                {
                    if (cfg.ZKTeco.ForceAllIn)
                    {
                        logicalDoor = 1;
                    }
                    else if (cfg.ZKTeco.ForceAllOut)
                    {
                        logicalDoor = 2;
                    }
                    else
                    {
                        var inSet = new HashSet<int>();
                        var outSet = new HashSet<int>();

                        foreach (var part in (cfg.ZKTeco.GateInDoors ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                            if (int.TryParse(part.Trim(), out var v)) inSet.Add(v);

                        foreach (var part in (cfg.ZKTeco.GateOutDoors ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                            if (int.TryParse(part.Trim(), out var v)) outSet.Add(v);

                        if (inSet.Contains(door)) logicalDoor = 1;
                        else if (outSet.Contains(door)) logicalDoor = 2;
                        else logicalDoor = 0;
                    }
                }

                try
                {
                    uid = RFIDService.ChuanHoaUID(uid);

                    int cooldown = cfg.ZKTeco.CardCooldownMs > 0 ? cfg.ZKTeco.CardCooldownMs : 2000;

                    if (!_lastScanByUid.TryGetValue(uid, out var last)) last = DateTime.MinValue;

                    if ((DateTime.Now - last).TotalMilliseconds < cooldown)
                        return;

                    _lastScanByUid[uid] = DateTime.Now;
                }
                catch { }

                var db = new DatabaseService();
                var card = db.GetRFIDCardByUid(uid);

                if (card == null || card.Id == 0)
                {
                    string msg = $"❌ Thẻ {uid} chưa đăng ký!";
                    if (logicalDoor == 1) vm.LanVaoTrangThai = msg;
                    else if (logicalDoor == 2) vm.LanRaTrangThai = msg;
                    else MessageBox.Show(msg, "Lỗi thẻ");
                    return;
                }

                vm.BienSoNhap = card.BienSo ?? string.Empty;
                vm.LastScannedUID = uid;

                // 🔥 FIX CHÍNH: dùng CardId
                bool xeTrongBai = db.IsXeTrongBaiByCardId(card.Id);

                // ── CỔNG VÀO ──
                if (logicalDoor == 1)
                {
                    if (xeTrongBai)
                    {
                        vm.LanVaoTrangThai = $"⚠ {card.BienSo ?? uid} đã trong bãi!";
                        return;
                    }

                    vm.XeVaoCommand.Execute(null);
                    return;
                }

                // ── CỔNG RA ──
                if (logicalDoor == 2)
                {
                    if (!xeTrongBai)
                    {
                        vm.LanRaTrangThai = $"⚠ {card.BienSo ?? uid} không có trong bãi!";
                        return;
                    }

                    vm.XeRaCommand.Execute(null);
                    return;
                }

                // ── AUTO MODE ──
                if (xeTrongBai)
                    vm.XeRaCommand.Execute(null);
                else
                    vm.XeVaoCommand.Execute(null);
            });
        }

        // ── Quản lý thẻ ──────────────────────────────────────────────────────────

        private void MoQuanLyThe(object sender, RoutedEventArgs e)
        {
            RFIDService.Instance.OnCardScanned -= OnRfidScanned;
            C3200Service.Instance.OnCardScanned -= OnC3200Scanned;

            new QuanLyThe().ShowDialog();

            RFIDService.Instance.OnCardScanned += OnRfidScanned;
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
        }

        // ── Mở cổng thủ công ─────────────────────────────────────────────────────

        private async void OpenGateIn_Click(object sender, RoutedEventArgs e) =>
            await OpenGateAsync(1);

        private async void OpenGateOut_Click(object sender, RoutedEventArgs e) =>
            await OpenGateAsync(2);

        private void MoButtonLogs_Click(object sender, RoutedEventArgs e) =>
            new ButtonLogsWindow().Show();

        private async Task OpenGateAsync(int doorNumber)
        {
            // Gọi Service xử lý trọn gói: Chụp ảnh -> Mở cổng -> Ghi Log
            await _gateControlService.ProcessGateActionAsync(doorNumber, _currentFrames, "MANUAL_OPEN", "Mở từ giao diện phần mềm");

            // (Tùy chọn) Cập nhật trạng thái lên UI để người dùng biết
            if (DataContext is MainViewModel vm)
            {
                string status = $"✅ Đã gửi lệnh mở cổng {doorNumber}";
                if (doorNumber == 1) vm.LanVaoTrangThai = status;
                else vm.LanRaTrangThai = status;
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
                        switch (data.CamKey)
                        {
                            case "Vao1": CameraVao1.Source = uiImage; break;
                            case "Vao2": CameraVao2.Source = uiImage; break;
                            case "Ra1": CameraRa1.Source = uiImage; break;
                            case "Ra2": CameraRa2.Source = uiImage; break;
                        }
                    }));
                }
                if (data.CamKey == "Vao1")
                {
                    RunAutoDetection(data.Frame);
                }
            };

            string rtspUrl = "rtsp://192.168.1.121:554/user=admin&password=tlJwpbo6&channel=0&stream=0.sdp";
            _cameraService.StartIpCamera("Vao1", rtspUrl);
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
                    this.Dispatcher.Invoke(() =>
                    {
                        if (this.DataContext is MainViewModel vm)
                        {
                            // Gán vào ô "Biển số nhập"
                            vm.BienSoNhap = plate.Trim().ToUpper();

                            // (Tùy chọn) Thông báo trạng thái để người dùng biết đã nhận diện xong
                            vm.LanVaoTrangThai = "Đã nhận diện: " + vm.BienSoNhap;
                        }
                    });
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
                        this.Dispatcher.Invoke(() =>
                        {
                            vm.BienSoNhap = plate?.Trim() ?? "";
                            if (vm.XeVaoCommand.CanExecute(null))
                            {
                                vm.XeVaoCommand.Execute(null);
                            }
                        });
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

        private void MoLichSu(object sender, RoutedEventArgs e) =>
            new HistoryWindow().ShowDialog();

        private void MoLichSuGiaHan_Click(object sender, RoutedEventArgs e) =>
            new RFIDGiaHanHistoryWindow().Show();

        private void MoSQLTool_Click(object sender, RoutedEventArgs e) 
        {
            var content = new QuanLyGiuXe.Views.DatabaseExplorerView();
            var win = new Window
            {
                Title = "Mini Database Explorer",
                Content = content,
                Owner = this,
                Width = 1000,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            win.Show();
        }

        private void MoCameraSettings_Click(object sender, RoutedEventArgs e) =>
            new CameraSettingsWindow { Owner = this }.ShowDialog();

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGrid dg && dg.SelectedItem is Xe xe)
                new VehicleDetailWindow(xe).ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // Ép tắt toàn bộ ứng dụng và tất cả các luồng ngầm (camera, thẻ, v.v.)
            Environment.Exit(0);
        }

        // ===== SIDEBAR HANDLERS =====
        private void MoRealtimeLog_Click(object sender, RoutedEventArgs e)
        {
            new RealtimeLogWindow { Owner = this }.Show();
        }

        private void MoDanhSachRFID(object sender, RoutedEventArgs e)
        {
            ShowToast("Mở danh sách thẻ RFID (chưa triển khai)");
        }

        // ===== MODULE CRUD HANDLER =====
        private void OpenModule_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag?.ToString();
            try
            {
                UserControl content = null;
                string title = "";
                switch (tag)
                {
                    case "LoaiXe":
                        content = (UserControl)Application.LoadComponent(new Uri("Views/LoaiXeView.xaml", UriKind.Relative));
                        title = "Quản lý Loại Xe";
                        break;
                    case "LoaiVe":
                        content = (UserControl)Application.LoadComponent(new Uri("Views/LoaiVeView.xaml", UriKind.Relative));
                        title = "Quản lý Loại Vé";
                        break;
                    case "RFID":
                        content = (UserControl)Application.LoadComponent(new Uri("Views/RFIDCardView.xaml", UriKind.Relative));
                        title = "Quản lý RFID";
                        break;
                    case "BangGia":
                        // load admin BangGia view (inline edit only)
                        content = (UserControl)Application.LoadComponent(new Uri("Views/BangGiaView.xaml", UriKind.Relative));
                        title = "Bảng giá (Quản trị)";
                        break;
                }

                if (content != null)
                {
                    var win = new Window
                    {
                        Title = title,
                        Content = content,
                        Owner = this,
                        Width = 900,
                        Height = 600,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    win.Show();
                }
                else
                {
                    ShowToast("Tính năng chưa có giao diện: " + (tag ?? "(unknown)"));
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
    }
}
