using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace LicenseTool
{
    public partial class OfflineLicenseGeneratorWindow : Window
    {
        private readonly string _serverUrl;
        private readonly HttpClient _httpClient;
        private ServerHardwareDetails _loadedHardware = new();

        public OfflineLicenseGeneratorWindow(string serverUrl, HttpClient httpClient)
        {
            InitializeComponent();
            _serverUrl = serverUrl;
            _httpClient = httpClient;
            Owner = Application.Current.MainWindow;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void IsPermanentCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (ExpireDaysTextBox != null)
            {
                ExpireDaysTextBox.IsEnabled = false;
                ExpireDaysTextBox.Text = "Vĩnh viễn";
            }
        }

        private void IsPermanentCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ExpireDaysTextBox != null)
            {
                ExpireDaysTextBox.IsEnabled = true;
                ExpireDaysTextBox.Text = "365";
            }
        }

        private void LoadRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Activation Request Files (*.req)|*.req|All Files (*.*)|*.*",
                    Title = "Chọn File Yêu Cầu Kích Hoạt"
                };

                if (ofd.ShowDialog() == true)
                {
                    string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                    var request = JsonConvert.DeserializeAnonymousType(json, new
                    {
                        version = 1,
                        machineId = "",
                        hardware = new ServerHardwareDetails(),
                        requestDate = ""
                    });

                    if (request != null && !string.IsNullOrEmpty(request.machineId))
                    {
                        MachineIdTextBox.Text = request.machineId;
                        _loadedHardware = request.hardware ?? new ServerHardwareDetails();

                        CpuInfoText.Text = $"CPU ID: {_loadedHardware.Cpu}";
                        MoboInfoText.Text = $"Motherboard: {_loadedHardware.Motherboard}";
                        DiskInfoText.Text = $"Disk Serial: {_loadedHardware.Disk}";
                        BiosInfoText.Text = $"BIOS Serial: {_loadedHardware.Bios}";

                        StatusBlock.Text = "Nạp file yêu cầu kích hoạt thành công!";
                    }
                    else
                    {
                        MessageBox.Show("File yêu cầu kích hoạt không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nạp file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GenerateLicense_Click(object sender, RoutedEventArgs e)
        {
            string customer = CustomerTextBox.Text?.Trim() ?? string.Empty;
            string machineId = MachineIdTextBox.Text?.Trim()?.ToUpperInvariant() ?? string.Empty;
            string product = ProductTextBox.Text?.Trim() ?? "APS";
            string licenseType = (LicenseTypeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Commercial";
            string licenseKey = LicenseKeyTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(customer))
            {
                MessageBox.Show("Vui lòng nhập tên khách hàng / dự án.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(machineId))
            {
                MessageBox.Show("Vui lòng nhập mã máy khách (Machine ID).", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (machineId.Length != 12)
            {
                var confirmId = MessageBox.Show("Mã Machine ID khuyên dùng là 12 ký tự hex. Bạn có chắc chắn muốn tiếp tục với mã này không?", 
                    "Xác nhận Machine ID", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirmId != MessageBoxResult.Yes) return;
            }

            // If hardware details are empty (user entered machineId manually)
            if (string.IsNullOrEmpty(_loadedHardware.Cpu) && 
                string.IsNullOrEmpty(_loadedHardware.Motherboard) && 
                string.IsNullOrEmpty(_loadedHardware.Disk) && 
                string.IsNullOrEmpty(_loadedHardware.Bios))
            {
                var confirm = MessageBox.Show(
                    "Cảnh báo: Bạn đang tạo bản quyền thủ công không thông qua file yêu cầu kích hoạt (.req).\n" +
                    "Hệ thống sẽ không thể kiểm tra chi tiết linh kiện phần cứng (chỉ so khớp Machine ID).\n" +
                    "Bạn có chắc chắn muốn tiếp tục?", 
                    "Cảnh báo bảo mật", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;

                // Fill placeholders
                _loadedHardware = new ServerHardwareDetails
                {
                    Cpu = "CPU_UNKNOWN",
                    Motherboard = "MB_UNKNOWN",
                    Disk = "DISK_UNKNOWN",
                    Bios = "BIOS_UNKNOWN"
                };
            }

            int expireDays = 365;
            if (IsPermanentCheckBox.IsChecked == true)
            {
                expireDays = -1;
            }
            else
            {
                if (!int.TryParse(ExpireDaysTextBox.Text, out expireDays) || expireDays <= 0)
                {
                    MessageBox.Show("Vui lòng nhập số ngày hết hạn hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Gather features
            var features = new List<string>();
            if (CameraCheckBox.IsChecked == true)  features.Add("CAMERA");
            if (LprCheckBox.IsChecked == true)     features.Add("LPR");
            if (RfidCheckBox.IsChecked == true)    features.Add("RFID");
            if (ReportCheckBox.IsChecked == true)  features.Add("REPORT");

            StatusBlock.Text = "Đang gửi yêu cầu tạo bản quyền lên máy chủ...";
            
            try
            {
                var requestBody = new GenerateOfflineLicenseRequest
                {
                    LicenseKey = licenseKey,
                    Product = product,
                    CustomerName = customer,
                    LicenseType = licenseType,
                    Features = features,
                    MachineId = machineId,
                    Hardware = _loadedHardware,
                    ExpireDays = expireDays
                };

                var url = $"{_serverUrl}/api/license/generate-offline";
                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errReason = response.StatusCode.ToString();
                    try
                    {
                        var resStr = await response.Content.ReadAsStringAsync();
                        var errObj = JsonConvert.DeserializeAnonymousType(resStr, new { Message = "" });
                        if (errObj != null && !string.IsNullOrEmpty(errObj.Message))
                            errReason = errObj.Message;
                    }
                    catch { }

                    StatusBlock.Text = $"Lỗi máy chủ: {errReason}";
                    MessageBox.Show($"Không thể tạo bản quyền: {errReason}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                
                // Prompt to save file
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Offline License File (*.bin)|*.bin",
                    FileName = "license.bin",
                    Title = "Lưu File Bản Quyền Ngoại Tuyến"
                };

                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllText(sfd.FileName, responseJson, Encoding.UTF8);
                    StatusBlock.Text = "Tạo bản quyền thành công!";
                    MessageBox.Show("Đã tạo và lưu file bản quyền ngoại tuyến thành công!\nVui lòng copy file license.bin này vào thư mục C:\\ProgramData\\APS\\ trên máy khách.", 
                        "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                StatusBlock.Text = $"Lỗi kết nối: {ex.Message}";
                MessageBox.Show($"Lỗi hệ thống khi gọi máy chủ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ServerHardwareDetails
    {
        public string Cpu { get; set; } = string.Empty;
        public string Motherboard { get; set; } = string.Empty;
        public string Disk { get; set; } = string.Empty;
        public string Bios { get; set; } = string.Empty;
    }

    public class GenerateOfflineLicenseRequest
    {
        public string? LicenseKey { get; set; }
        public string Product { get; set; } = "APS";
        public string CustomerName { get; set; } = string.Empty;
        public string LicenseType { get; set; } = "Commercial";
        public List<string> Features { get; set; } = new();
        public string MachineId { get; set; } = string.Empty;
        public ServerHardwareDetails Hardware { get; set; } = new();
        public int ExpireDays { get; set; } = 365;
    }

    public class OfflineLicenseResponse
    {
        public int Version { get; set; } = 1;
        public string Product { get; set; } = "APS";
        public string Customer { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public string LicenseType { get; set; } = "Commercial";
        public List<string> Features { get; set; } = new();
        public ServerHardwareDetails Hardware { get; set; } = new();
        public string CreatedDate { get; set; } = string.Empty;
        public string ExpirationDate { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
