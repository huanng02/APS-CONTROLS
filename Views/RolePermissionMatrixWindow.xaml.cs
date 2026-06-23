using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class RolePermissionMatrixWindow : Window
    {
        private readonly PermissionMatrixViewModel _viewModel = new();
        private bool _isInitialized = false;

        public RolePermissionMatrixWindow()
        {
            InitializeComponent();
            Loaded += RolePermissionMatrixWindow_Loaded;
        }

        private async void RolePermissionMatrixWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            _isInitialized = true;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                this.Cursor = Cursors.Wait;
                LblStatus.Text = "Đang tải Roles & Permissions...";

                await _viewModel.LoadDataAsync();

                SetupGridColumns();
                PopulateGridRows();
                UpdateStatusLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải ma trận phân quyền: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                LblStatus.Text = "Lỗi tải dữ liệu.";
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void SetupGridColumns()
        {
            // Remove existing dynamic role columns (keep the first 4 fixed columns)
            while (GridMatrix.Columns.Count > 4)
            {
                GridMatrix.Columns.RemoveAt(4);
            }

            string currentRole = CurrentUserContext.Instance.Role;
            int currentLevel = PermissionMatrixService.GetRoleLevel(currentRole);

            // Dynamically generate role columns only for roles the user is permitted to edit
            foreach (var role in _viewModel.Roles)
            {
                int targetLevel = PermissionMatrixService.GetRoleLevel(role.Name);
                if (targetLevel >= currentLevel)
                {
                    continue; // Skip roles with equal/higher level or own role
                }

                var colRole = new DataGridCheckBoxColumn
                {
                    Header = role.Name,
                    Binding = new Binding($"[{role.Id}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                    Width = 110,
                    IsReadOnly = false
                };

                GridMatrix.Columns.Add(colRole);
            }
        }

        private void PopulateGridRows()
        {
            var searchText = TxtSearch.Text.Trim();
            var filteredPerms = _viewModel.Permissions;

            if (!string.IsNullOrEmpty(searchText))
            {
                filteredPerms = _viewModel.Permissions.Where(p =>
                    p.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Module.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            var rows = filteredPerms.Select(p => new PermissionMatrixRow(p, _viewModel, UpdateStatusLabel)).ToList();
            GridMatrix.ItemsSource = rows;
        }

        private void UpdateStatusLabel()
        {
            int changesCount = _viewModel.GetChangeCount();
            if (changesCount > 0)
            {
                LblStatus.Text = $"Có {changesCount} thay đổi chưa lưu.";
                LblStatus.FontWeight = FontWeights.Bold;
                BtnSave.IsEnabled = true;
            }
            else
            {
                LblStatus.Text = $"Hiển thị {GridMatrix.Items.Count} quyền hạn hệ thống.";
                LblStatus.FontWeight = FontWeights.Normal;
                BtnSave.IsEnabled = false;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            PopulateGridRows();
            UpdateStatusLabel();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var changes = _viewModel.GetPendingChanges();
                if (!changes.Any())
                {
                    MessageBox.Show("Không có thay đổi nào cần lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirmResult = MessageBox.Show(
                    $"Bạn có chắc chắn muốn lưu {changes.Count} thay đổi phân quyền này không?",
                    "Xác nhận lưu thay đổi",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.Yes) return;

                this.Cursor = Cursors.Wait;
                LblStatus.Text = "Đang lưu thay đổi vào cơ sở dữ liệu...";

                // Execute save via ViewModel
                await _viewModel.SaveChangesAsync();

                // Reload logged-in user permissions from DB (since role permissions have changed)
                await PermissionService.Instance.RefreshCurrentUserPermissionsAsync();

                // Re-evaluate WPF MainWindow menu/button visibility based on updated permissions
                if (Application.Current?.MainWindow is QuanLyGiuXe.MainWindow mainWin)
                {
                    mainWin.Dispatcher.Invoke(() => mainWin.ApplyPermissions());
                }

                MessageBox.Show("Đã lưu tất cả thay đổi phân quyền thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reload Matrix from the newly synchronized state
                SetupGridColumns();
                PopulateGridRows();
                UpdateStatusLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu thay đổi phân quyền: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                LblStatus.Text = "Lưu thay đổi thất bại.";
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class PermissionMatrixRow
    {
        public PermissionMatrixItem Permission { get; }
        private readonly PermissionMatrixViewModel _viewModel;
        private readonly Action _onValueChanged;

        public PermissionMatrixRow(PermissionMatrixItem permission, PermissionMatrixViewModel viewModel, Action onValueChanged)
        {
            Permission = permission;
            _viewModel = viewModel;
            _onValueChanged = onValueChanged;
        }

        public string Module => Permission.Module;
        public string Code => Permission.Code;
        public string Name => Permission.Name;
        public string Description => Permission.Description;

        // Indexer for checkbox binding
        public bool this[int roleId]
        {
            get => _viewModel.GetCurrentValue(roleId, Permission.Id);
            set
            {
                var role = _viewModel.Roles.FirstOrDefault(r => r.Id == roleId);
                if (role != null)
                {
                    _viewModel.TogglePermission(roleId, role.Name, Permission.Id, Permission.Code, value);
                    _onValueChanged?.Invoke();
                }
            }
        }
    }
}
