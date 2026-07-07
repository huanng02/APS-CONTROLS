using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class EmployeeDialog : Window
    {
        private readonly Employee _employee;
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();
        private string? _base64Avatar;
        private string? _oldCardUID;

        public EmployeeDialog(Employee employee)
        {
            InitializeComponent();
            _employee = employee;

            LoadDropdowns();
            PopulateEmployeeData();

            try
            {
                RFIDEventRouterService.Instance.SetTerminalContext(Environment.MachineName, RFIDContextType.CardEnrollment);
                CardEnrollmentHandler.OnCardEnrolled += OnCardEnrolledByRouter;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error subscribing to RFID events: " + ex.Message);
            }
        }

        private void OnCardEnrolledByRouter(string uid) =>
            Dispatcher.BeginInvoke(new Action(() => txtCardUID.Text = RFIDEventRouterService.ChuanHoaUID(uid)));

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                CardEnrollmentHandler.OnCardEnrolled -= OnCardEnrolledByRouter;
                RFIDEventRouterService.Instance.ResetTerminalToDefault(Environment.MachineName);
            }
            catch { }
            base.OnClosed(e);
        }

        private void LoadDropdowns()
        {
            try
            {
                // Companies
                var comps = _service.GetAllCompanies();
                cbCompany.ItemsSource = comps;

                // Positions
                var poss = _service.GetPositions();
                cbPosition.ItemsSource = poss;

                // Card Ticket Types
                var ticketTypes = new LoaiVeService().GetAll();
                cbCardLoaiVe.ItemsSource = ticketTypes;

                // Card Vehicle Types
                var vehicleTypes = new LoaiXeService().GetAll();
                cbCardLoaiXe.ItemsSource = vehicleTypes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateEmployeeData()
        {
            txtFullName.Text = _employee.FullName;
            txtEmployeeCode.Text = _employee.EmployeeCode;
            txtPhone.Text = _employee.Phone;
            txtEmail.Text = _employee.Email;
            txtCCCD.Text = _employee.CCCD;
            _base64Avatar = _employee.Avatar;

            if (_employee.CompanyId > 0)
            {
                cbCompany.SelectedValue = _employee.CompanyId;
                cbCompany_SelectionChanged(null, null); // load departments
            }

            if (_employee.DepartmentId.HasValue && _employee.DepartmentId.Value > 0)
            {
                cbDepartment.SelectedValue = _employee.DepartmentId.Value;
            }

            if (_employee.PositionId > 0)
            {
                cbPosition.SelectedValue = _employee.PositionId;
            }

            if (_employee.Status == "Inactive")
            {
                cbStatus.SelectedIndex = 1;
            }
            else
            {
                cbStatus.SelectedIndex = 0;
            }

            // Code is read-only on edit
            if (_employee.Id > 0)
            {
                txtEmployeeCode.IsEnabled = false;

                // Reload employee from database to get active card info (since VM clone doesn't include it)
                var dbEmp = _service.GetEmployeeById(_employee.Id);
                if (dbEmp != null)
                {
                    _employee.CardUID = dbEmp.CardUID;
                    _employee.CardStatus = dbEmp.CardStatus;
                    _employee.CardExpiration = dbEmp.CardExpiration;
                }
            }

            // Fill card info if employee has an active card
            if (!string.IsNullOrEmpty(_employee.CardUID))
            {
                txtCardUID.Text = _employee.CardUID;
                _oldCardUID = _employee.CardUID;

                var card = _service.GetRFIDCardByUidOrId(_employee.CardUID);
                if (card != null)
                {
                    txtCardBienSo.Text = card.BienSo;
                    cbCardLoaiVe.SelectedValue = card.LoaiVeId;
                    cbCardLoaiXe.SelectedValue = card.LoaiXeId;
                    dpCardExpiration.SelectedDate = card.NgayHetHan;
                }
            }
            else
            {
                // defaults for new card
                dpCardExpiration.SelectedDate = DateTime.Now.AddYears(1);
                if (cbCardLoaiVe.Items.Count > 0) cbCardLoaiVe.SelectedIndex = 0;
                if (cbCardLoaiXe.Items.Count > 0) cbCardLoaiXe.SelectedIndex = 0;
            }

            // Preview Avatar
            ShowAvatarPreview();
        }

        private void ShowAvatarPreview()
        {
            if (string.IsNullOrEmpty(_base64Avatar))
            {
                // Set default placeholder or clear
                imgAvatarPreview.Source = null;
                return;
            }

            try
            {
                byte[] binaryData = Convert.FromBase64String(_base64Avatar);
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(binaryData);
                bi.EndInit();
                imgAvatarPreview.Source = bi;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error showing avatar preview: " + ex.Message);
            }
        }

        private void cbCompany_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            if (cbCompany.SelectedValue == null)
            {
                cbDepartment.ItemsSource = null;
                return;
            }

            try
            {
                int companyId = (int)cbCompany.SelectedValue;
                var depts = _service.GetDepartments(companyId);
                cbDepartment.ItemsSource = depts;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading departments: " + ex.Message);
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Tệp hình ảnh (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|Tất cả các tệp (*.*)|*.*",
                Title = "Chọn ảnh nhân viên"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    txtAvatarPath.Text = ofd.FileName;
                    byte[] bytes = File.ReadAllBytes(ofd.FileName);
                    
                    // Compress if image is too large (over 200KB) to save DB space
                    if (bytes.Length > 200 * 1024)
                    {
                        // Standard conversion to Base64
                        _base64Avatar = Convert.ToBase64String(bytes);
                    }
                    else
                    {
                        _base64Avatar = Convert.ToBase64String(bytes);
                    }

                    ShowAvatarPreview();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi nạp ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string fullName = txtFullName.Text.Trim();
            string code = txtEmployeeCode.Text.Trim();
            string phone = txtPhone.Text.Trim();
            string email = txtEmail.Text.Trim();
            string cccd = txtCCCD.Text.Trim();
            string cardUid = txtCardUID.Text.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Tên nhân viên không được để trống.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtFullName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("Mã nhân viên không được để trống.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtEmployeeCode.Focus();
                return;
            }

            if (cbCompany.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn công ty.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbCompany.Focus();
                return;
            }

            if (cbPosition.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn chức vụ.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbPosition.Focus();
                return;
            }

            int companyId = (int)cbCompany.SelectedValue;
            int positionId = (int)cbPosition.SelectedValue;
            int? departmentId = cbDepartment.SelectedValue as int?;
            if (departmentId == 0) departmentId = null;

            string status = ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString() ?? "Active";

            // RFID properties verification
            int veId = cbCardLoaiVe.SelectedValue != null ? Convert.ToInt32(cbCardLoaiVe.SelectedValue) : 0;
            int xeId = cbCardLoaiXe.SelectedValue != null ? Convert.ToInt32(cbCardLoaiXe.SelectedValue) : 0;
            string bienSo = txtCardBienSo.Text.Trim();
            DateTime? ngayHetHan = dpCardExpiration.SelectedDate;

            if (!string.IsNullOrWhiteSpace(cardUid))
            {
                if (veId == 0)
                {
                    MessageBox.Show("Vui lòng chọn loại vé cho thẻ RFID.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    cbCardLoaiVe.Focus();
                    return;
                }
                if (xeId == 0)
                {
                    MessageBox.Show("Vui lòng chọn loại xe cho thẻ RFID.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    cbCardLoaiXe.Focus();
                    return;
                }

                // Check card assignment
                try
                {
                    var existingCard = _service.GetRFIDCardByUidOrId(cardUid);
                    if (existingCard != null && existingCard.EmployeeId.HasValue && existingCard.EmployeeId.Value > 0)
                    {
                        if (existingCard.EmployeeId.Value != _employee.Id)
                        {
                            var confirm = MessageBox.Show(
                                $"Thẻ '{cardUid}' hiện đang được gán cho nhân viên khác. Bạn có muốn CHUYỂN thẻ này sang cho nhân viên '{fullName}'?",
                                "Xác nhận chuyển thẻ", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (confirm == MessageBoxResult.No)
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi kiểm tra thẻ RFID: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                _employee.FullName = fullName;
                _employee.EmployeeCode = code;
                _employee.CompanyId = companyId;
                _employee.PositionId = positionId;
                _employee.DepartmentId = departmentId;
                _employee.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone;
                _employee.Email = string.IsNullOrWhiteSpace(email) ? null : email;
                _employee.CCCD = string.IsNullOrWhiteSpace(cccd) ? null : cccd;
                _employee.Avatar = _base64Avatar;
                _employee.Status = status;

                if (_employee.Id == 0)
                {
                    if (_service.ValidateDuplicateEmployeeCode(code))
                    {
                        MessageBox.Show("Mã nhân viên đã tồn tại trong hệ thống.", "Lỗi trùng lặp", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtEmployeeCode.Focus();
                        return;
                    }
                    _employee.Id = _service.InsertEmployee(_employee);
                }
                else
                {
                    _service.UpdateEmployee(_employee);
                }

                // Save/update RFID Card association
                if (string.IsNullOrWhiteSpace(cardUid))
                {
                    // De-assign if there was an old card
                    if (!string.IsNullOrEmpty(_oldCardUID))
                    {
                        _service.RemoveRFIDCard(_oldCardUID);
                    }
                }
                else
                {
                    // If card UID changed
                    if (cardUid != _oldCardUID)
                    {
                        if (!string.IsNullOrEmpty(_oldCardUID))
                        {
                            _service.RemoveRFIDCard(_oldCardUID);
                        }
                        _service.AssignRFIDCard(cardUid, _employee.Id);
                    }

                    // Query the card and update custom details
                    var assignedCard = _service.GetRFIDCardByUidOrId(cardUid);
                    if (assignedCard != null)
                    {
                        var cardToUpdate = new Models.RFIDCards
                        {
                            Id = assignedCard.Id,
                            CardUID = cardUid,
                            CardName = string.IsNullOrEmpty(assignedCard.CardName) ? ("Employee Card " + cardUid) : assignedCard.CardName,
                            BienSo = bienSo,
                            LoaiXeId = xeId,
                            LoaiVeId = veId,
                            NgayDangKy = assignedCard.NgayTao == DateTime.MinValue ? DateTime.Now : assignedCard.NgayTao,
                            NgayHetHan = ngayHetHan,
                            TrangThai = "Active",
                            EmployeeId = _employee.Id
                        };
                        new RFIDCardService().Update(cardToUpdate);
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
