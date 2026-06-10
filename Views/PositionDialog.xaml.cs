using System;
using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class PositionDialog : Window
    {
        private readonly Position _position;
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public PositionDialog(Position position)
        {
            InitializeComponent();
            _position = position;

            txtPositionName.Text = _position.PositionName;
            if (_position.Status == "Inactive")
            {
                cbStatus.SelectedIndex = 1;
            }
            else
            {
                cbStatus.SelectedIndex = 0;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string posName = txtPositionName.Text.Trim();
            if (string.IsNullOrWhiteSpace(posName))
            {
                MessageBox.Show("Tên chức vụ không được để trống.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPositionName.Focus();
                return;
            }

            string status = ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString() ?? "Active";

            try
            {
                _position.PositionName = posName;
                _position.Status = status;

                if (_position.Id == 0)
                {
                    _service.InsertPosition(_position);
                }
                else
                {
                    _service.UpdatePosition(_position);
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
