using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class AuditHistoryWindow : Window
    {
        private readonly AuditLogService _service = AuditLogService.Instance;
        private int _currentPage = 1;
        private const int PageSize = 25;
        private int _totalPages = 1;
        private int _totalItems = 0;
        private bool _isInitialized = false;

        public AuditHistoryWindow()
        {
            // Security Enforcement: Block unauthorized constructor execution
            AuthorizationGuard.Protect("VIEW_AUDIT_LOG", "Access Audit History Screen");

            InitializeComponent();
            Loaded += AuditHistoryWindow_Loaded;
        }

        private async void AuditHistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeDropdowns();
            await LoadAuditLogsAsync();
            _isInitialized = true;
        }

        private void InitializeDropdowns()
        {
            CboActionType.Items.Clear();
            CboActionType.Items.Add("Tất cả");
            CboActionType.Items.Add("GRANT_PERMISSION");
            CboActionType.Items.Add("REVOKE_PERMISSION");
            CboActionType.SelectedIndex = 0;

            CboEntityType.Items.Clear();
            CboEntityType.Items.Add("Tất cả");
            CboEntityType.Items.Add("RolePermission");
            CboEntityType.Items.Add("User");
            CboEntityType.Items.Add("Role");
            CboEntityType.SelectedIndex = 0;

            DpFrom.SelectedDate = DateTime.Today.AddDays(-7);
            DpTo.SelectedDate = DateTime.Now;
        }

        private async Task LoadAuditLogsAsync()
        {
            try
            {
                this.Cursor = Cursors.Wait;
                BtnSearch.IsEnabled = false;

                string? actionFilter = CboActionType.SelectedItem?.ToString();
                if (actionFilter == "Tất cả") actionFilter = "All";

                string? entityFilter = CboEntityType.SelectedItem?.ToString();
                if (entityFilter == "Tất cả") entityFilter = "All";

                DateTime from = DpFrom.SelectedDate ?? DateTime.Today.AddDays(-7);
                DateTime to = DpTo.SelectedDate ?? DateTime.Now;
                
                // Set to end of day for To Date to capture logs up to 23:59:59
                to = to.Date.AddDays(1).AddSeconds(-1);

                string search = TxtSearch.Text.Trim();

                var (items, totalCount) = await _service.GetAuditLogsPagedAsync(
                    _currentPage, PageSize, null, actionFilter, entityFilter, from, to, search
                );

                _totalItems = totalCount;
                _totalPages = (int)Math.Ceiling((double)_totalItems / PageSize);
                if (_totalPages < 1) _totalPages = 1;

                GridLogs.ItemsSource = items;

                // Sync navigation button states
                BtnFirst.IsEnabled = _currentPage > 1;
                BtnPrev.IsEnabled = _currentPage > 1;
                BtnNext.IsEnabled = _currentPage < _totalPages;
                BtnLast.IsEnabled = _currentPage < _totalPages;
                LblPageStatus.Text = $"Trang {_currentPage} / {_totalPages} (Tổng số: {_totalItems} dòng)";
            }
            catch (SecurityException secEx)
            {
                MessageBox.Show("Không có quyền xem: " + secEx.Message, "Lỗi phân quyền", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("AUDIT_GRID_LOAD_FAILED", "AuditHistoryWindow", "Failed to fetch paginated logs", ex);
                MessageBox.Show("Lỗi tải lịch sử audit: " + ex.Message, "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSearch.IsEnabled = true;
                this.Cursor = Cursors.Arrow;
            }
        }

        private void GridLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var log = GridLogs.SelectedItem as AuditLog;
            if (log == null)
            {
                ClearDetails();
                return;
            }

            TxtDetailId.Text = log.AuditLogId.ToString();
            TxtDetailTime.Text = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            TxtDetailUser.Text = log.Username;
            TxtDetailAction.Text = log.ActionType;
            TxtDetailEntity.Text = log.EntityType;
            TxtDetailDesc.Text = log.Description;

            TxtDetailOldValue.Text = PrettyPrintJson(log.OldValue);
            TxtDetailNewValue.Text = PrettyPrintJson(log.NewValue);
        }

        private void ClearDetails()
        {
            TxtDetailId.Text = string.Empty;
            TxtDetailTime.Text = string.Empty;
            TxtDetailUser.Text = string.Empty;
            TxtDetailAction.Text = string.Empty;
            TxtDetailEntity.Text = string.Empty;
            TxtDetailDesc.Text = string.Empty;
            TxtDetailOldValue.Text = string.Empty;
            TxtDetailNewValue.Text = string.Empty;
        }

        private string PrettyPrintJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    return System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                }
            }
            catch
            {
                return json;
            }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            await LoadAuditLogsAsync();
        }

        private async void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            CboActionType.SelectedIndex = 0;
            CboEntityType.SelectedIndex = 0;
            DpFrom.SelectedDate = DateTime.Today.AddDays(-7);
            DpTo.SelectedDate = DateTime.Now;
            TxtSearch.Text = string.Empty;
            _currentPage = 1;
            await LoadAuditLogsAsync();
        }

        private async void BtnFirst_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage = 1;
                await LoadAuditLogsAsync();
            }
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadAuditLogsAsync();
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                await LoadAuditLogsAsync();
            }
        }

        private async void BtnLast_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage = _totalPages;
                await LoadAuditLogsAsync();
            }
        }
    }
}
