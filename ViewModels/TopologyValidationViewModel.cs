using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class TopologyValidationViewModel : BaseViewModel
    {
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
            set { _errorCount = value; OnPropertyChanged(); }
        }

        // ── Grouped Issue Collections ───────────────────────────────────

        public ObservableCollection<ValidationIssue> ErrorIssues { get; } = new();
        public ObservableCollection<ValidationIssue> WarningIssues { get; } = new();
        public ObservableCollection<ValidationIssue> InfoIssues { get; } = new();

        // ── Status ──────────────────────────────────────────────────────

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            set { _isValid = value; OnPropertyChanged(); }
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

        private bool _hasRun;
        public bool HasRun
        {
            get => _hasRun;
            set { _hasRun = value; OnPropertyChanged(); }
        }

        // ── Commands ────────────────────────────────────────────────────

        public ICommand ValidateCommand { get; }
        public ICommand RefreshCommand { get; }

        // ── Constructor ─────────────────────────────────────────────────

        public TopologyValidationViewModel()
        {
            ValidateCommand = new RelayCommand(_ => ExecuteValidation());
            RefreshCommand = new RelayCommand(_ => ExecuteValidation());

            // Auto-run validation on load
            ExecuteValidation();
        }

        // ── Validation Execution ────────────────────────────────────────

        private async void ExecuteValidation()
        {
            if (IsLoading) return;

            IsLoading = true;
            ValidationStatusText = "Đang kiểm tra...";

            try
            {
                var result = await Task.Run(() =>
                    TopologyValidationService.Instance.ValidateTopology());

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // Clear previous results
                    ErrorIssues.Clear();
                    WarningIssues.Clear();
                    InfoIssues.Clear();

                    // Populate grouped collections
                    foreach (var issue in result.Errors)
                        ErrorIssues.Add(issue);

                    foreach (var issue in result.Warnings)
                        WarningIssues.Add(issue);

                    foreach (var issue in result.Infos)
                        InfoIssues.Add(issue);

                    // Update counts
                    ErrorCount = result.Errors.Count;
                    WarningCount = result.Warnings.Count;
                    InfoCount = result.Infos.Count;

                    // Update status
                    IsValid = result.IsValid;
                    HasRun = true;

                    if (result.IsValid)
                    {
                        ValidationStatusText = "✓ Cấu hình hợp lệ";
                    }
                    else
                    {
                        ValidationStatusText = "⚠ Cần xử lý trước khi vận hành";
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ValidationStatusText = $"Kiểm tra thất bại: {ex.Message}";
                    HasRun = true;
                });
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
