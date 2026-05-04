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
using System.Net.Http;

namespace QuanLyGiuXe
{
    public partial class MainWindow : Window
    {
        private readonly object _manualOpenLock = new();
        private readonly System.Collections.Generic.Dictionary<int, DateTime> _lastManualOpen = new();
        private readonly Dictionary<string, DateTime> _lastScanByUid = new();
        private MainViewModel _viewModel;
        private CameraService _cameraService = new CameraService();
        private AnprService _anprService = new AnprService();
        private Dictionary<string, Bitmap> _currentFrames = new();
        private bool _isProcessingAuto = false;
        private DateTime _lastAutoScanTime = DateTime.MinValue;
        private readonly GateControlService _gateControlService = new GateControlService();
        private bool _isProcessing = false;
        private readonly HttpClient _httpClient = new HttpClient();
        private DateTime _lastProcessingTime = DateTime.MinValue;
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel(); // Khởi tạo 1 lần
            this.DataContext = _viewModel;
            _anprService.OnDetectionCompleted += (result) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    _viewModel.LanVaoBienSo = result.Plate;
                    _viewModel.LanVaoRoiImage = result.RoiImage;
                    _viewModel.PathAnhVao = result.SavedPath; // Lưu đường dẫn để Insert DB
                    _viewModel.LanVaoTrangThai = "✅ Nhận diện: " + result.Plate;
                });
            };
            MoCameras();

            RFIDService.Instance.OnCardScanned += OnRfidCardScanned;
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

        private async void OnRfidCardScanned(string uid)
        {
            var vm = (MainViewModel)this.DataContext;
            vm.CurrentCardUID = uid; // Khi gán thế này, UI sẽ tự nhảy số
        }
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
            RFIDService.Instance.OnCardScanned -= OnRfidCardScanned;
            C3200Service.Instance.OnCardScanned -= OnC3200Scanned;

            new QuanLyThe().ShowDialog();

            RFIDService.Instance.OnCardScanned += OnRfidCardScanned;
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

        private System.Windows.Media.ImageSource Base64ToImageSource(string base64String)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64String);
                using (var ms = new System.IO.MemoryStream(imageBytes))
                {
                    var imageSource = new System.Windows.Media.Imaging.BitmapImage();
                    imageSource.BeginInit();
                    imageSource.StreamSource = ms;
                    imageSource.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    imageSource.EndInit();
                    imageSource.Freeze();
                    return imageSource;
                }
            }
            catch { return null; }
        }
        private async Task XuLyNhanDienBienSo(Bitmap bmp)
        {
            try
            {
                // 1. RESIZE: Đưa ảnh về chuẩn 800px để nét vẽ AI to và rõ hơn
                int targetWidth = 800;
                int targetHeight = (bmp.Height * targetWidth) / bmp.Width;
                using var resizedBmp = new Bitmap(bmp, new System.Drawing.Size(targetWidth, targetHeight));

                using var ms = new System.IO.MemoryStream();
                // 2. CHẤT LƯỢNG: Lưu ở định dạng Jpeg với chất lượng cao nhất
                resizedBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                var byteData = ms.ToArray();

                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(byteData), "image", "frame.jpg");

                var response = await _httpClient.PostAsync("http://127.0.0.1:5000/process_plate", content);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);

                    Dispatcher.Invoke(() => {
                        if (this.DataContext is MainViewModel viewModel)
                        {
                            // 1. XỬ LÝ CHỮ BIỂN SỐ (OCR)
                            if (result.results != null && result.results.Count > 0)
                            {
                                // Lấy kết quả biển số từ API
                                string plateText = result.results[0].plate ?? "Không có text";
                                viewModel.LanVaoBienSo = plateText.ToString().ToUpper();
                            }
                            else
                            {
                                // Nếu API trả về results trống hoặc no_plate
                                viewModel.LanVaoBienSo = "Chưa đọc được biển số";
                            }

                            // 2. XỬ LÝ ẢNH ROI (Đọc từ ổ cứng máy khách như bạn muốn)
                            string roiPath = @"D:\APS\AI_Plate_Recognition\static\debug\last_roi.jpg";

                            // Gọi hàm LoadImageFromFile đã viết ở bước trước
                            var anhRoi = LoadImageFromFile(roiPath);
                            if (anhRoi != null)
                            {
                                viewModel.LanVaoRoiImage = anhRoi;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi API: " + ex.Message);
            }
        }
        private void MoCameras()
        {
            _cameraService.CamKey = "Vao1";

            _cameraService.NewFrameReceived += (s, data) =>
            {
                // Lúc này data.CamKey sẽ mang giá trị "Vao1"
                Dispatcher.BeginInvoke(new Action(() => {
                    if (data.CamKey == "Vao1") CameraVao1.Source = data.FrameForUI;
                    else if (data.CamKey == "Ra1") CameraRa1.Source = data.FrameForUI;
                }));
                if (data.CamKey == "Vao1" && !_isProcessing && (DateTime.Now - _lastProcessingTime).TotalSeconds >= 1)
                {
                    _isProcessing = true;
                    _lastProcessingTime = DateTime.Now;
                    Task.Run(async () => {
                        await XuLyNhanDienBienSo(data.Frame);
                        _isProcessing = false;
                    });
                }
            };

            string rtspUrl = "rtsp://192.168.1.121:554/user=admin&password=tlJwpbo6&channel=0&stream=0.sdp";
            _cameraService.StartIpCamera("Vao1", rtspUrl);
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFrames.TryGetValue("Vao1", out var bitmapToProcess))
            {
                await _anprService.ProcessAutoDetectionAsync(bitmapToProcess);
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
        private void MoDashboard_Click(object sender, RoutedEventArgs e)
        {
            var content = new QuanLyGiuXe.Views.DashboardView();
            var win = new Window
            {
                Title = "Hệ thống Dashboard Thống kê",
                Content = content,
                Owner = this,
                Width = 1200,
                Height = 850,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            win.Show();
        }

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
        private System.Windows.Media.ImageSource LoadImageFromFile(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return null;

                var image = new System.Windows.Media.Imaging.BitmapImage();
                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                {
                    image.BeginInit();
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi load ảnh ROI: " + ex.Message);
                return null;
            }
        }
    }
}
