using System.Drawing;
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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            MoCameras();

            RFIDService.Instance.OnCardScanned += OnRfidScanned;
            RFIDService.Instance.Start();
            C3200Service.Instance.OnCardScanned += OnC3200Scanned;
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

        private async Task OpenGateAsync(int doorNumber)
        {
            string tenCong = doorNumber == 1 ? "Cổng Vào" : "Cổng Ra";
            bool opened = await C3200Service.Instance.OpenBarrierAsync(doorNumber);

            if (!opened)
                MessageBox.Show($"❌ Không mở được {tenCong}\n\n{C3200Service.Instance.LastError}",
                    "C3-200 Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
