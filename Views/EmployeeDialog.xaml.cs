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

        public EmployeeDialog(Employee employee)
        {
            InitializeComponent();
            _employee = employee;

            LoadDropdowns();
            PopulateEmployeeData();
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
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|All files (*.*)|*.*",
                Title = "Select Employee Photo"
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
                    _service.InsertEmployee(_employee);
                }
                else
                {
                    _service.UpdateEmployee(_employee);
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
