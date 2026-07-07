using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class EmployeeDetailDialog : Window
    {
        private Employee _employee;
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public EmployeeDetailDialog(Employee employee)
        {
            InitializeComponent();
            _employee = employee;

            DisplayDetails();
        }

        private void DisplayDetails()
        {
            // Title & Name
            lblFullName.Text = _employee.FullName;
            lblCode.Text = $"Employee Code: {_employee.EmployeeCode}";
            Title = $"Employee Details: {_employee.FullName}";

            // Status Badge
            lblStatus.Text = _employee.Status;
            if (_employee.Status == "Inactive")
            {
                badgeStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47)); // Red
            }
            else
            {
                badgeStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 142, 60)); // Green
            }

            // Contact Info
            lblPhone.Text = $"📞 {_employee.Phone ?? "(N/A)"}";
            lblEmail.Text = $"✉️ {_employee.Email ?? "(N/A)"}";
            lblCCCD.Text = $"🆔 {_employee.CCCD ?? "(N/A)"}";

            // Org Info
            lblCompany.Text = _employee.CompanyName ?? "(N/A)";
            lblDepartment.Text = _employee.DepartmentName ?? "(N/A)";
            lblPosition.Text = _employee.PositionName ?? "(N/A)";

            // Avatar Preview
            if (!string.IsNullOrEmpty(_employee.Avatar))
            {
                try
                {
                    byte[] binaryData = Convert.FromBase64String(_employee.Avatar);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = new MemoryStream(binaryData);
                    bi.EndInit();
                    imgAvatar.Source = bi;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error showing details avatar: " + ex.Message);
                    imgAvatar.Source = null;
                }
            }
            else
            {
                imgAvatar.Source = null;
            }

            // RFID Configuration
            if (!string.IsNullOrEmpty(_employee.CardUID))
            {
                panelActiveCard.Visibility = Visibility.Visible;
                panelNoCard.Visibility = Visibility.Collapsed;

                lblCardUID.Text = _employee.CardUID;
                lblCardStatus.Text = _employee.CardStatus ?? "Active";
                lblCardExpiration.Text = _employee.CardExpiration.HasValue 
                    ? _employee.CardExpiration.Value.ToString("dd/MM/yyyy") 
                    : "No Expiration";
            }
            else
            {
                panelActiveCard.Visibility = Visibility.Collapsed;
                panelNoCard.Visibility = Visibility.Visible;
                txtCardUID.Clear();
            }
        }

        private void RefreshEmployeeData()
        {
            var updated = _service.GetEmployeeById(_employee.Id);
            if (updated != null)
            {
                _employee = updated;
                DisplayDetails();
            }
        }

        private void AssignCard_Click(object sender, RoutedEventArgs e)
        {
            string cardUid = txtCardUID.Text.Trim();
            if (string.IsNullOrWhiteSpace(cardUid))
            {
                MessageBox.Show("Vui lòng nhập mã thẻ RFID.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCardUID.Focus();
                return;
            }

            try
            {
                // Check if card is currently assigned to another employee, prompt for confirmation (handles Transfer card!)
                var existingCard = _service.GetRFIDCardByUidOrId(cardUid);
                if (existingCard != null && existingCard.EmployeeId.HasValue && existingCard.EmployeeId.Value > 0)
                {
                    if (existingCard.EmployeeId.Value == _employee.Id)
                    {
                        MessageBox.Show("Thẻ này đã được gán cho nhân viên này rồi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Prompt transfer confirmation!
                    var confirm = MessageBox.Show($"Thẻ '{cardUid}' hiện đang được gán cho nhân viên khác. Bạn có muốn CHUYỂN thẻ này sang cho nhân viên '{_employee.FullName}'?",
                        "Xác nhận chuyển thẻ (Transfer RFID)", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (confirm == MessageBoxResult.No)
                    {
                        return;
                    }
                }

                _service.AssignRFIDCard(cardUid, _employee.Id);
                MessageBox.Show("Gán thẻ RFID thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshEmployeeData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi gán thẻ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveCard_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_employee.CardUID)) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn gỡ thẻ '{_employee.CardUID}' khỏi nhân viên này?", 
                "Gỡ thẻ RFID", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    _service.RemoveRFIDCard(_employee.CardUID);
                    MessageBox.Show("Gỡ thẻ RFID thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshEmployeeData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi gỡ thẻ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RegisterNewCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = new ViewModels.RFIDCardWizardViewModel();
                vm.InitForAdd(_employee.Id);
                // Pre-select monthly tab for employees
                vm.ActiveTabIndex = 1;

                var dlg = new RFIDCardAddEditWindow(null)
                {
                    Owner = this
                };
                dlg.DataContext = vm;
                if (dlg.ShowDialog() == true)
                {
                    var toAdd = new Models.RFIDCards
                    {
                        CardUID = vm.CardUID,
                        CardName = vm.CardName,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId ?? 0,
                        LoaiVeId = vm.LoaiVeId ?? 0,
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai,
                        EmployeeId = vm.EmployeeId
                    };
                    new RFIDCardService().Add(toAdd);
                    MessageBox.Show("Đăng ký thẻ nhân viên thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshEmployeeData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đăng ký thẻ thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
