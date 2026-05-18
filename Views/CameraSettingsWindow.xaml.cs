using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class CameraSettingsWindow : Window
    {
        public CameraSettingsWindow()
        {
            InitializeComponent();
            LoadCameras();
        }

        private void LoadCameras()
        {
            var config = AppConfig.Load();
            var cam = config.Cameras;
            var zk = config.ZKTeco;

            txtVaoToanCanhIP.Text = cam.VaoToanCanh;
            txtVaoBienSoIP.Text = cam.VaoBienSo;
            txtRaToanCanhIP.Text = cam.RaToanCanh;
            txtRaBienSoIP.Text = cam.RaBienSo;

            if (txtCameraCount != null)
                txtCameraCount.Text = "📡 Hệ thống đang sử dụng kết nối Camera IP qua mạng LAN.";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Lấy thực thể cấu hình hiện tại
                var config = AppConfig.Load();

                //Cập nhật thông tin Camera từ giao diện
                config.Cameras.VaoToanCanh = txtVaoToanCanhIP.Text;
                config.Cameras.VaoBienSo = txtVaoBienSoIP.Text;
                config.Cameras.RaToanCanh = txtRaToanCanhIP.Text;
                config.Cameras.RaBienSo = txtRaBienSoIP.Text;

                //Lưu xuống file config.json
                config.Save();

                MessageBox.Show("Đã lưu cấu hình Camera IP thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Có lỗi khi lưu cấu hình: {ex.Message}", "Lỗi");
            }
        }

        private void BtnScanIP_Click(object sender, RoutedEventArgs e)
        {
            // 1. Khóa nút bấm ngay trên luồng UI để tránh người dùng bấm liên tục
            btnScanIP.IsEnabled = false;
            btnScanIP.Content = "⏳ Đang quét mạng...";

            // 2. Đẩy RỜI TOÀN BỘ tiến trình quét ra luồng phụ để UI không bao giờ bị đơ
            Task.Run(async () =>
            {
                try
                {
                    var service = new NetworkDiscoveryService();
                    // Truyền dải IP của máy bạn (.2.)
                    var results = await service.ScanNetworkAsync("192.168.2.");

                    // 3. Khi quét xong, ép việc hiển thị kết quả quay lại luồng UI để không bị crash
                    this.Dispatcher.Invoke(() =>
                    {
                        if (results != null && results.Count > 0)
                        {
                            string msg = "Tìm thấy các thiết bị online:\n";
                            foreach (var item in results)
                            {
                                // Cắt lấy 3 cặp số đầu của MAC (Ví dụ: A4:E8:8D)
                                string oui = (item.MAC ?? "").Length >= 8 ? item.MAC.Substring(0, 8).ToUpper() : "N/A";

                                // Hiện thêm mã OUI này lên màn hình để chúng ta copy vào code
                                msg += $"📍 IP: {item.IP} - {item.Vendor} (Mã OUI: {oui}) [MAC FULL: {item.MAC}]\n";
                            }
                            MessageBox.Show(msg, "Kết quả quét");
                        }
                        else
                        {
                            MessageBox.Show("Không tìm thấy thiết bị nào ở dải mạng 192.168.2.x\nHãy chắc chắn Camera đã bật nguồn và cắm chung Hub/Switch.", "Thông báo");
                        }

                        // Mở lại nút bấm
                        btnScanIP.IsEnabled = true;
                        btnScanIP.Content = "🔍 Quét nhanh IP";
                    });
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Lỗi thực thi quét: " + ex.Message);
                        btnScanIP.IsEnabled = true;
                        btnScanIP.Content = "🔍 Quét nhanh IP";
                    });
                }
            });
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
