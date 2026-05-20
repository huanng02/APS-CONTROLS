using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    public partial class UserAddEditWindow : Window
    {
        private readonly bool _isCreate;
        public UserUpsertModel Result { get; private set; } = new();

        public UserAddEditWindow(bool isCreate, List<RoleOption> roles, UserListItem? editingUser = null)
        {
            InitializeComponent();
            _isCreate = isCreate;
            
            TxtTitle.Text = _isCreate ? "THÊM NGƯỜI DÙNG MỚI" : "CHỈNH SỬA NGƯỜI DÙNG";
            PanelPassword.Visibility = _isCreate ? Visibility.Visible : Visibility.Collapsed;
            
            if (!_isCreate)
            {
                TxtUsername.IsEnabled = false;
                TxtUsername.Opacity = 0.6;
            }

            CboRole.ItemsSource = roles;
            CboStatus.ItemsSource = new[] { "Active", "Disabled" };
            CboStatus.SelectedIndex = 0;

            if (editingUser != null)
            {
                TxtTen.Text = editingUser.Ten;
                TxtUsername.Text = editingUser.Username;
                CboRole.SelectedValue = editingUser.RoleId;
                CboStatus.SelectedItem = editingUser.TrangThai;
            }

            this.MouseDown += (s, e) => {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                    this.DragMove();
            };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTen.Text) ||
                string.IsNullOrWhiteSpace(TxtUsername.Text) ||
                CboRole.SelectedValue == null ||
                CboStatus.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isCreate && TxtPassword.Password.Trim().Length < 6)
            {
                MessageBox.Show("Mật khẩu phải có ít nhất 6 ký tự.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new UserUpsertModel
            {
                Ten = TxtTen.Text.Trim(),
                Username = TxtUsername.Text.Trim(),
                Password = _isCreate ? TxtPassword.Password.Trim() : string.Empty,
                RoleId = Convert.ToInt32(CboRole.SelectedValue),
                TrangThai = CboStatus.SelectedItem.ToString()
            };

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
