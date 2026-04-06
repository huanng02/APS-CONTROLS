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
        private Bitmap? currentFrame;
        private FilterInfoCollection? cameras;
        private VideoCaptureDevice? cam;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            MoCamera();

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
                    MessageBox.Show($"❌ Thẻ {uid} chưa đăng ký!", "Lỗi thẻ");
                    return;
                }

                string bienSo = db.GetBienSoFromUID(uid);
                if (string.IsNullOrEmpty(bienSo))
                {
                    MessageBox.Show($"❌ Thẻ {uid} chưa gán biển số!", "Lỗi thẻ");
                    return;
                }

                vm.BienSoNhap = bienSo;
                vm.LastScannedUID = uid;

                bool xeTrongBai = vm.DanhSachXe.Any(x => x.BienSo == bienSo);

                if (door == 1 && !xeTrongBai)
                    vm.XeVaoCommand.Execute(null);
                else if (door == 2 && xeTrongBai)
                    vm.XeRaCommand.Execute(null);
                else if (xeTrongBai)
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

        // ── Camera ───────────────────────────────────────────────────────────────

        private void MoCamera()
        {
            cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (cameras.Count == 0) return;

            foreach (FilterInfo camera in cameras)
            {
                if (camera.Name.Contains("ivcam", StringComparison.OrdinalIgnoreCase))
                {
                    cam = new VideoCaptureDevice(camera.MonikerString);
                    break;
                }
            }

            cam ??= new VideoCaptureDevice(cameras[0].MonikerString);
            cam.NewFrame += Cam_NewFrame;
            cam.Start();
        }

        private void Cam_NewFrame(object sender, NewFrameEventArgs e)
        {
            currentFrame = (Bitmap)e.Frame.Clone();
            Dispatcher.Invoke(() => CameraView.Source = ConvertBitmap(currentFrame));
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
            if (currentFrame == null) return;

            string plate = await ApiService.SendImageAsync(currentFrame);
            if (DataContext is MainViewModel vm)
            {
                vm.BienSoNhap = plate.Trim();
                vm.XeVaoCommand.Execute(null);
            }
        }

        // ── Khác ─────────────────────────────────────────────────────────────────

        private void MoLichSu(object sender, RoutedEventArgs e) =>
            new HistoryWindow().ShowDialog();

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGrid dg && dg.SelectedItem is Xe xe)
                new VehicleDetailWindow(xe).ShowDialog();
        }
    }
}
