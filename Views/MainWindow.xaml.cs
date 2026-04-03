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
            MoCamera();
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(800);
            timer.Tick += Timer_Tick;
            timer.Start();
            var rfid = RFIDService.Instance;
            rfid.OnCardScanned += RFID_OnCardScanned;
            rfid.Start();
        }
        private void RFID_OnCardScanned(string uid)
        {
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

                    if (vm.DanhSachXe.Any(x => x.BienSo == bienSo))
                        vm.XeRaCommand.Execute(null);
                    else
                        vm.XeVaoCommand.Execute(null);
                }
            });
        }

        private void MoLichSu(object sender, RoutedEventArgs e)
        {
            HistoryWindow w = new HistoryWindow();
            w.ShowDialog();
        }

        void MoCamera()
        {
            cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (cameras.Count == 0)
            {
                MessageBox.Show("Không tìm thấy camera");
                return;
            }

            // tìm iVCam
            foreach (FilterInfo camera in cameras)
            {
                if (camera.Name.ToLower().Contains("ivcam"))
                {
                    cam = new VideoCaptureDevice(camera.MonikerString);
                    break;
                }
            }

            // nếu không có iVCam thì lấy camera đầu
            if (cam == null)
            {
                cam = new VideoCaptureDevice(cameras[0].MonikerString);
            }

            cam.NewFrame += Cam_NewFrame;
            cam.Start();
        }

        void Cam_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            currentFrame = (Bitmap)eventArgs.Frame.Clone();

            Dispatcher.Invoke(() =>
            {
                CameraView.Source = ConvertBitmap(currentFrame);
            });
        }

        BitmapImage ConvertBitmap(Bitmap bitmap)
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
            if (currentFrame == null || isSending) return;

            isSending = true;

            try
            {
                string plate = await ApiService.SendImageAsync(currentFrame);

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
