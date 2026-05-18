using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using LibVLCSharp.Shared;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, DateTime> _lastScanByUid = new();
        private readonly MainViewModel _viewModel;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly AnprService _anprService;
        private readonly GateControlService _gateControlService = new GateControlService();
        private Dictionary<string, Bitmap> _currentFrames = new();

        // 1. TÁCH BIỆN PHÁP QUẢN LÝ 2 LUỒNG CAMERA SONG SONG
        private readonly LibVLC _libVlc;
        private readonly LibVLCSharp.Shared.MediaPlayer _mediaPlayerToanCanh;
        private readonly LibVLCSharp.Shared.MediaPlayer _mediaPlayerBienSo;

        public MainWindow()
        {
            // Khởi tạo thư viện VLC gốc trước khi giao diện load
            Core.Initialize();

            // Cấu hình tăng tốc phần cứng, giảm độ trễ luồng RTSP (TCP, buffer 200ms)
            var options = new string[] { "--network-caching=200", "--rtsp-tcp", "--no-stats", "--skip-frames" };
            _libVlc = new LibVLC(options);

            _mediaPlayerToanCanh = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
            _mediaPlayerBienSo = new LibVLCSharp.Shared.MediaPlayer(_libVlc);

            InitializeComponent();
            _anprService = new AnprService(_httpClient);
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            // KẾT NỐI XAML: Gán 2 MediaPlayer vào đúng tên 2 ô hiển thị bạn đã làm bên giao diện
            if (videoView != null) videoView.MediaPlayer = _mediaPlayerToanCanh;   // Ô Cam 1 - Toàn cảnh
            if (videoView2 != null) videoView2.MediaPlayer = _mediaPlayerBienSo; // Ô Cam 2 - Biển số

            // Lắng nghe sự kiện AI xử lý xong để trả thông tin về màn hình
            _anprService.OnDetectionCompleted += (result) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    _viewModel.LanVaoBienSo = result.Plate;
                    _viewModel.LanVaoRoiImage = result.RoiImage;
                    _viewModel.PathAnhVao = result.SavedPath;
                    _viewModel.LanVaoTrangThai = "✅ Nhận diện: " + result.Plate;
                });
            };

            // Mở đồng loạt 2 Camera
            MoCameras();

            // Khởi chạy hệ thống thẻ
            RFIDService.Instance.OnCardScanned += OnRfidCardScanned;
            RFIDService.Instance.Start();
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
            C3200Service.Instance.OnEvent += OnC3200Event;

            // Giải phóng luồng an toàn khi đóng ứng dụng
            this.Closing += (s, e) => {
                C3200Service.Instance.OnEvent -= OnC3200Event;
                C3200Service.Instance.OnCardScanned -= OnC3200Scanned;
                RFIDService.Instance.OnCardScanned -= OnRfidCardScanned;

                _mediaPlayerToanCanh?.Stop(); _mediaPlayerToanCanh?.Dispose();
                _mediaPlayerBienSo?.Stop(); _mediaPlayerBienSo?.Dispose();
                _libVlc?.Dispose();
                Environment.Exit(0);
            };
        }

        // --- HÀM MỞ 2 CAMERA ĐỘNG THEO FILE CONFIG CHUẨN ---
        private void MoCameras()
        {
            try
            {
                var config = AppConfig.Load();
                if (config?.Cameras == null) return;

                string ipToanCanh = config.Cameras.VaoToanCanh;
                string ipBienSo = config.Cameras.VaoBienSo;

                // Kích hoạt Cam 1 (Toàn cảnh)
                if (!string.IsNullOrEmpty(ipToanCanh))
                {
                    string urlToanCanh = $"rtsp://admin:tlJwpbo6@{ipToanCanh}:554/user=admin&password=tlJwpbo6&channel=0&stream=0.sdp";
                    var media = new LibVLCSharp.Shared.Media(_libVlc, urlToanCanh, FromType.FromLocation);
                    _mediaPlayerToanCanh.Play(media);
                }

                // Kích hoạt Cam 2 (Biển số)
                if (!string.IsNullOrEmpty(ipBienSo))
                {
                    string urlBienSo = $"rtsp://admin:tlJwpbo6@{ipBienSo}:554/user=admin&password=tlJwpbo6&channel=0&stream=0.sdp";
                    var media = new LibVLCSharp.Shared.Media(_libVlc, urlBienSo, FromType.FromLocation);
                    _mediaPlayerBienSo.Play(media);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể khởi động luồng hệ thống camera bãi xe: " + ex.Message);
            }
        }

        // --- HÀM CHỤP FRAME TRỰC TIẾP TỪ RAM ĐỂ CHẠY ANPR (CHỐNG TRỄ UI) ---
        private async Task KichHoatNhanDienBienSoAsync()
        {
            try
            {
                string tempPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"snap_{Guid.NewGuid().GetHashCode()}.png");

                // Bốc nhanh snapshot từ luồng Camera Biển Số (MediaPlayerBiểnSố)
                if (_mediaPlayerBienSo.TakeSnapshot(0, tempPath, 0, 0))
                {
                    await Task.Run(async () =>
                    {
                        if (File.Exists(tempPath))
                        {
                            byte[] bytes = File.ReadAllBytes(tempPath);
                            using (var ms = new MemoryStream(bytes))
                            using (Bitmap bitmap = new Bitmap(ms))
                            {
                                // Đẩy sang Flask xử lý nhận diện YOLOv11 + PaddleOCR
                                var result = await _anprService.RecognizeAsync(bitmap);
                                if (result != null)
                                {
                                    Dispatcher.Invoke(() => _viewModel.LanVaoBienSo = result.Plate);
                                }
                            }

                            // Xóa file tạm ngay lập tức để bảo vệ ổ cứng SSD
                            try { File.Delete(tempPath); } catch { }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi thực hiện trích xuất ANPR: " + ex.Message);
            }
        }

        private void XuLyQuetThe(string uid, int door = 0)
        {
            Dispatcher.Invoke(async () =>
            {
                if (DataContext is not MainViewModel vm) return;
                var cfg = AppConfig.Load();
                int logicalDoor = 0;

                if (door != 0)
                {
                    if (cfg.ZKTeco.ForceAllIn) logicalDoor = 1;
                    else if (cfg.ZKTeco.ForceAllOut) logicalDoor = 2;
                    else
                    {
                        var inSet = new HashSet<int>(); var outSet = new HashSet<int>();
                        foreach (var part in (cfg.ZKTeco.GateInDoors ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)) if (int.TryParse(part.Trim(), out var v)) inSet.Add(v);
                        foreach (var part in (cfg.ZKTeco.GateOutDoors ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)) if (int.TryParse(part.Trim(), out var v)) outSet.Add(v);
                        if (inSet.Contains(door)) logicalDoor = 1; else if (outSet.Contains(door)) logicalDoor = 2;
                    }
                }

                uid = RFIDService.ChuanHoaUID(uid);
                int cooldown = cfg.ZKTeco.CardCooldownMs > 0 ? cfg.ZKTeco.CardCooldownMs : 2000;
                if (!_lastScanByUid.TryGetValue(uid, out var last)) last = DateTime.MinValue;
                if ((DateTime.Now - last).TotalMilliseconds < cooldown) return;
                _lastScanByUid[uid] = DateTime.Now;

                var db = new DatabaseService();
                var card = db.GetRFIDCardByUid(uid);

                if (card == null || card.Id == 0)
                {
                    string msg = $"❌ Thẻ {uid} chưa đăng ký!";
                    if (logicalDoor == 1) vm.LanVaoTrangThai = msg;
                    else if (logicalDoor == 2) vm.LanRaTrangThai = msg;
                    return;
                }

                vm.BienSoNhap = card.BienSo ?? string.Empty;
                vm.LastScannedUID = uid;
                bool xeTrongBai = db.IsXeTrongBaiByCardId(card.Id);

                if (logicalDoor == 1) // XE VÀO LÀN
                {
                    if (xeTrongBai) { vm.LanVaoTrangThai = $"⚠ {card.BienSo ?? uid} đã trong bãi!"; return; }

                    // 2. KÍCH HOẠT CHỤP ẢNH AI NGAY KHI QUẸT THẺ THÀNH CÔNG
                    await KichHoatNhanDienBienSoAsync();

                    vm.XeVaoCommand.Execute(null);
                }
                else if (logicalDoor == 2) // XE RA LÀN
                {
                    if (!xeTrongBai) { vm.LanRaTrangThai = $"⚠ {card.BienSo ?? uid} không có trong bãi!"; return; }
                    vm.XeRaCommand.Execute(null);
                }
                else
                {
                    if (xeTrongBai) vm.XeRaCommand.Execute(null);
                    else
                    {
                        await KichHoatNhanDienBienSoAsync();
                        vm.XeVaoCommand.Execute(null);
                    }
                }
            });
        }

        private void OnC3200Event(Services.C3200Event evt)
        {
            if (evt == null) return;
            var raw = (evt.RawData ?? "").ToUpper();
            bool isButton = raw.Contains("BUTTON") || evt.EventType == 202;
            if (isButton)
            {
                Task.Run(() => _gateControlService.ProcessGateActionAsync(evt.Door, _currentFrames, "BUTTON_PRESS"));
            }
        }

        private async void OnRfidCardScanned(string uid)
        {
            Dispatcher.Invoke(() => _viewModel.CurrentCardUID = uid);
            // Nếu dùng đầu đọc rfid USB phụ tại bàn, kích hoạt quét luôn:
            await KichHoatNhanDienBienSoAsync();
        }

        private void OnC3200Scanned(string uid, int door) => XuLyQuetThe(uid, door);

        private void MoQuanLyThe(object sender, RoutedEventArgs e)
        {
            RFIDService.Instance.OnCardScanned -= OnRfidCardScanned;
            C3200Service.Instance.OnCardScanned -= OnC3200Scanned;
            new QuanLyThe().ShowDialog();
            RFIDService.Instance.OnCardScanned += OnRfidCardScanned;
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
        }

        private async void OpenGateIn_Click(object sender, RoutedEventArgs e) => await OpenGateAsync(1);
        private async void OpenGateOut_Click(object sender, RoutedEventArgs e) => await OpenGateAsync(2);
        private async Task OpenGateAsync(int doorNumber)
        {
            await _gateControlService.ProcessGateActionAsync(doorNumber, _currentFrames, "MANUAL_OPEN", "Mở từ giao diện");
            if (DataContext is MainViewModel vm)
            {
                string status = $"✅ Đã mở cổng {doorNumber}";
                if (doorNumber == 1) vm.LanVaoTrangThai = status; else vm.LanRaTrangThai = status;
            }
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            // Nút bấm chụp thủ công trên màn hình chính
            await KichHoatNhanDienBienSoAsync();
        }

        private void MoLichSu(object sender, RoutedEventArgs e) => new HistoryWindow().ShowDialog();
        private void MoLichSuGiaHan_Click(object sender, RoutedEventArgs e) => new RFIDGiaHanHistoryWindow().Show();
        private void MoCameraSettings_Click(object sender, RoutedEventArgs e) => new CameraSettingsWindow { Owner = this }.ShowDialog();
        private void MoRealtimeLog_Click(object sender, RoutedEventArgs e) => new RealtimeLogWindow { Owner = this }.Show();
        private void MoDashboard_Click(object sender, RoutedEventArgs e) => new Window { Title = "Dashboard", Content = new DashboardView(), Owner = this, Width = 1200, Height = 850 }.Show();
        private void MoAdvancedSettings_Click(object sender, RoutedEventArgs e) => new Views.AdvancedSettingsWindow { Owner = this }.ShowDialog();
        private void MoC3200Settings_Click(object sender, RoutedEventArgs e) => new C3200SettingsWindow().ShowDialog();
        private void MoButtonLogs_Click(object sender, RoutedEventArgs e) => new ButtonLogsWindow().Show();
    }
}