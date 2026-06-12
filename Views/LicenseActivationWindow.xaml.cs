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
                FingerprintBox.Text = LicenseValidationService.Instance.GetLocalFingerprint();
                ServerUrlBlock.Text = LicenseValidationService.Instance.GetServerUrl();
                StatusBlock.Text = string.Empty;
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Không thể lấy mã phần cứng: {ex.Message}";
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
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

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            string licenseKey = LicenseKeyBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(licenseKey))
            {
                StatusBlock.Text = "Vui lòng nhập mã kích hoạt.";
                return;
            }

            // Disable UI
            ActivateButton.IsEnabled = false;
            LicenseKeyBox.IsEnabled = false;
            StatusBlock.Foreground = System.Windows.Media.Brushes.LightBlue;
            StatusBlock.Text = "Đang kết nối máy chủ kích hoạt...";

            try
            {
                var result = await LicenseValidationService.Instance.ActivateOnlineAsync(licenseKey);

                if (result.Success)
                {
                    IsActivated = true;
                    MessageBox.Show("Kích hoạt bản quyền thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    StatusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 101, 101));
                    StatusBlock.Text = result.ErrorMsg;
                }
            }
            catch (Exception ex)
            {
                StatusBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 101, 101));
                StatusBlock.Text = $"Lỗi kích hoạt: {ex.Message}";
            }
            finally
            {
                ActivateButton.IsEnabled = true;
                LicenseKeyBox.IsEnabled = true;
            }
        }

        private void LicenseKeyBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Activate_Click(this, new RoutedEventArgs());
            }
        }
    }
}
