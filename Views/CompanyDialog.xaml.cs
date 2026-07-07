using System;
using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class CompanyDialog : Window
    {
        private readonly Company _company;
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public CompanyDialog(Company company)
        {
            InitializeComponent();
            _company = company;

            // Load data to form
            txtCode.Text = _company.Code;
            txtName.Text = _company.Name;
            txtAddress.Text = _company.Address;
            txtPhone.Text = _company.Phone;

            if (_company.Status == "Inactive")
            {
                cbStatus.SelectedIndex = 1;
            }
            else
            {
                cbStatus.SelectedIndex = 0;
            }

            // Code is read-only on edit to avoid breaking references
            if (_company.Id > 0)
            {
                txtCode.IsEnabled = false;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string code = txtCode.Text.Trim();
            string name = txtName.Text.Trim();
            string address = txtAddress.Text.Trim();
            string phone = txtPhone.Text.Trim();
            string status = ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString() ?? "Active";

            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show("Mã công ty không được để trống.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCode.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Tên công ty không được để trống.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            try
            {
                _company.Code = code;
                _company.Name = name;
                _company.Address = string.IsNullOrWhiteSpace(address) ? null : address;
                _company.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone;
                _company.Status = status;

                if (_company.Id == 0)
                {
                    // Create
                    if (_service.ValidateDuplicateCompanyCode(code))
                    {
                        MessageBox.Show("Mã công ty đã tồn tại trong hệ thống.", "Lỗi trùng lặp", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtCode.Focus();
                        return;
                    }
                    _service.InsertCompany(_company);
                }
                else
                {
                    // Update
                    _service.UpdateCompany(_company);
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
