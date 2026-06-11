using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class DeploymentCenterViewModel : BaseViewModel
    {
        // ── Active Version Info ─────────────────────────────────────────

        private string _currentVersion = "V0";
        public string CurrentVersion
        {
            get => _currentVersion;
            set { _currentVersion = value; OnPropertyChanged(); }
        }

        private DateTime? _currentDeployTime;
        public DateTime? CurrentDeployTime
        {
            get => _currentDeployTime;
            set { _currentDeployTime = value; OnPropertyChanged(); }
        }

        private string _currentDeployUser = string.Empty;
        public string CurrentDeployUser
        {
            get => _currentDeployUser;
            set { _currentDeployUser = value; OnPropertyChanged(); }
        }

        // ── Status flags ──────────────────────────────────────────────

        private bool _hasPendingChanges;
        public bool HasPendingChanges
        {
            get => _hasPendingChanges;
            set 
            { 
                _hasPendingChanges = value; 
                OnPropertyChanged(); 
                UpdateCanDeploy();
            }
        }

        private bool _canDeploy;
        public bool CanDeploy
        {
            get => _canDeploy;
            set { _canDeploy = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _validationStatusText = "Chưa kiểm tra";
        public string ValidationStatusText
        {
            get => _validationStatusText;
            set { _validationStatusText = value; OnPropertyChanged(); }
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        // ── Pending Changes Detail Counts ───────────────────────────────

        private int _pendingSitesCount;
        public int PendingSitesCount
        {
            get => _pendingSitesCount;
            set { _pendingSitesCount = value; OnPropertyChanged(); }
        }

        private int _pendingZonesCount;
        public int PendingZonesCount
        {
            get => _pendingZonesCount;
            set { _pendingZonesCount = value; OnPropertyChanged(); }
        }

        private int _pendingGatesCount;
        public int PendingGatesCount
        {
            get => _pendingGatesCount;
            set { _pendingGatesCount = value; OnPropertyChanged(); }
        }

        private int _pendingLanesCount;
        public int PendingLanesCount
        {
            get => _pendingLanesCount;
            set { _pendingLanesCount = value; OnPropertyChanged(); }
        }

        private int _pendingReadersCount;
        public int PendingReadersCount
        {
            get => _pendingReadersCount;
            set { _pendingReadersCount = value; OnPropertyChanged(); }
        }

        private int _pendingControllersCount;
        public int PendingControllersCount
        {
            get => _pendingControllersCount;
            set { _pendingControllersCount = value; OnPropertyChanged(); }
        }

        // ── Summary Counts ──────────────────────────────────────────────

        private int _infoCount;
        public int InfoCount
        {
            get => _infoCount;
            set { _infoCount = value; OnPropertyChanged(); }
        }

        private int _warningCount;
        public int WarningCount
        {
            get => _warningCount;
            set { _warningCount = value; OnPropertyChanged(); }
        }

        private int _errorCount;
        public int ErrorCount
        {
            get => _errorCount;
            set 
            { 
                _errorCount = value; 
                OnPropertyChanged(); 
                UpdateCanDeploy();
            }
        }

        private bool _isValid = true;
        public bool IsValid
        {
            get => _isValid;
            set { _isValid = value; OnPropertyChanged(); }
        }

        private DeploymentRecord? _selectedDeployment;
        public DeploymentRecord? SelectedDeployment
        {
            get => _selectedDeployment;
            set 
            { 
                _selectedDeployment = value; 
                OnPropertyChanged(); 
                if (value != null)
                {
                    ShowDeploymentChanges(value);
                }
            }
        }

        private void ShowDeploymentChanges(DeploymentRecord record)
        {
            try
            {
                var win = new Views.DeploymentChangesWindow(record);
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentCenterViewModel", "ShowDeploymentChanges", "Failed to open changes dialog", ex);
            }
            finally
            {
                _selectedDeployment = null;
                OnPropertyChanged(nameof(SelectedDeployment));
            }
        }

        // ── Collections ─────────────────────────────────────────────────

        public ObservableCollection<ValidationIssue> ErrorIssues { get; } = new();
        public ObservableCollection<ValidationIssue> WarningIssues { get; } = new();
        public ObservableCollection<ValidationIssue> InfoIssues { get; } = new();
        public ObservableCollection<DeploymentRecord> DeploymentHistory { get; } = new();
        public ObservableCollection<ConfigurationAuditRecord> AuditHistory { get; } = new();

        // ── Commands ────────────────────────────────────────────────────

        public ICommand ValidateCommand { get; }
        public ICommand DeployCommand { get; }
        public ICommand RefreshCommand { get; }

        // ── Constructor ─────────────────────────────────────────────────

        public DeploymentCenterViewModel()
        {
            ValidateCommand = new RelayCommand(_ => ExecuteValidation());
            DeployCommand = new RelayCommand(_ => ExecuteDeploy(), _ => CanDeploy);
            RefreshCommand = new RelayCommand(_ => LoadAllData());

            LoadAllData();
        }

        // ── Data Loading & State Management ──────────────────────────────

        private async void LoadAllData()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                // 1. Get version state & pending status
                var version = await DeploymentService.Instance.GetCurrentVersionAsync();
                var pending = await DeploymentService.Instance.HasPendingChangesAsync();
                var lastDeploy = await DeploymentService.Instance.GetLastDeploymentAsync();

                // 2. Load lists
                var historyList = await DeploymentService.Instance.GetDeploymentHistoryAsync();
                var auditList = await ConfigurationAuditService.Instance.GetChangesSinceAsync(lastDeploy?.DeployTime);

                // 3. Load counts of pending changes since last successful deployment
                var pendingCounts = await ConfigurationAuditService.Instance.GetPendingChangesCountsAsync(lastDeploy?.DeployTime);

                // Run validation
                var validationResult = await Task.Run(() => DeploymentService.Instance.ValidateDraft());

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CurrentVersion = version;
                    HasPendingChanges = pending;
                    if (lastDeploy != null)
                    {
                        CurrentDeployTime = lastDeploy.DeployTime;
                        CurrentDeployUser = lastDeploy.DeployBy;
                    }
                    else
                    {
                        CurrentDeployTime = null;
                        CurrentDeployUser = "N/A";
                    }

                    // Populate collections
                    DeploymentHistory.Clear();
                    foreach (var h in historyList) DeploymentHistory.Add(h);

                    AuditHistory.Clear();
                    foreach (var a in auditList) AuditHistory.Add(a);

                    // Update pending details counts
                    PendingSitesCount = pendingCounts.ContainsKey("Site") ? pendingCounts["Site"] : 0;
                    PendingZonesCount = pendingCounts.ContainsKey("Zone") ? pendingCounts["Zone"] : 0;
                    PendingGatesCount = pendingCounts.ContainsKey("Gate") ? pendingCounts["Gate"] : 0;
                    PendingLanesCount = pendingCounts.ContainsKey("Lane") ? pendingCounts["Lane"] : 0;
                    PendingReadersCount = pendingCounts.ContainsKey("Reader Mapping") ? pendingCounts["Reader Mapping"] : 0;
                    PendingControllersCount = pendingCounts.ContainsKey("Controller") ? pendingCounts["Controller"] : 0;

                    // Process validation issues
                    ErrorIssues.Clear();
                    WarningIssues.Clear();
                    InfoIssues.Clear();

                    foreach (var issue in validationResult.Errors) ErrorIssues.Add(issue);
                    foreach (var issue in validationResult.Warnings) WarningIssues.Add(issue);
                    foreach (var issue in validationResult.Infos) InfoIssues.Add(issue);

                    ErrorCount = validationResult.Errors.Count;
                    WarningCount = validationResult.Warnings.Count;
                    InfoCount = validationResult.Infos.Count;
                    IsValid = validationResult.IsValid;

                    if (!IsValid)
                    {
                        ValidationStatusText = "⚠ Không hợp lệ (Có lỗi nghiêm trọng)";
                    }
                    else if (HasPendingChanges)
                    {
                        ValidationStatusText = "⚠ Chưa triển khai cấu hình mới";
                    }
                    else
                    {
                        ValidationStatusText = "✓ Hợp lệ và Đã triển khai";
                    }

                    UpdateCanDeploy();
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentCenterViewModel", "LoadAllData", "Error loading Deployment Center data", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void ExecuteValidation()
        {
            IsLoading = true;
            ValidationStatusText = "Đang kiểm tra...";

            try
            {
                var validationResult = await Task.Run(() => DeploymentService.Instance.ValidateDraft());

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ErrorIssues.Clear();
                    WarningIssues.Clear();
                    InfoIssues.Clear();

                    foreach (var issue in validationResult.Errors) ErrorIssues.Add(issue);
                    foreach (var issue in validationResult.Warnings) WarningIssues.Add(issue);
                    foreach (var issue in validationResult.Infos) InfoIssues.Add(issue);

                    ErrorCount = validationResult.Errors.Count;
                    WarningCount = validationResult.Warnings.Count;
                    InfoCount = validationResult.Infos.Count;
                    IsValid = validationResult.IsValid;

                    if (!IsValid)
                    {
                        ValidationStatusText = "⚠ Không hợp lệ (Có lỗi nghiêm trọng)";
                    }
                    else if (HasPendingChanges)
                    {
                        ValidationStatusText = "⚠ Chưa triển khai cấu hình mới";
                    }
                    else
                    {
                        ValidationStatusText = "✓ Hợp lệ và Đã triển khai";
                    }

                    UpdateCanDeploy();
                });
            }
            catch (Exception ex)
            {
                ValidationStatusText = $"Kiểm tra thất bại: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void ExecuteDeploy()
        {
            if (!CanDeploy) return;

            IsLoading = true;
            string deployer = CurrentUser.Username ?? "Admin";
            bool success = false;

            try
            {
                success = await DeploymentService.Instance.DeployAsync(deployer, Notes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi trong quá trình triển khai: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }

            if (success)
            {
                // Apply deployed configuration: sync config.json, diff old vs new, stop/start affected streams
                await CameraService.Instance.ApplyDeployedConfigurationAsync();

                MessageBox.Show("Triển khai cấu hình mới thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Notes = string.Empty;
                LoadAllData();
            }
            else
            {
                MessageBox.Show("Triển khai thất bại. Vui lòng kiểm tra lỗi validation.", "Thông báo lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadAllData();
            }
        }

        private void UpdateCanDeploy()
        {
            CanDeploy = HasPendingChanges && ErrorCount == 0;
        }
    }
}
