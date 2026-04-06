using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using OpenCvSharp;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using QuanLyGiuXe.Views;
namespace QuanLyGiuXe
{
    public partial class MainWindow : System.Windows.Window
    {
        int frameCount = 0;
        Bitmap currentFrame;
        bool isProcessing = false;
        SerialPort port;
        string lastUID = "";
        DateTime lastScan = DateTime.MinValue;
        FilterInfoCollection cameras;
        VideoCaptureDevice cam;


        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            DispatcherTimer timer = new DispatcherTimer();
            CameraService.Instance.Start();
            timer.Interval = TimeSpan.FromMilliseconds(800);
            timer.Tick += Timer_Tick;
            timer.Start();
            var rfid = RFIDService.Instance;
            rfid.OnCardScanned += RFID_OnCardScanned;
            rfid.Start();
            var cam = CameraService.Instance;
            var vm = DataContext as MainViewModel;

            CameraService.Instance.OnFrameUpdated += (frame) =>
            {
                Dispatcher.Invoke(() =>
                {
                    vm.CameraImage = ConvertBitmap(frame);
                });
            };
            int frameSkip = 0;

            cam.OnFrameUpdated += (frame) =>
            {
                frameSkip++;

                if (frameSkip % 2 != 0) return; // giảm 50% load

                Dispatcher.Invoke(() =>
                {
                    CameraView.Source = ConvertBitmap(frame);
                });
            };

            cam.Start();
        }
        private DateTime _lastCapture = DateTime.MinValue;

        private void RFID_OnCardScanned(string uid)
        {
            var frame = CameraService.Instance.GetCurrentFrame();
            MessageBox.Show(frame == null ? "NULL FRAME" : "FRAME OK");
            Dispatcher.Invoke(() =>
            {
                if (DataContext is MainViewModel vm)
                {
                    string bienSo = vm.BienSoNhap;

                    if (string.IsNullOrEmpty(bienSo))
                    {
                        MessageBox.Show("Chưa có biển số!");
                        return;
                    }

                    string bienSoDB = LayBienSoTuUID(uid);

                    if (bienSo != bienSoDB)
                    {
                        MessageBox.Show("⚠ Biển số không trùng khớp");
                        return;
                    }

                    // chống spam quét thẻ
                    if ((DateTime.Now - _lastCapture).TotalSeconds < 2)
                        return;

                    _lastCapture = DateTime.Now;

                    string imagePath;

                    if (vm.DanhSachXe.Any(x => x.BienSo == bienSo))
                    {
                        //Xe ra
                        imagePath = CameraService.Instance.CaptureAndSave(uid, "ra");

                        vm.XeRaCommand.Execute(null);
                        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                        {
                            vm.CapturedImage = new BitmapImage(new Uri(imagePath));
                        }
                        else
                        {
                            MessageBox.Show("Không chụp được ảnh!");
                        }
                    }
                    else
                    {
                        //Xe vào
                        imagePath = CameraService.Instance.CaptureAndSave(uid, "vao");

                        vm.XeVaoCommand.Execute(null);
                        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                        {
                            vm.CapturedImage = new BitmapImage(new Uri(imagePath));
                        }
                        else
                        {
                            MessageBox.Show("Không chụp được ảnh!");
                        }
                    }

                    MessageBox.Show($"Đã lưu ảnh:\n{imagePath}");
                }
            });
        }

        private void MoLichSu(object sender, RoutedEventArgs e)
        {
            HistoryWindow w = new HistoryWindow();
            w.ShowDialog();
        }

        private BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                ms.Position = 0;

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();

                return image;
            }
        }

        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            var frame = CameraService.Instance.GetCurrentFrame();

            if (frame == null)
            {
                MessageBox.Show("Không có frame!");
                return;
            }
            string plate = await ApiService.SendImageAsync(currentFrame);

            MessageBox.Show("Biển số: " + plate);

            if (DataContext is MainViewModel vm)
            {
                vm.BienSoNhap = plate.Trim();

                vm.XeVaoCommand.Execute(null);
            }
        }
        private void MoQuanLyThe(object sender, RoutedEventArgs e)
        {
            var rfid = RFIDService.Instance;

            // Tắt xử lý ở MainWindow
            rfid.OnCardScanned -= RFID_OnCardScanned;

            QuanLyThe window = new QuanLyThe();
            window.ShowDialog();

            // Bật lại
            rfid.OnCardScanned += RFID_OnCardScanned;
        }
        string LayBienSoTuUID(string uid)
        {
            var db = new DatabaseService();
            return db.GetBienSoFromUID(uid);
        }

        private bool isSending = false;

        private async void Timer_Tick(object sender, EventArgs e)
        {
            if (isSending) return;

            var frame = CameraService.Instance.GetCurrentFrame();

            if (frame == null) return;

            isSending = true;

            try
            {
                string plate = await ApiService.SendImageAsync(frame);

                if (!string.IsNullOrEmpty(plate))
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (DataContext is MainViewModel vm)
                        {
                            vm.BienSoNhap = plate;
                        }
                    });
                }
            }
            finally
            {
                isSending = false;
            }
        }
    }
}
