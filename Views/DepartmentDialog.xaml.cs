using System;
using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class DepartmentDialog : Window
    {
        private readonly Department _department;
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public DepartmentDialog(Department department)
        {
            InitializeComponent();
            _department = department;

            LoadCompanies();

            // Set values
            txtDepartmentName.Text = _department.DepartmentName;
            if (_department.CompanyId > 0)
            {
                cbCompany.SelectedValue = _department.CompanyId;
            }
            if (_department.Status == "Inactive")
            {
                cbStatus.SelectedIndex = 1;
            }
            else
            {
                cbStatus.SelectedIndex = 0;
            }
        }

        private void LoadCompanies()
        {
            try
            {
                var list = _service.GetAllCompanies();
                cbCompany.ItemsSource = list;
                if (list.Count > 0)
                {
                    cbCompany.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách công ty: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (cbCompany.SelectedValue == null)
            {
                MessageBox.Show("Vui lòng chọn công ty.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbCompany.Focus();
                return;
            }

            string deptName = txtDepartmentName.Text.Trim();
            if (string.IsNullOrWhiteSpace(deptName))
            {
                MessageBox.Show("Tên phòng ban không được để trống.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtDepartmentName.Focus();
                return;
            }

            int companyId = (int)cbCompany.SelectedValue;
            string status = ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString() ?? "Active";

            try
            {
                _department.CompanyId = companyId;
                _department.DepartmentName = deptName;
                _department.Status = status;

                if (_department.Id == 0)
                {
                    _service.InsertDepartment(_department);
                }
                else
                {
                    _service.UpdateDepartment(_department);
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
