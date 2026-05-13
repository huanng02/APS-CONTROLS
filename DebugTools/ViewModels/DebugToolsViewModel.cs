#if DEBUG
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QuanLyGiuXe.DebugTools.Models;
using QuanLyGiuXe.DebugTools.Services;
using QuanLyGiuXe.DebugTools.Simulations;
using QuanLyGiuXe.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuanLyGiuXe.DebugTools.ViewModels
{
    public class DebugToolsViewModel : INotifyPropertyChanged
    {
        private readonly ExceptionSimulationService _exceptionService = new ExceptionSimulationService();
        private readonly ReconnectSimulationService _reconnectService = new ReconnectSimulationService();
        private readonly BackupRestoreTestService _backupTestService = new BackupRestoreTestService();

        public ObservableCollection<TestResult> TestResults => QaTestService.Instance.Results;

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        private string _status = "Ready";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private int _progress;
        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        // Commands for Error Handling
        public ICommand UIExceptionCommand { get; }
        public ICommand BgExceptionCommand { get; }
        public ICommand AsyncExceptionCommand { get; }
        public ICommand NestedExceptionCommand { get; }
        public ICommand SpamExceptionCommand { get; }
        public ICommand FallbackLogCommand { get; }

        // Commands for Reconnect
        public ICommand DisconnectSqlCommand { get; }
        public ICommand ReconnectSqlCommand { get; }
        public ICommand NetworkFailureCommand { get; }

        // Commands for Backup/Restore
        public ICommand CreateBackupCommand { get; }
        public ICommand StressBackupCommand { get; }
        public ICommand CreateFakeBackupCommand { get; }

        public ICommand ClearResultsCommand { get; }

        public DebugToolsViewModel()
        {
            UIExceptionCommand = new RelayCommand(_ => _exceptionService.SimulateUIException());
            BgExceptionCommand = new RelayCommand(_ => _exceptionService.SimulateBackgroundException());
            AsyncExceptionCommand = new RelayCommand(async _ => await RunTest("Async Exception", "ErrorHandling", () => _exceptionService.SimulateAsyncException()));
            NestedExceptionCommand = new RelayCommand(_ => _exceptionService.SimulateNestedException());
            SpamExceptionCommand = new RelayCommand(_ => _exceptionService.SimulateSpamExceptions());
            FallbackLogCommand = new RelayCommand(_ => _exceptionService.TestFallbackLogging());

            DisconnectSqlCommand = new RelayCommand(_ => _reconnectService.SimulateSqlDisconnect());
            ReconnectSqlCommand = new RelayCommand(async _ => await _reconnectService.SimulateSqlReconnect());
            NetworkFailureCommand = new RelayCommand(async _ => await RunTest("Network Failure", "Reconnect", () => _reconnectService.SimulateNetworkFailureAsync()));

            CreateBackupCommand = new RelayCommand(async _ => await RunTest("Full Backup", "Backup", async () => await _backupTestService.RunBackupTestAsync()));
            StressBackupCommand = new RelayCommand(async _ => await RunTest("Stress Backup", "Backup", async () => await _backupTestService.StressTestBackupAsync()));
            CreateFakeBackupCommand = new RelayCommand(async _ => await RunTest("Fake Backup", "Backup", async () => await _backupTestService.CreateFakeBackupFileAsync()));

            ClearResultsCommand = new RelayCommand(_ => QaTestService.Instance.ClearResults());
        }

        private async Task RunTest(string name, string cat, Func<Task> action)
        {
            IsRunning = true;
            Status = $"Running: {name}...";
            Progress = 0;
            
            try
            {
                await QaTestService.Instance.RunTestAsync(name, cat, action);
                Progress = 100;
            }
            finally
            {
                IsRunning = false;
                Status = "Ready";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Basic RelayCommand implementation since we might not have a generic one accessible
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
#endif
