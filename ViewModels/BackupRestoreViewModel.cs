using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Services.Backup;

namespace QuanLyGiuXe.ViewModels
{
    public class BackupRestoreViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<BackupFile> _backupFiles;
        public ObservableCollection<BackupFile> BackupFiles
        {
            get => _backupFiles;
            set { _backupFiles = value; OnPropertyChanged(nameof(BackupFiles)); }
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set { _isProcessing = value; OnPropertyChanged(nameof(IsProcessing)); }
        }

        private string _statusMessage = "Sẵn sàng.";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(nameof(ProgressValue)); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand BackupNowCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand VerifyCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        private bool _autoBackupEnabled;
        public bool AutoBackupEnabled
        {
            get => _autoBackupEnabled;
            set { _autoBackupEnabled = value; OnPropertyChanged(nameof(AutoBackupEnabled)); }
        }

        private string _backupTime;
        public string BackupTime
        {
            get => _backupTime;
            set { _backupTime = value; OnPropertyChanged(nameof(BackupTime)); }
        }

        private int _retentionDays;
        public int RetentionDays
        {
            get => _retentionDays;
            set { _retentionDays = value; OnPropertyChanged(nameof(RetentionDays)); }
        }

        public string CurrentBackupPath => DatabaseBackupService.Instance.GetCurrentBackupDirectory();

        public BackupRestoreViewModel()
        {
            BackupFiles = new ObservableCollection<BackupFile>();
            
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
            BackupNowCommand = new RelayCommand(async _ => await BackupNowAsync(), _ => !IsProcessing);
            RestoreCommand = new RelayCommand<BackupFile>(async file => await RestoreAsync(file), _ => !IsProcessing);
            VerifyCommand = new RelayCommand<BackupFile>(async file => await VerifyAsync(file), _ => !IsProcessing);
            DeleteCommand = new RelayCommand<BackupFile>(async file => await DeleteAsync(file), _ => !IsProcessing);
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings(), _ => !IsProcessing);
            
            // Subscribe to progress
            DatabaseBackupService.Instance.ProgressChanged += (pct) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ProgressValue = pct;
                    StatusMessage = $"Đang xử lý: {pct}%...";
                });
            };

            // Load settings
            var config = AppConfig.Load().Backup;
            AutoBackupEnabled = config.AutoBackupEnabled;
            BackupTime = config.BackupTime;
            RetentionDays = config.RetentionDays;

            // Load init
            _ = LoadDataAsync();
        }

        private void SaveSettings()
        {
            try
            {
                var config = AppConfig.Load();
                config.Backup.AutoBackupEnabled = AutoBackupEnabled;
                config.Backup.BackupTime = BackupTime;
                config.Backup.RetentionDays = RetentionDays;
                config.Save();

                ToastNotificationService.Instance.ShowToast("Đã lưu cấu hình sao lưu!", ToastType.Success);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi lưu cấu hình: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "Đang tải danh sách bản sao lưu...";
                var files = await DatabaseBackupService.Instance.GetBackupFilesAsync();
                
                var dispatcher = Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
                
                dispatcher.Invoke(() =>
                {
                    BackupFiles.Clear();
                    foreach (var f in files)
                        BackupFiles.Add(f);
                });
                
                if (files.Count == 0)
                {
                    string path = DatabaseBackupService.Instance.GetCurrentBackupDirectory();
                    StatusMessage = $"Không tìm thấy file nào tại: {path}";
                }
                else
                {
                    StatusMessage = $"Tìm thấy {files.Count} bản sao lưu. Sẵn sàng.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Lỗi tải dữ liệu.";
                MessageBox.Show("Không thể tải danh sách backup: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task BackupNowAsync()
        {
            try
            {
                IsProcessing = true;
                ProgressValue = 0;
                StatusMessage = "Bắt đầu tạo bản sao lưu...";
                
                await DatabaseBackupService.Instance.BackupNowAsync();
                
                ToastNotificationService.Instance.ShowToast("Tạo bản sao lưu thành công!", ToastType.Success);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi Backup", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Lỗi khi sao lưu.";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task RestoreAsync(BackupFile file)
        {
            if (file == null) return;

            var result = MessageBox.Show(
                $"CẢNH BÁO: Phục hồi dữ liệu sẽ xóa các thay đổi mới nhất kể từ ngày {file.CreatedAtDisplay}.\n" +
                $"Quá trình này sẽ ngắt kết nối hệ thống trong ít phút.\n\n" +
                $"Bạn có CHẮC CHẮN muốn phục hồi dữ liệu từ bản sao lưu này không?",
                "Xác nhận phục hồi", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Đang phục hồi dữ liệu, không tắt ứng dụng...";
                
                bool success = await RestoreService.Instance.RestoreDatabaseAsync(file.FilePath);
                
                if (success)
                {
                    ToastNotificationService.Instance.ShowToast("Phục hồi dữ liệu thành công!", ToastType.Success);
                    StatusMessage = "Phục hồi thành công. Vui lòng kết nối lại.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi Phục Hồi", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Lỗi khi phục hồi.";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task VerifyAsync(BackupFile file)
        {
            if (file == null) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Đang kiểm tra tính toàn vẹn của file...";
                
                bool isValid = await DatabaseBackupService.Instance.VerifyBackupAsync(file.FilePath);
                
                if (isValid)
                {
                    MessageBox.Show("File sao lưu hợp lệ và có thể sử dụng để phục hồi!", "Kết quả kiểm tra", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusMessage = "File hợp lệ.";
                }
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                MessageBox.Show("Kết quả kiểm tra: " + errorMsg, "Lỗi kiểm tra", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = "Kiểm tra thất bại.";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task DeleteAsync(BackupFile file)
        {
            if (file == null) return;

            var result = MessageBox.Show($"Bạn có muốn xóa vĩnh viễn bản sao lưu '{file.FileName}'?", "Xóa backup", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    IsProcessing = true;
                    await DatabaseBackupService.Instance.DeleteBackupAsync(file.FilePath);
                    ToastNotificationService.Instance.ShowToast("Đã xóa file backup.", ToastType.Success);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể xóa file: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsProcessing = false;
                }
            }
        }
    }
}
