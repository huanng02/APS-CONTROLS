using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LibVLCSharp.Shared;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    public partial class CameraSettingsWindow : Window
    {
        private TextBox _currentActiveTextBox = null;

        public CameraSettingsWindow()
        {
            InitializeComponent();
            LoadCameras();

            // Đăng ký sự kiện focus để nhận diện người dùng đang muốn điền vào vị trí nào
            txtVaoBienSoIP.GotFocus += TextBox_GotFocus;
            txtRaBienSoIP.GotFocus += TextBox_GotFocus;
            txtVaoToanCanhIP.GotFocus += TextBox_GotFocus;
            txtRaToanCanhIP.GotFocus += TextBox_GotFocus;
        }

        /// <summary>
        /// BÓC TÁCH IP TỪ CHUỖI RTSP ĐỂ HIỂN THỊ LÊN GIAO DIỆN SẠCH ĐẸP
        /// </summary>
        private void LoadCameras()
        {
            try
            {
                var config = AppConfig.Load();
                if (config?.Cameras == null) return;

                var cam = config.Cameras;

                // Sử dụng hàm Helper để bóc riêng IP từ chuỗi RTSP phức tạp ra hiển thị
                txtVaoToanCanhIP.Text = ExtractIpFromRtsp(cam.VaoToanCanh);
                txtVaoBienSoIP.Text = ExtractIpFromRtsp(cam.VaoBienSo);
                txtRaToanCanhIP.Text = ExtractIpFromRtsp(cam.RaToanCanh);
                txtRaBienSoIP.Text = ExtractIpFromRtsp(cam.RaBienSo);

                if (txtCameraCount != null)
                    txtCameraCount.Text = "📡 Hệ thống đang sử dụng kết nối Camera IP qua mạng LAN.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi nạp thông tin camera lên UI: " + ex.Message);
            }
        }

        /// <summary>
        /// SỰ KIỆN LƯU CẤU HÌNH: TỰ ĐỘNG BIẾN ĐỔI IP THÀNH CHUỖI RTSP CHUẨN TRƯỚC KHI GHI FILE
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = AppConfig.Load() ?? new AppConfig();
                if (config.Cameras == null) config.Cameras = new CameraConfig();

                string user = txtCamUser.Text.Trim();
                string pass = txtCamPass.Password.Trim();

                // Lấy IP từ TextBox giao diện
                string ipVaoToanCanh = txtVaoToanCanhIP.Text.Trim();
                string ipVaoBienSo = txtVaoBienSoIP.Text.Trim();
                string ipRaToanCanh = txtRaToanCanhIP.Text.Trim();
                string ipRaBienSo = txtRaBienSoIP.Text.Trim();

                // Dựng chuỗi RTSP Động - Phân chia Luồng Chính (stream=0) / Luồng Phụ (stream=1) tự động
                config.Cameras.VaoToanCanh = BuildRtspUrl(ipVaoToanCanh, user, pass, streamId: 1); // Luồng phụ (Mượt, giảm tải)
                config.Cameras.VaoBienSo = BuildRtspUrl(ipVaoBienSo, user, pass, streamId: 0);   // Luồng chính (Nét để quét AI)
                config.Cameras.RaToanCanh = BuildRtspUrl(ipRaToanCanh, user, pass, streamId: 1);  // Luồng phụ (Mượt, giảm tải)
                config.Cameras.RaBienSo = BuildRtspUrl(ipRaBienSo, user, pass, streamId: 0);    // Luồng chính (Nét để quét AI)

                // Ghi đè file config.json thông qua hàm Save có sẵn của bạn
                config.Save();

                MessageBox.Show("💾 Đã tự động cấu hình phân luồng và lưu Camera thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Có lỗi khi lưu cấu hình: {ex.Message}", "Lỗi");
            }
        }

        // Đồng bộ hành vi cho cả 2 nút Save trùng lặp trên giao diện của bạn
        private void BtnSave_Click(object sender, RoutedEventArgs e) => Save_Click(sender, e);

        /// <summary>
        /// QUÉT IP QUA ONVIF VÀ TỰ ĐỘNG GÁN IP VÀO Ô ĐANG CHỌN
        /// </summary>
        private async void BtnScanIP_Click(object sender, RoutedEventArgs e)
        {
            btnScanIP.IsEnabled = false;
            btnScanIP.Content = "⏳ Đang quét Camera (ONVIF)...";

            var discoveryService = new NetworkDiscoveryService();
            var cameras = await discoveryService.ScanNetworkAsync();

            lstDetectedCameras.ItemsSource = cameras;
            btnScanIP.IsEnabled = true;
            btnScanIP.Content = "🔍 Quét nhanh IP";

            if (cameras == null || cameras.Count == 0)
            {
                MessageBox.Show("Không tìm thấy thiết bị Camera ONVIF nào trong mạng.", "Thông báo");
            }
        }

        private void LstDetectedCameras_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstDetectedCameras.SelectedItem is CameraDetected selectedCam)
            {
                // Chỉ lấy phần IP thuần của camera được quét dán vào TextBox hiển thị
                string ipTarget = !string.IsNullOrEmpty(selectedCam.IP) ? selectedCam.IP : ExtractIpFromRtsp(selectedCam.RtspUrl);

                if (_currentActiveTextBox != null)
                {
                    _currentActiveTextBox.Text = ipTarget;
                    _currentActiveTextBox.Focus();
                }
                else
                {
                    txtVaoBienSoIP.Text = ipTarget;
                }

                lstDetectedCameras.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// THỬ LUỒNG HIỂN THỊ CAMERA CHỐNG TREO 
        /// </summary>
        private void BtnThuCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LibVLCSharp.Shared.Core.Initialize();
                if (sender is not Button btn) return;

                string ipNhapVao = string.Empty;
                string tenViTri = string.Empty;

                if (btn.Parent is Grid parentGrid)
                {
                    var textBox = parentGrid.Children.OfType<TextBox>().FirstOrDefault();
                    var textBlock = parentGrid.Children.OfType<TextBlock>().FirstOrDefault();
                    if (textBox != null) ipNhapVao = textBox.Text.Trim();
                    if (textBlock != null) tenViTri = textBlock.Text;
                }

                if (string.IsNullOrEmpty(ipNhapVao))
                {
                    MessageBox.Show("Vui lòng nhập hoặc chọn một IP Camera trước khi thử!", "Thông báo");
                    return;
                }

                string user = txtCamUser.Text.Trim();
                string pass = txtCamPass.Password.Trim();

                // Xác định xem nút bấm đang test là luồng Toàn Cảnh hay Biển Số để gán luồng test cho chuẩn
                int streamId = tenViTri.Contains("Toàn cảnh") ? 1 : 0;
                string rtspTestUrl = BuildRtspUrl(ipNhapVao, user, pass, streamId);

                System.Diagnostics.Debug.WriteLine($"---> ĐƯỜNG DẪN RTSP CHẠY THỬ: {rtspTestUrl}");

                Window testWindow = new Window
                {
                    Title = $"Đang thử: {tenViTri} ({ipNhapVao}) - Luồng {(streamId == 0 ? "Chính" : "Phụ")}",
                    Width = 560,
                    Height = 360,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                var vlcView = new LibVLCSharp.WPF.VideoView();
                testWindow.Content = vlcView;

                string[] vlcOptions = new string[]
                {
                    "--rtsp-tcp",
                    "--network-caching=250",
                    "--live-caching=250",
                    "--clock-synchro=0",
                    "--avcodec-hw=any",
                    "--drop-late-frames",
                    "--skip-frames"
                };

                var localLibVlc = new LibVLC(vlcOptions);
                var localMediaPlayer = new LibVLCSharp.Shared.MediaPlayer(localLibVlc);
                vlcView.MediaPlayer = localMediaPlayer;

                testWindow.Show();

                var media = new Media(localLibVlc, rtspTestUrl, FromType.FromLocation);
                localMediaPlayer.Play(media);

                testWindow.Closed += (s, args) =>
                {
                    try
                    {
                        media?.Dispose();
                        if (localMediaPlayer != null)
                        {
                            if (localMediaPlayer.IsPlaying) localMediaPlayer.Stop();
                            localMediaPlayer.Dispose();
                        }
                        localLibVlc?.Dispose();
                        System.Diagnostics.Debug.WriteLine("---> ĐÃ GIẢI PHÓNG HOÀN TOÀN TÀI NGUYÊN CAMERA TEST.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lỗi giải phóng tài nguyên: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối thử: " + ex.Message);
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox) _currentActiveTextBox = textBox;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        #region --- CÁC HÀM BỔ TRỢ XỬ LÝ CHUỖI ĐỘC LẬP PHẦN CỨNG ---

        /// <summary>
        /// Hàm trích xuất IP từ một chuỗi RTSP bất kỳ (An toàn, không lo crash nếu chuỗi rỗng)
        /// </summary>
        private string ExtractIpFromRtsp(string rtspUrl)
        {
            if (string.IsNullOrEmpty(rtspUrl)) return string.Empty;
            if (!rtspUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)) return rtspUrl; // Nếu đã là IP thuần sẵn rồi thì trả về luôn

            try
            {
                var fakeHttp = rtspUrl.Replace("rtsp://", "http://", StringComparison.OrdinalIgnoreCase);
                var uri = new Uri(fakeHttp);
                return uri.Host;
            }
            catch
            {
                try
                {
                    var atIndex = rtspUrl.IndexOf('@');
                    var searchString = atIndex >= 0 ? rtspUrl.Substring(atIndex + 1) : rtspUrl.Substring(7);
                    var colonIndex = searchString.IndexOf(':');
                    if (colonIndex >= 0) return searchString.Substring(0, colonIndex);
                    var slashIndex = searchString.IndexOf('/');
                    if (slashIndex >= 0) return searchString.Substring(0, slashIndex);
                    return searchString;
                }
                catch { return rtspUrl; }
            }
        }


        private string BuildRtspUrl(string ip, string user, string pass, int streamId)
        {
            if (string.IsNullOrEmpty(ip)) return string.Empty;

            // Nếu người dùng điền cả link RTSP thủ công vào ô thì giữ nguyên
            if (ip.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)) return ip;

            string streamPath = (streamId == 0) ? "ch1/main/av_stream" : "ch1/sub/av_stream";

            // Trả về chuỗi RTSP chuẩn mã hóa định dạng thiết bị
            return $"rtsp://{user}:{pass}@{ip}:554/{streamPath}";
        }

        #endregion
    }
}