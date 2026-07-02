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



        private void LicensesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedLicense = LicensesGrid.SelectedItem as LicenseViewModel;
            if (selectedLicense != null)
            {
                var machineList = selectedLicense.ActiveMachines.Select(m => new MachineViewModel
                {
                    MachineFingerprint = m.MachineFingerprint,
                    Status             = m.Status,
                    CreatedAt          = m.CreatedAt,
                    LastSeenAt         = m.LastSeenAt,
                    LastActivatedAt    = m.LastActivatedAt,
                    MachineName        = m.MachineName ?? "",
                    CpuId              = m.CpuId ?? "",
                    DiskSerial         = m.DiskSerial ?? "",
                    MacAddress         = m.MacAddress ?? "",
                    OsVersion          = m.OsVersion ?? "",
                    ActivatedFromIp    = m.ActivatedFromIp ?? "",
                    MotherboardSerial  = m.MotherboardSerial ?? "",
                    BiosSerial         = m.BiosSerial ?? ""
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

        private async void ResetMachineFromRow_Click(object sender, RoutedEventArgs e)
        {
            var selectedLicense = LicensesGrid.SelectedItem as LicenseViewModel;
            var button = sender as Button;
            var selectedMachine = button?.DataContext as MachineViewModel;

            if (selectedLicense == null || selectedMachine == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn RESET (xóa đăng ký) cho máy này?\nThao tác này sẽ giải phóng 1 slot của key để máy khác có thể kích hoạt.\n\nFingerprint: {selectedMachine.MachineFingerprint}", 
                "Xác nhận Reset máy", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            StatusBlock.Text = "Đang reset máy...";
            try
            {
                var url = $"{GetServerUrl()}/api/license/reset-machine";
                var requestBody = new 
                { 
                    LicenseKey = selectedLicense.LicenseKey, 
                    MachineFingerprint = selectedMachine.MachineFingerprint 
                };
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    StatusBlock.Text = $"Lỗi reset máy: {response.StatusCode}";
                    return;
                }

                MessageBox.Show("Reset đăng ký máy thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi kết nối khi reset máy: {ex.Message}";
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

        private void OfflineGenerator_Click(object sender, RoutedEventArgs e)
        {
            var offlineWin = new OfflineLicenseGeneratorWindow(GetServerUrl(), _httpClient);
            if (offlineWin.ShowDialog() == true)
            {
                LoadData();
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
        public string Product { get; set; } = "APS";
        public string CustomerName { get; set; } = string.Empty;
        public string LicenseType { get; set; } = "Commercial";
        public string Features { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public List<MachineDto> ActiveMachines { get; set; } = new();
    }

    public class MachineDto
    {
        public string MachineFingerprint { get; set; } = string.Empty;
        public string Status { get; set; } = "ACTIVE";
        public DateTime CreatedAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public DateTime? LastActivatedAt { get; set; }
        // Hardware info
        public string? MachineName { get; set; }
        public string? CpuId { get; set; }
        public string? DiskSerial { get; set; }
        public string? MacAddress { get; set; }
        public string? OsVersion { get; set; }
        public string? ActivatedFromIp { get; set; }
        public string? MotherboardSerial { get; set; }
        public string? BiosSerial { get; set; }
    }

    public class MachineViewModel
    {
        public string MachineFingerprint { get; set; } = string.Empty;
        public string Status { get; set; } = "ACTIVE";
        public string FingerprintShort => MachineFingerprint.Length > 8 ? MachineFingerprint[..8] + "..." : MachineFingerprint;
        public DateTime CreatedAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public DateTime? LastActivatedAt { get; set; }

        // Hardware info
        public string MachineName { get; set; } = "";
        public string CpuId { get; set; } = "";
        public string DiskSerial { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string OsVersion { get; set; } = "";
        public string ActivatedFromIp { get; set; } = "";
        public string MotherboardSerial { get; set; } = "";
        public string BiosSerial { get; set; } = "";

        // Formatted display
        public string DisplayName => !string.IsNullOrWhiteSpace(MachineName)
            ? MachineName
            : $"Device #{FingerprintShort}";
        public string CreatedAtFormatted => $"Kích hoạt: {CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        public string LastSeenAtFormatted => LastSeenAt.Year < 2000
            ? "Chưa nhận"
            : LastSeenAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        public string LastActivatedFormatted => LastActivatedAt.HasValue
            ? LastActivatedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            : "--";
        public string MacAddressFormatted => string.IsNullOrWhiteSpace(MacAddress)
            ? "--"
            : string.Join(":", Enumerable.Range(0, MacAddress.Length / 2)
                .Take(6)
                .Select(i => MacAddress.Substring(i * 2, 2)));
    }
}