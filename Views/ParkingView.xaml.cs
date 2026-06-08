using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using System.Text.Json;

namespace QuanLyGiuXe.Views
{
    public partial class ParkingView : UserControl
    {
        private bool _isCamInitialized = false;
        private LibVLC _libVlc;
        private readonly AnprService _anprService = new AnprService(new System.Net.Http.HttpClient());

        public ParkingView()
        {
            InitializeComponent();
            try
            {
                LibVLCSharp.Shared.Core.Initialize();
                System.Diagnostics.Debug.WriteLine("---> [HỆ THỐNG] ĐÃ ÉP NẠP THÀNH CÔNG LIBVLC CORE TỪ CONSTRUCTOR.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi nạp sớm thư viện VLC: " + ex.Message);
            }
            this.Loaded += ParkingView_Loaded;
            this.Unloaded += ParkingView_Unloaded;

            // Đăng ký sự kiện nhận diện AI một lần duy nhất
            _anprService.OnDetectionCompleted += AnprService_OnDetectionCompleted;
        }

        private void ParkingView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isCamInitialized) return;

            try
            {
                if (Window.GetWindow(this)?.DataContext is MainViewModel mainVM)
                {
                    this.DataContext = mainVM;
                }

                // Khởi tạo thực thể VLC từ lõi đã được nạp sớm ở trên
                var globalOptions = new string[]
                {
                "--rtsp-tcp",
                "--network-caching=300",
                "--skip-frames",
                "--no-video-title-show",
                "--quiet"
                };
                _libVlc = new LibVLC(globalOptions);

                // Các chuỗi RTSP ép cứng chuẩn của bạn
                string urlVao1 = "rtsp://admin:tlJwpbo6@192.168.1.100:554/ch1/sub/av_stream";
                string urlVao2 = "rtsp://admin:tlJwpbo6@192.168.1.101:554/ch1/main/av_stream";
                string urlRa1 = "rtsp://admin:tlJwpbo6@192.168.1.102:554/ch1/sub/av_stream";
                string urlRa2 = "rtsp://admin:tlJwpbo6@192.168.1.103:554/ch1/main/av_stream";

                StartCameraStream(CameraVao1, urlVao1);
                StartCameraStream(CameraVao2, urlVao2);
                StartCameraStream(CameraRa1, urlRa1);
                StartCameraStream(CameraRa2, urlRa2);

                _isCamInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi tại Loaded: " + ex.Message);
            }
        }

        /// <summary>
        /// Hàm nạp luồng chuẩn hóa: Gắn trực tiếp vào VideoView, loại bỏ lỗi xung đột luồng
        /// </summary>
        public void StartCameraStream(LibVLCSharp.WPF.VideoView videoView, string rtspUrl)
        {
            if (videoView == null || string.IsNullOrEmpty(rtspUrl) || _libVlc == null) return;

            try
            {
                // Giải phóng triệt để MediaPlayer cũ trước khi gán mới
                if (videoView.MediaPlayer != null)
                {
                    var oldPlayer = videoView.MediaPlayer;
                    videoView.MediaPlayer = null; // Gỡ kết nối giao diện trước
                    oldPlayer.Stop();
                    oldPlayer.Dispose();
                }

                var mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
                videoView.MediaPlayer = mediaPlayer;

                var media = new Media(_libVlc, rtspUrl, FromType.FromLocation);
                media.AddOption("rtsp-tcp");
                media.AddOption("network-caching=300");

                mediaPlayer.Play(media);
                System.Diagnostics.Debug.WriteLine($"15:42:00 ---> [OK] Đã phát luồng camera thực tế thành công cho ô: {videoView.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi phát luồng {videoView.Name}: {ex.Message}");
            }
        }

        private void ParkingView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CameraVao1?.MediaPlayer != null) { CameraVao1.MediaPlayer.Stop(); CameraVao1.MediaPlayer.Dispose(); }
                if (CameraVao2?.MediaPlayer != null) { CameraVao2.MediaPlayer.Stop(); CameraVao2.MediaPlayer.Dispose(); }
                if (CameraRa1?.MediaPlayer != null) { CameraRa1.MediaPlayer.Stop(); CameraRa1.MediaPlayer.Dispose(); }
                if (CameraRa2?.MediaPlayer != null) { CameraRa2.MediaPlayer.Stop(); CameraRa2.MediaPlayer.Dispose(); }
                _libVlc?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi giải phóng tài nguyên khi đóng ParkingView: " + ex.Message);
            }
        }

        private void OpenGateIn_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main) main.OpenGateIn_Click(sender, e);
        }

        private void OpenGateOut_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main) main.OpenGateOut_Click(sender, e);
        }

        private async void BtnMockCardVao_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = this.DataContext as MainViewModel ?? Window.GetWindow(this)?.DataContext as MainViewModel;
            if (viewModel != null)
            {
                await TriggerAnprProcessAsync(CameraVao2?.MediaPlayer, "IN", viewModel);
            }
        }

        private async void BtnMockCardRa_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("======> [MOCK CARD] Kích hoạt quẹt thẻ Làn Ra.");
            var viewModel = this.DataContext as MainViewModel ?? Window.GetWindow(this)?.DataContext as MainViewModel;
            if (viewModel != null)
            {
                await TriggerAnprProcessAsync(CameraRa2?.MediaPlayer, "OUT", viewModel);
            }
        }

        private async Task TriggerAnprProcessAsync(LibVLCSharp.Shared.MediaPlayer mediaPlayer, string laneType, MainViewModel viewModel)
        {
            try
            {
                if (mediaPlayer == null || !mediaPlayer.IsPlaying)
                {
                    System.Diagnostics.Debug.WriteLine($"[LỖI] Camera Làn {laneType} chưa chạy.");
                    return;
                }

                string tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestSnaps");
                if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                string fileName = laneType == "IN" ? "snapshot_test_vao.jpg" : "snapshot_test_ra.jpg";
                string tempFile = Path.Combine(tempFolder, fileName);

                bool isCaptured = await Task.Run(() =>
                {
                    try { return mediaPlayer.TakeSnapshot(0, tempFile, 0, 0); }
                    catch { return false; }
                });

                if (isCaptured && File.Exists(tempFile))
                {
                    System.Diagnostics.Debug.WriteLine($"[ANPR] Đã chụp ảnh thành công file: {tempFile}");
                    BitmapSource anhSnap = _anprService.LoadImageNoLock(tempFile);

                    if (anhSnap != null && viewModel != null)
                    {
                        App.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (laneType == "IN")
                            {
                                viewModel.AnhChupVao2 = anhSnap;
                                if (AnhSnapVao2 != null) AnhSnapVao2.Source = anhSnap;
                            }
                            else
                            {
                                viewModel.AnhChupRa2 = anhSnap;
                            }
                        }));
                    }

                    _ = Task.Run(async () => {
                        await _anprService.ProcessAutoDetectionAsync(tempFile);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi xử lý hệ thống Làn {laneType}: " + ex.Message);
            }
        }

        private void AnprService_OnDetectionCompleted(AnprResult result)
        {
            if (result == null) return;

            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var mainWindow = Window.GetWindow(this);
                dynamic windowDataContext = mainWindow?.DataContext;

                if (result.SavedPath.Contains("snapshot_test_vao") || result.SavedPath.ToLower().Contains("vao"))
                {
                    ImgRoiLane1.Source = result.RoiImage;

                    if (windowDataContext != null)
                    {
                        windowDataContext.Lane1BienSo = result.Plate;
                        windowDataContext.Lane1TrangThai = "Nhận diện thành công!";
                    }
                }
                else
                {
                    ImgRoiLane2.Source = result.RoiImage;

                    if (windowDataContext != null)
                    {
                        windowDataContext.Lane2BienSo = result.Plate;
                        windowDataContext.Lane2TrangThai = "Nhận diện thành công!";
                    }
                }

                _ = Task.Run(() => _anprService.SaveFinalImage(result.RoiImage, result.Plate));
            }));
        }

        public MediaPlayer GetMediaPlayer(string key)
        {
            return key switch
            {
                "Vao1" => CameraVao1?.MediaPlayer,
                "Vao2" => CameraVao2?.MediaPlayer,
                "Ra1" => CameraRa1?.MediaPlayer,
                "Ra2" => CameraRa2?.MediaPlayer,
                _ => null
            };
        }
    }
}