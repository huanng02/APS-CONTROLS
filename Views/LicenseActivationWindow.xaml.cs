using System;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class LicenseActivationWindow : Window
    {
        public bool IsActivated { get; private set; } = false;

        public LicenseActivationWindow()
        {
            InitializeComponent();
            LoadFingerprintAndServer();
        }

        private void LoadFingerprintAndServer()
        {
            try
            {
                var details = HardwareFingerprintService.GetHardwareDetails();
                FingerprintBox.Text = HardwareFingerprintService.CalculateMachineId(details);
                StatusBlock.Text = string.Empty;
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Không thể lấy mã phần cứng: {ex.Message}";
            }
        }

        private void ExportRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Activation Request File (*.req)|*.req",
                    FileName = "activation_request.req",
                    Title = "Lưu File Yêu Cầu Kích Hoạt"
                };

                if (sfd.ShowDialog() == true)
                {
                    string json = HardwareFingerprintService.GenerateActivationRequestJson();
                    System.IO.File.WriteAllText(sfd.FileName, json, System.Text.Encoding.UTF8);
                    MessageBox.Show("Đã xuất file yêu cầu kích hoạt thành công!\nVui lòng gửi file này cho nhà quản trị để tạo bản quyền.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi xuất yêu cầu: {ex.Message}";
            }
        }

        private void ImportLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "License File (*.bin)|*.bin",
                    Title = "Chọn File Bản Quyền Kích Hoạt"
                };

                if (ofd.ShowDialog() == true)
                {
                    var importResult = LicenseValidationService.Instance.ImportOfflineLicense(ofd.FileName);

                    if (importResult.Success)
                    {
                        IsActivated = true;
                        MessageBox.Show("Kích hoạt bản quyền ngoại tuyến thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.DialogResult = true;
                        this.Close();
                    }
                    else
                    {
                        StatusBlock.Text = $"Bản quyền không hợp lệ: {importResult.ErrorMsg}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi nhập bản quyền: {ex.Message}";
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(FingerprintBox.Text);
                MessageBox.Show("Đã sao chép mã phần cứng vào bộ nhớ tạm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi sao chép: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
