using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RealtimeLogViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Circular buffer limit
        private const int MAX_LOGS = 1000;

        // Thread-safe lock object
        private readonly object _lockObj = new();

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        // ── View for Filtering / Sorting ──
        public ICollectionView LogView { get; }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set { _isPaused = value; OnPropertyChanged(nameof(IsPaused)); }
        }

        private bool _autoScroll = true;
        public bool AutoScroll
        {
            get => _autoScroll;
            set { _autoScroll = value; OnPropertyChanged(nameof(AutoScroll)); }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set 
            { 
                _searchText = value; 
                OnPropertyChanged(nameof(SearchText)); 
                DebounceSearch();
            }
        }

        private string _selectedLevel = "All";
        public string SelectedLevel
        {
            get => _selectedLevel;
            set 
            { 
                _selectedLevel = value; 
                OnPropertyChanged(nameof(SelectedLevel)); 
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => LogView.Refresh()));
            }
        }

        public ObservableCollection<string> Levels { get; } = new() { "All", "Info", "Warning", "Error", "Security", "Audit" };

        // Commands
        public ICommand ClearLogCommand { get; }
        public ICommand PauseLogCommand { get; }
        public ICommand ToggleAutoScrollCommand { get; }
        public ICommand ExportLogCommand { get; }

        private CancellationTokenSource? _searchCts;

        public RealtimeLogViewModel()
        {
            // Enable collection synchronization for thread-safe UI updates
            BindingOperations.EnableCollectionSynchronization(LogEntries, _lockObj);

            LogView = CollectionViewSource.GetDefaultView(LogEntries);
            LogView.Filter = FilterLog;

            ClearLogCommand = new RelayCommand(_ => {
                lock (_lockObj) LogEntries.Clear();
            });
            
            PauseLogCommand = new RelayCommand(_ => IsPaused = !IsPaused);
            ToggleAutoScrollCommand = new RelayCommand(_ => AutoScroll = !AutoScroll);
            ExportLogCommand = new RelayCommand(_ => ExportLogs());

            // Load initial logs
            LoadInitialLogs();

            // Subscribe to new logs
            LoggingService.Instance.LogEmitted += OnLogEmitted;
        }

        private void LoadInitialLogs()
        {
            Task.Run(() =>
            {
                try
                {
                    var db = new DatabaseService();
                    var logs = db.GetAppLogs(null, null, 200).OrderBy(x => x.Timestamp); // Load last 200 logs
                    
                    lock (_lockObj)
                    {
                        foreach (var log in logs)
                        {
                            log.Timestamp = log.Timestamp.ToLocalTime();
                            LogEntries.Insert(0, log); // Insert at top since UI typically expects newest first
                        }
                    }
                }
                catch { }
            });
        }

        private void OnLogEmitted(LogEntry entry)
        {
            if (IsPaused) return;

            // Deep copy to prevent cross-thread issues if entry is mutated
            var newEntry = new LogEntry
            {
                Timestamp = entry.Timestamp.ToLocalTime(),
                Level = entry.Level,
                EventType = entry.EventType,
                Source = entry.Source,
                UserId = entry.UserId,
                Username = entry.Username,
                Plate = entry.Plate,
                Details = entry.Details,
                Exception = entry.Exception,
                MachineName = entry.MachineName,
                IpAddress = entry.IpAddress,
                CorrelationId = entry.CorrelationId,
                Action = entry.Action,
                OldValues = entry.OldValues,
                NewValues = entry.NewValues
            };

            lock (_lockObj)
            {
                LogEntries.Insert(0, newEntry);
                
                // Circular buffer logic
                while (LogEntries.Count > MAX_LOGS)
                {
                    LogEntries.RemoveAt(LogEntries.Count - 1);
                }
            }
        }

        private bool FilterLog(object obj)
        {
            if (obj is not LogEntry log) return false;

            // Level filter
            if (SelectedLevel != "All" && !string.Equals(log.Level, SelectedLevel, StringComparison.OrdinalIgnoreCase))
            {
                // Handle alias "Security"/"Audit" as they might map to different strings or events
                // In our codebase: Level "Security" is custom mapped in UI, but LoggingService uses LogSecurity (usually Level=Security/Warning)
                // Let's do simple string match first
                if (SelectedLevel == "Security" && !string.Equals(log.Level, "Security", StringComparison.OrdinalIgnoreCase)) return false;
                if (SelectedLevel == "Audit" && !string.Equals(log.Level, "Audit", StringComparison.OrdinalIgnoreCase)) return false;
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLower();
                return (log.Details?.ToLower().Contains(term) == true) ||
                       (log.Source?.ToLower().Contains(term) == true) ||
                       (log.EventType?.ToLower().Contains(term) == true) ||
                       (log.Plate?.ToLower().Contains(term) == true);
            }

            return true;
        }

        private void DebounceSearch()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Delay(300, token).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() => LogView.Refresh()));
                }
            }, TaskScheduler.Default);
        }

        private void ExportLogs()
        {
            // TODO: Implement ClosedXML export for logs
            MessageBox.Show("Tính năng Export Excel đang được cập nhật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Dispose()
        {
            LoggingService.Instance.LogEmitted -= OnLogEmitted;
            _searchCts?.Cancel();
            _searchCts?.Dispose();
        }
    }
}
