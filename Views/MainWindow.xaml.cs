using System.Drawing;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

                    // try open barrier and record result
                    byte? barrierResult = null;
                    try
                    {
                        bool opened = await C3200Service.Instance.OpenBarrierAsync(evt.Door);
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

                var db = new DatabaseService();
                if (!db.CheckCardExists(uid))
                {
                    string msg = $"❌ Thẻ {uid} chưa đăng ký!";
                    if (door == 1) vm.LanVaoTrangThai = msg;
                    else if (door == 2) vm.LanRaTrangThai = msg;
                    else MessageBox.Show(msg, "Lỗi thẻ");
                    return;
                }

                string bienSo = db.GetBienSoFromUID(uid);
                if (string.IsNullOrEmpty(bienSo))
                {
                    string msg = $"❌ Thẻ {uid} chưa gán biển số!";
                    if (door == 1) vm.LanVaoTrangThai = msg;
                    else if (door == 2) vm.LanRaTrangThai = msg;
                    else MessageBox.Show(msg, "Lỗi thẻ");
                    return;
                }

                vm.BienSoNhap = bienSo;
                vm.LastScannedUID = uid;

                bool xeTrongBai = vm.DanhSachXe.Any(x => x.BienSo == bienSo);

                // ── C3200 Reader 1 = CỔNG VÀO (chỉ cho xe vào) ──
                if (door == 1)
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
                if (door == 2)
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
    }
}
