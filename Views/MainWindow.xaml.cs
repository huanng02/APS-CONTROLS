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
using AForge.Video;
using AForge.Video.DirectShow;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe
{
    public partial class MainWindow : Window
    {
        private Bitmap? frameVao1, frameVao2, frameRa1, frameRa2;
        private FilterInfoCollection? cameras;
        private VideoCaptureDevice? camVao1, camVao2, camRa1, camRa2;
        private readonly object _manualOpenLock = new();
        private readonly System.Collections.Generic.Dictionary<int, DateTime> _lastManualOpen = new();
        private readonly Dictionary<string, DateTime> _lastScanByUid = new();

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
            // debounce: ignore RTLog events that arrive immediately after a manual open command
            try
            {
                // If any manual open happened very recently, skip RTLog processing to avoid duplicate/misaligned records.
                bool recentManual = false;
                lock (_manualOpenLock)
                {
                    foreach (var kv in _lastManualOpen)
                    {
                        if ((DateTime.UtcNow - kv.Value).TotalSeconds < 5)
                        {
                            recentManual = true; break;
                        }
                    }
                }
                if (recentManual)
                {
                    try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"), $"{DateTime.Now:O}\tSKIP_RTLOG\tdoor={evt.Door}\trecent_manual_any\n"); } catch { }
                    return;
                }
            }
            catch { }
            // detect physical button press: either raw contains BUTTON_PRESS or event type 202 (device-specific)
            if (evt == null) return;
            var raw = (evt.RawData ?? string.Empty).ToUpperInvariant();
            bool isButton = raw.Contains("BUTTON") || raw.Contains("BUTTON_PRESS") || evt.EventType == 202;
            if (!isButton) return;

            // capture current frames for the door and save images + try open barrier and insert DB record
            Task.Run(async () =>
            {
                try
                {
                    Bitmap? plate = null;
                    Bitmap? full = null;
                    // capture clones on UI thread
                    Dispatcher.Invoke(() =>
                    {
                        if (evt.Door == 1)
                        {
                            if (frameVao2 != null) plate = (Bitmap)frameVao2.Clone();
                            if (frameVao1 != null) full = (Bitmap)frameVao1.Clone();
                        }
                        else
                        {
                            if (frameRa2 != null) plate = (Bitmap)frameRa2.Clone();
                            if (frameRa1 != null) full = (Bitmap)frameRa1.Clone();
                        }
                    });

                    string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressImages");
                    Directory.CreateDirectory(imagesDir);
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    string platePath = null;
                    string fullPath = null;

                    if (full != null)
                    {
                        fullPath = Path.Combine(imagesDir, $"{stamp}_door{evt.Door}_full.jpg");
                        full.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                        full.Dispose();
                    }
                    if (plate != null)
                    {
                        platePath = Path.Combine(imagesDir, $"{stamp}_door{evt.Door}_plate.jpg");
                        plate.Save(platePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                        plate.Dispose();
                    }

                    // determine logical mapping for this button press (IN/OUT) using current config
                    var cfg = AppConfig.Load();
                    int logicalDoor = cfg.ZKTeco.MapPhysicalToLogical(evt.Door);
                    evt.InOutState = logicalDoor;

                    // try open barrier and record result
                    byte? barrierResult = null;
                    try
                    {
                        // determine button action from config (Button1 maps to evt.Door==1, Button2 to evt.Door==2)
                        bool opened = false;
                        string action = evt.Door == 1 ? cfg.ZKTeco.Button1Action : cfg.ZKTeco.Button2Action;
                        switch (action)
                        {
                            case "OpenThisDoor":
                                opened = await C3200Service.Instance.OpenBarrierAsync(evt.Door);
                                break;
                            case "OpenGroupIn":
                                var inSet = cfg.ZKTeco.GetInSet();
                                foreach (var d in inSet) await C3200Service.Instance.OpenBarrierAsync(d);
                                opened = true;
                                break;
                            case "OpenGroupOut":
                                var outSet = cfg.ZKTeco.GetOutSet();
                                foreach (var d in outSet) await C3200Service.Instance.OpenBarrierAsync(d);
                                opened = true;
                                break;
                            default:
                                opened = false; // Disabled or unknown
                                break;
                        }
                        barrierResult = opened ? (byte)1 : (byte)0;
                    }
                    catch { barrierResult = null; }

                    // If no images captured initially, retry a few times (small delay) to allow cameras to update.
                    string notes = null;
                    if (string.IsNullOrEmpty(platePath) && string.IsNullOrEmpty(fullPath))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            await Task.Delay(200);
                            Bitmap? plate2 = null;
                            Bitmap? full2 = null;
                            Dispatcher.Invoke(() =>
                            {
                                if (evt.Door == 1)
                                {
                                    if (frameVao2 != null) plate2 = (Bitmap)frameVao2.Clone();
                                    if (frameVao1 != null) full2 = (Bitmap)frameVao1.Clone();
                                }
                                else if (evt.Door == 2)
                                {
                                    if (frameRa2 != null) plate2 = (Bitmap)frameRa2.Clone();
                                    if (frameRa1 != null) full2 = (Bitmap)frameRa1.Clone();
                                }
                            });

                            if (full2 != null)
                            {
                                fullPath = Path.Combine(imagesDir, $"{stamp}_door{evt.Door}_full_retry{i}.jpg");
                                full2.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                full2.Dispose();
                            }
                            if (plate2 != null)
                            {
                                platePath = Path.Combine(imagesDir, $"{stamp}_door{evt.Door}_plate_retry{i}.jpg");
                                plate2.Save(platePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                                plate2.Dispose();
                            }

                            if (!string.IsNullOrEmpty(platePath) || !string.IsNullOrEmpty(fullPath)) break;
                        }

                        if (string.IsNullOrEmpty(platePath) && string.IsNullOrEmpty(fullPath))
                        {
                            // no images after retries -> mark note; do not assume barrier failed just because images missing
                            notes = "no_images";
                            // leave barrierResult as null so we don't record '0' (interpreted as explicit failure)
                            barrierResult = null;
                        }
                    }

                    // insert to DB (with debug logging)
                    try
                    {
                        var db = new DatabaseService();
                        DateTime ts = DateTime.Now;
                        if (!string.IsNullOrEmpty(evt.Time) && DateTime.TryParse(evt.Time, out var parsed)) ts = parsed;
                        db.InsertButtonPressLog(ts, (byte?)evt.Door, evt.EventType, evt.InOutState,
                            evt.CardNo, evt.Pin, evt.RawData, "BUTTON_PRESS",
                            barrierResult, platePath, fullPath, null, null, notes);

                        // try reconcile any recent manual-open records for same door
                        try
                        {
                            if (barrierResult.HasValue)
                            {
                                db.ReconcileManualOpen(ts, (byte)evt.Door, barrierResult.Value, 5, $" (reconciled by RT opened={barrierResult})");
                            }
                        }
                        catch { }

                        // debug trace
                        try
                        {
                            string logLine = $"{DateTime.Now:O}\tRT\tdoor={evt.Door}\tevt={evt.EventType}\topened={barrierResult}\tplateExists={(File.Exists(platePath) ? 1 : 0)}\tfullExists={(File.Exists(fullPath) ? 1 : 0)}\tplate={platePath}\tfull={fullPath}\traw={evt.RawData}\tnotes={notes}\n";
                            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"), logLine);
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"), $"{DateTime.Now:O}\tRT\tINsertError\t{ex.Message}\n"); } catch { }
                    }
                }
                catch { }
            });
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
                int mappedIn = cfg.ZKTeco.GateInDoor;
                int mappedOut = cfg.ZKTeco.GateOutDoor;

                // map physical door (from device) to logical door: 1 = IN, 2 = OUT, 0 = unknown/usb
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
                        // multi-door mapping from CSV
                        var inSet = new HashSet<int>();
                        var outSet = new HashSet<int>();
                        foreach (var part in (cfg.ZKTeco.GateInDoors ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                            if (int.TryParse(part.Trim(), out var v)) inSet.Add(v);
                        foreach (var part in (cfg.ZKTeco.GateOutDoors ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                            if (int.TryParse(part.Trim(), out var v)) outSet.Add(v);

                        if (inSet.Contains(door)) logicalDoor = 1;
                        else if (outSet.Contains(door)) logicalDoor = 2;
                        else logicalDoor = 0;
                    }
                }

                // debug trace: incoming scan + mapping
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"),
                        $"{DateTime.Now:O}\tSCAN_IN\trawDoor={door}\tmappedIn={cfg.ZKTeco.GateInDoors}\tmappedOut={cfg.ZKTeco.GateOutDoors}\n");
                }
                catch { }

                // normalize UID and throttle repeated scans per-UID using configured cooldown
                try
                {
                    uid = RFIDService.ChuanHoaUID(uid);

                    int cooldown = cfg.ZKTeco.CardCooldownMs > 0 ? cfg.ZKTeco.CardCooldownMs : 2000;

                    if (!_lastScanByUid.TryGetValue(uid, out var last)) last = DateTime.MinValue;
                    var elapsed = (DateTime.Now - last).TotalMilliseconds;
                    if (elapsed < cooldown)
                    {
                        // still cooling down: show a brief message depending on direction
                        double leftMs = Math.Ceiling(cooldown - elapsed);
                        string human = leftMs >= 1000 ? $"{(leftMs/1000.0):0.0}s" : $"{leftMs}ms";
                        string msg = $"⏳ Vui lòng đợi {human} trước khi quét lại";
                        if (logicalDoor == 1) vm.LanVaoTrangThai = msg;
                        else if (logicalDoor == 2) vm.LanRaTrangThai = msg;
                        else
                        {
                            // generic brief notification (non-blocking toast)
                            ShowToast(msg, 1500);
                        }

                        return;
                    }

                    _lastScanByUid[uid] = DateTime.Now;
                }
                catch { }

                var db = new DatabaseService();
                if (!db.CheckCardExists(uid))
                {
                    string msg = $"❌ Thẻ {uid} chưa đăng ký!";
                    if (logicalDoor == 1) vm.LanVaoTrangThai = msg;
                    else if (logicalDoor == 2) vm.LanRaTrangThai = msg;
                    else MessageBox.Show(msg, "Lỗi thẻ");
                    return;
                }

                string bienSo = db.GetBienSoFromUID(uid);
                if (string.IsNullOrEmpty(bienSo))
                {
                    string msg = $"❌ Thẻ {uid} chưa gán biển số!";
                    if (logicalDoor == 1) vm.LanVaoTrangThai = msg;
                    else if (logicalDoor == 2) vm.LanRaTrangThai = msg;
                    else MessageBox.Show(msg, "Lỗi thẻ");
                    return;
                }

                vm.BienSoNhap = bienSo;
                vm.LastScannedUID = uid;

                bool xeTrongBai = vm.DanhSachXe.Any(x => x.BienSo == bienSo);

                // ── C3200 Reader 1 = CỔNG VÀO (chỉ cho xe vào) ──
                if (logicalDoor == 1)
                {
                    if (xeTrongBai)
                    {
                        vm.LanVaoTrangThai = $"⚠ {bienSo} đã trong bãi!";
                        return;
                    }
                    vm.XeVaoCommand.Execute(null);
                    return;
                }

                // ── C3200 Reader 2 = CỔNG RA (chỉ cho xe ra) ──
                if (logicalDoor == 2)
                {
                    if (!xeTrongBai)
                    {
                        vm.LanRaTrangThai = $"⚠ {bienSo} không có trong bãi!";
                        return;
                    }
                    vm.XeRaCommand.Execute(null);
                    return;
                }

                // ── RFID USB (door == 0) = tự động phân luồng ──
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
            string tenCong = doorNumber == 1 ? "Cổng Vào" : "Cổng Ra";
            // debug: record requested manual open
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"),
                    $"{DateTime.Now:O}\tMANUAL_REQUEST\tdoor={doorNumber}\n");
            }
            catch { }

            // record manual open time so RTLog processing can ignore the corresponding RT log
            try
            {
                lock (_manualOpenLock)
                {
                    _lastManualOpen[doorNumber] = DateTime.UtcNow;
                }
            }
            catch { }

            bool opened = await C3200Service.Instance.OpenBarrierAsync(doorNumber);

            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"),
                    $"{DateTime.Now:O}\tMANUAL_RESULT\tdoor={doorNumber}\topened={opened}\n");
            }
            catch { }

            if (!opened)
            {
                MessageBox.Show($"❌ Không mở được {tenCong}\n\n{C3200Service.Instance.LastError}",
                    "C3-200 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // update UI status briefly to reflect manual open
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (DataContext is MainViewModel vm)
                    {
                        if (doorNumber == 1) vm.LanVaoTrangThai = opened ? $"✅ {tenCong} đã mở (thủ công)" : $"❌ Mở {tenCong} thất bại";
                        else vm.LanRaTrangThai = opened ? $"✅ {tenCong} đã mở (thủ công)" : $"❌ Mở {tenCong} thất bại";
                    }
                });
            }
            catch { }

            // Capture current frames and save a manual-open log entry
            try
            {
                Bitmap? plate = null;
                Bitmap? full = null;
                Dispatcher.Invoke(() =>
                {
                    if (doorNumber == 1)
                    {
                        if (frameVao2 != null) plate = (Bitmap)frameVao2.Clone();
                        if (frameVao1 != null) full = (Bitmap)frameVao1.Clone();
                    }
                    else if (doorNumber == 2)
                    {
                        if (frameRa2 != null) plate = (Bitmap)frameRa2.Clone();
                        if (frameRa1 != null) full = (Bitmap)frameRa1.Clone();
                    }
                });

                string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressImages");
                Directory.CreateDirectory(imagesDir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string platePath = null; string fullPath = null;
                if (full != null)
                {
                    fullPath = Path.Combine(imagesDir, $"{stamp}_manual_door{doorNumber}_full.jpg");
                    full.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    full.Dispose();
                }
                if (plate != null)
                {
                    platePath = Path.Combine(imagesDir, $"{stamp}_manual_door{doorNumber}_plate.jpg");
                    plate.Save(platePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    plate.Dispose();
                }

                try
                {
                    // record manual open timestamp for debounce
                    lock (_manualOpenLock)
                    {
                        _lastManualOpen[doorNumber] = DateTime.UtcNow;
                    }

                    var db = new DatabaseService();
                    // record diagnostic info with manual insert; do not force 0 if uncertain
                    string manualNotes = $"Manual open via UI; sdkLastError={C3200Service.Instance.LastError}; diag={C3200Service.Instance.GetDiagnosticText()}";
                    db.InsertButtonPressLog(DateTime.Now, (byte?)doorNumber, null, null,
                        "MANUAL", null, null, "MANUAL_OPEN",
                        opened ? (byte)1 : (byte?)null, platePath, fullPath, Environment.UserName, null, manualNotes);
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ButtonPressDebug.txt"), $"{DateTime.Now:O}\tMANUAL_INSERT_ERROR\t{ex.Message}\n"); } catch { }
                }
            }
            catch { }
        }

        // ── Camera (4 cam: 2 per gate) ───────────────────────────────────────

        private void MoCameras()
        {
            cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (cameras.Count == 0) return;

            var cfg = AppConfig.Load().Cameras;

            StartCam(ref camVao1, cfg.VaoToanCanh, 0, CamVao1_NewFrame);
            StartCam(ref camVao2, cfg.VaoBienSo, 1, CamVao2_NewFrame);
            StartCam(ref camRa1, cfg.RaToanCanh, 2, CamRa1_NewFrame);
            StartCam(ref camRa2, cfg.RaBienSo, 3, CamRa2_NewFrame);
        }

        private void StartCam(ref VideoCaptureDevice? cam, string cfgName, int fallbackIdx,
            NewFrameEventHandler handler)
        {
            cam = FindCamera(cfgName);
            if (cam == null && cameras != null && fallbackIdx < cameras.Count)
                cam = new VideoCaptureDevice(cameras[fallbackIdx].MonikerString);
            if (cam == null) return;
            cam.NewFrame += handler;
            cam.Start();
        }

        private VideoCaptureDevice? FindCamera(string name)
        {
            if (string.IsNullOrEmpty(name) || cameras == null) return null;
            foreach (FilterInfo c in cameras)
                if (c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return new VideoCaptureDevice(c.MonikerString);
            return null;
        }

        private void CamVao1_NewFrame(object sender, NewFrameEventArgs e)
        {
            frameVao1 = (Bitmap)e.Frame.Clone();
            Dispatcher.Invoke(() => CameraVao1.Source = ConvertBitmap(frameVao1));
        }

        private void CamVao2_NewFrame(object sender, NewFrameEventArgs e)
        {
            frameVao2 = (Bitmap)e.Frame.Clone();
            Dispatcher.Invoke(() => CameraVao2.Source = ConvertBitmap(frameVao2));
        }

        private void CamRa1_NewFrame(object sender, NewFrameEventArgs e)
        {
            frameRa1 = (Bitmap)e.Frame.Clone();
            Dispatcher.Invoke(() => CameraRa1.Source = ConvertBitmap(frameRa1));
        }

        private void CamRa2_NewFrame(object sender, NewFrameEventArgs e)
        {
            frameRa2 = (Bitmap)e.Frame.Clone();
            Dispatcher.Invoke(() => CameraRa2.Source = ConvertBitmap(frameRa2));
        }

        private static BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            ms.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = ms;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            return image;
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (frameVao1 == null) return;

            string plate = await ApiService.SendImageAsync(frameVao1);
            if (DataContext is MainViewModel vm)
            {
                vm.BienSoNhap = plate.Trim();
                vm.XeVaoCommand.Execute(null);
            }
        }

        // ── Khác ─────────────────────────────────────────────────────────────────

        private void MoLichSu(object sender, RoutedEventArgs e) =>
            new HistoryWindow().ShowDialog();

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
