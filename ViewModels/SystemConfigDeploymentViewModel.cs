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
    public class SystemConfigDeploymentViewModel : BaseViewModel
    {
        // ── Topology View Model ─────────────────────────────────────────
        public ParkingTopologyViewModel TopologyViewModel { get; } = new ParkingTopologyViewModel();

        // ── Active Tab Index ───────────────────────────────────────────
        private int _activeTabIndex;
        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (_activeTabIndex != value)
                {
                    _activeTabIndex = value;
                    OnPropertyChanged();
                    if (_activeTabIndex == 2) // Development Center is now tab index 2
                    {
                        LoadAllData();
                    }
                }
            }
        }

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

        private int _pendingCamerasCount;
        public int PendingCamerasCount
        {
            get => _pendingCamerasCount;
            set { _pendingCamerasCount = value; OnPropertyChanged(); }
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
                LoggingService.Instance.LogError("SystemConfigDeploymentViewModel", "ShowDeploymentChanges", "Failed to open changes dialog", ex);
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

        public SystemConfigDeploymentViewModel()
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
                    PendingCamerasCount = pendingCounts.ContainsKey("Camera") ? pendingCounts["Camera"] : 0;

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
                LoggingService.Instance.LogError("SystemConfigDeploymentViewModel", "LoadAllData", "Error loading data", ex);
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
                // 1. Clear cached active configs and load the newly copied files
                AppConfig.ClearCache();
                ReaderLaneMappingService.Instance.Load();

                // 2. Configure and reconnect ZKTeco controller service with new active settings
                try
                {
                    var activeCfg = AppConfig.Load();
                    C3200Service.Instance.Configure(
                        activeCfg.ZKTeco.IpAddress, 
                        activeCfg.ZKTeco.TcpPort,
                        activeCfg.ZKTeco.Password, 
                        activeCfg.ZKTeco.Timeout, 
                        activeCfg.ZKTeco.BarrierDuration
                    );
                    ConnectionMonitorService.Instance.ResetState();
                    _ = Task.Run(async () =>
                    {
                        try { await C3200Service.Instance.ConnectAsync(); } catch { }
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("ExecuteDeploy", "SystemConfigDeploymentViewModel", "Failed to configure/reconnect C3200 after deployment", ex);
                }

                // 3. Apply deployed camera configuration: sync config.json, diff old vs new, stop/start affected streams
                await CameraService.Instance.ApplyDeployedConfigurationAsync();

                // 4. Refresh main view model settings immediately so UI is updated in real-time
                try
                {
                    if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
                    {
                        vm.RefreshSettings();
                    }
                }
                catch { }

                // 5. Reload camera streams dynamically
                try
                {
                    var mainWin = Application.Current.MainWindow as MainWindow;
                    if (mainWin != null)
                    {
                        mainWin.ReloadCameras();
                    }
                }
                catch { }

                MessageBox.Show("Triển khai cấu hình mới thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Notes = string.Empty;
                LoadAllData();
                ActiveTabIndex = 0;
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
