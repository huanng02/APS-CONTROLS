using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace LicenseTool
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private string GetServerUrl()
        {
            return ServerUrlBox.Text?.Trim().TrimEnd('/') ?? "http://localhost:5000";
        }

        private async void LoadData()
        {
            StatusBlock.Text = "Đang tải dữ liệu...";
            try
            {
                var url = $"{GetServerUrl()}/api/license/list";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    StatusBlock.Text = $"Lỗi máy chủ: {response.StatusCode}";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var licenses = JsonConvert.DeserializeObject<List<LicenseViewModel>>(json);

                LicensesGrid.ItemsSource = licenses;
                StatusBlock.Text = $"Tải dữ liệu thành công. Tìm thấy {licenses?.Count ?? 0} license key.";
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi kết nối máy chủ: {ex.Message}";
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void IsPermanentCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (ExpireDaysBox != null)
            {
                ExpireDaysBox.IsEnabled = false;
                ExpireDaysBox.Text = "Vĩnh viễn";
            }
        }

        private void IsPermanentCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ExpireDaysBox != null)
            {
                ExpireDaysBox.IsEnabled = true;
                ExpireDaysBox.Text = "365";
            }
        }

        private async void CreateKey_Click(object sender, RoutedEventArgs e)
        {
            int days = 365;
            if (IsPermanentCheckBox.IsChecked == true)
            {
                days = -1;
            }
            else
            {
                if (!int.TryParse(ExpireDaysBox.Text, out days) || days <= 0)
                {
                    MessageBox.Show("Vui lòng nhập số ngày hết hạn hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (!int.TryParse(MaxMachinesBox.Text, out var maxMachines) || maxMachines <= 0)
            {
                MessageBox.Show("Vui lòng nhập số lượng máy tối đa hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusBlock.Text = "Đang tạo license key...";
            try
            {
                var url = $"{GetServerUrl()}/api/license/create";
                var requestBody = new { ExpireDays = days, MaxMachines = maxMachines };
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    StatusBlock.Text = $"Không thể tạo license key: {response.StatusCode}";
                    return;
                }

                var resString = await response.Content.ReadAsStringAsync();
                var newKey = JsonConvert.DeserializeAnonymousType(resString, new { LicenseKey = "" });

                MessageBox.Show($"Tạo License Key thành công:\n\n{newKey?.LicenseKey}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi kết nối khi tạo key: {ex.Message}";
            }
        }

        private void LicensesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedLicense = LicensesGrid.SelectedItem as LicenseViewModel;
            if (selectedLicense != null)
            {
                var machineList = selectedLicense.ActiveMachines.Select(m => new MachineViewModel
                {
                    MachineFingerprint = m.MachineFingerprint,
                    CreatedAt = m.CreatedAt,
                    LastSeenAt = m.LastSeenAt
                }).ToList();

                MachinesGrid.ItemsSource = machineList;
                SelectedKeyLabel.Text = $" - Key: {selectedLicense.LicenseKey}";
                RevokeLicenseBtn.IsEnabled = true;
            }
            else
            {
                MachinesGrid.ItemsSource = null;
                SelectedKeyLabel.Text = string.Empty;
                RevokeLicenseBtn.IsEnabled = false;
            }
        }

        private async void RevokeMachineFromRow_Click(object sender, RoutedEventArgs e)
        {
            var selectedLicense = LicensesGrid.SelectedItem as LicenseViewModel;
            var button = sender as Button;
            var selectedMachine = button?.DataContext as MachineViewModel;

            if (selectedLicense == null || selectedMachine == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn thu hồi đăng ký cho máy này?\n\nFingerprint: {selectedMachine.MachineFingerprint}", 
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            StatusBlock.Text = "Đang thu hồi máy...";
            try
            {
                var url = $"{GetServerUrl()}/api/license/revoke";
                var requestBody = new 
                { 
                    LicenseKey = selectedLicense.LicenseKey, 
                    MachineFingerprint = selectedMachine.MachineFingerprint 
                };
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    StatusBlock.Text = $"Lỗi thu hồi: {response.StatusCode}";
                    return;
                }

                MessageBox.Show("Thu hồi đăng ký máy thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi kết nối khi thu hồi máy: {ex.Message}";
            }
        }

        private async void RevokeLicense_Click(object sender, RoutedEventArgs e)
        {
            var selectedLicense = LicensesGrid.SelectedItem as LicenseViewModel;
            if (selectedLicense == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn THU HỒI TOÀN BỘ License Key:\n\n{selectedLicense.LicenseKey}?", 
                "Cảnh báo", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            StatusBlock.Text = "Đang thu hồi toàn bộ license...";
            try
            {
                var url = $"{GetServerUrl()}/api/license/revoke";
                var requestBody = new 
                { 
                    LicenseKey = selectedLicense.LicenseKey, 
                    MachineFingerprint = string.Empty 
                };
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    StatusBlock.Text = $"Lỗi thu hồi license: {response.StatusCode}";
                    return;
                }

                MessageBox.Show("Thu hồi toàn bộ License Key thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi kết nối khi thu hồi license: {ex.Message}";
            }
        }

        private void CopyKeyFromRow_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var license = button?.DataContext as LicenseViewModel;
            if (license != null && !string.IsNullOrEmpty(license.LicenseKey))
            {
                try
                {
                    Clipboard.SetText(license.LicenseKey);
                    MessageBox.Show($"Đã sao chép License Key vào bộ nhớ tạm:\n\n{license.LicenseKey}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi sao chép: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class LicenseViewModel
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = string.Empty;
        public int MaxMachines { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ExpireAt { get; set; }
        public string ExpireAtFormatted => ExpireAt.Year >= 2999 ? "Vĩnh viễn" : ExpireAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public List<MachineDto> ActiveMachines { get; set; } = new();
    }

    public class MachineDto
    {
        public string MachineFingerprint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastSeenAt { get; set; }
    }

    public class MachineViewModel
    {
        public string MachineFingerprint { get; set; } = string.Empty;
        public string FingerprintShort => MachineFingerprint.Length > 8 ? $"Device #{MachineFingerprint.Substring(0, 8)}" : "Device";
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => $"Kích hoạt: {CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        public DateTime LastSeenAt { get; set; }
    }
}