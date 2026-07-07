using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class UserManagementWindow : Window
    {
        private readonly UserManagementService _service = new();
        private List<RoleOption> _roles = new();
        private CancellationTokenSource? _searchDebounceCts;
        private bool _isInitialized = false;

        public UserManagementWindow()
        {
            InitializeComponent();
            Loaded += UserManagementWindow_Loaded;
        }

        private async void UserManagementWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeDataAsync();
            _isInitialized = true;
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                var allRoles = await _service.GetRolesAsync();
                string currentRoleName = CurrentUserContext.Instance.Role;
                int currentLevel = PermissionMatrixService.GetRoleLevel(currentRoleName);

                // Filter roles strictly lower than current user's level
                _roles = allRoles.Where(r => PermissionMatrixService.GetRoleLevel(r.Name) < currentLevel).ToList();

                var roleFilterList = new List<RoleOption>
                {
                    new RoleOption { Id = -1, Name = "All" }
                };
                roleFilterList.AddRange(_roles);

                CboRoleFilter.ItemsSource = roleFilterList;
                CboRoleFilter.SelectedIndex = 0;

                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                this.Cursor = Cursors.Wait;

                RoleOption? selectedRole = CboRoleFilter.SelectedItem as RoleOption;
                int? roleId = (selectedRole != null && selectedRole.Id != -1) ? selectedRole.Id : null;

                string status = (CboStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
                string search = TxtSearch.Text.Trim();

                var users = await _service.SearchUsersAsync(CurrentUser.Id, search, roleId, status);

                string currentRoleName = CurrentUserContext.Instance.Role;
                int currentLevel = PermissionMatrixService.GetRoleLevel(currentRoleName);

                // Filter users strictly lower than current user's level
                var filteredUsers = users.Where(u => PermissionMatrixService.GetRoleLevel(u.RoleName) < currentLevel).ToList();

                GridUsers.ItemsSource = filteredUsers;
                LblTotalCount.Text = filteredUsers.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private UserListItem? GetSelectedUser() => GridUsers.SelectedItem as UserListItem;

        private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            await DebouncedSearchAsync();
        }

        private async Task DebouncedSearchAsync()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                {
                    await LoadUsersAsync();
                }
            }
            catch (TaskCanceledException) { }
        }

        private async void CboRoleFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            await LoadUsersAsync();
        }

        private async void CboStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            await LoadUsersAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            await AddUserAsync();
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            await EditSelectedUserAsync();
        }

        private async void BtnDisable_Click(object sender, RoutedEventArgs e)
        {
            await DisableSelectedUserAsync();
        }

        private async void BtnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            await ResetPasswordAsync();
        }

        private void BtnPermissionMatrix_Click(object sender, RoutedEventArgs e)
        {
            var win = new RolePermissionMatrixWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnAuditHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new AuditHistoryWindow();
                win.Owner = this;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không có quyền truy cập", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void GridUsers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Only trigger edit if a row was double-clicked
            if (GridUsers.SelectedItem != null)
            {
                await EditSelectedUserAsync();
            }
        }

        private async Task AddUserAsync()
        {
            var dialog = new UserAddEditWindow(isCreate: true, roles: _roles);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            var result = await _service.CreateUserAsync(dialog.Result, CurrentUser.Id);
            MessageBox.Show(result.Message, result.Success ? "Thành công" : "Cảnh báo",
                MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            if (result.Success)
            {
                await LoadUsersAsync();
            }
        }

        private async Task EditSelectedUserAsync()
        {
            var user = GetSelectedUser();
            if (user == null)
            {
                MessageBox.Show("Vui lòng chọn user cần sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new UserAddEditWindow(isCreate: false, roles: _roles, editingUser: user);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            var result = await _service.UpdateUserAsync(user.Id, dialog.Result.Ten, dialog.Result.RoleId, dialog.Result.TrangThai, CurrentUser.Id);
            MessageBox.Show(result.Message, result.Success ? "Thành công" : "Cảnh báo",
                MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            if (result.Success)
            {
                await LoadUsersAsync();
            }
        }

        private async Task DisableSelectedUserAsync()
        {
            var user = GetSelectedUser();
            if (user == null)
            {
                MessageBox.Show("Vui lòng chọn user cần disable.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Disable user '{user.Username}'?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            var result = await _service.DisableUserAsync(user.Id, CurrentUser.Id);
            MessageBox.Show(result.Message, result.Success ? "Thành công" : "Lỗi", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            if (result.Success)
            {
                await LoadUsersAsync();
            }
        }

        private async Task ResetPasswordAsync()
        {
            var user = GetSelectedUser();
            if (user == null)
            {
                MessageBox.Show("Vui lòng chọn user cần reset.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Reset password '{user.Username}' về mặc định 123456?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            var result = await _service.ResetPasswordAsync(user.Id, CurrentUser.Id);
            MessageBox.Show(result.Message, result.Success ? "Thành công" : "Lỗi", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
    }
}
