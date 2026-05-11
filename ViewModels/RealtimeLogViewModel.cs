using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RealtimeLogViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private const int MAX_LOGS = 1000;
        private readonly object _lockObj = new();
        
        // Internal full collection
        private readonly List<LogEntry> _fullLogEntries = new();
        
        // Collection shown in DataGrid (Paged)
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

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
                CurrentPage = 1;
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
                CurrentPage = 1;
                RefreshPagedLogs();
            }
        }

        public ObservableCollection<string> Levels { get; } = new() { "All", "Info", "Warning", "Error", "Security", "Audit" };

        // ── Paging Properties ──
        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(nameof(PageSize)); CurrentPage = 1; RefreshPagedLogs(); }
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(nameof(CurrentPage)); RefreshPagedLogs(); }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(nameof(TotalPages)); }
        }

        private int _totalItems = 0;
        public int TotalItems
        {
            get => _totalItems;
            set { _totalItems = value; OnPropertyChanged(nameof(TotalItems)); }
        }

        // Commands
        public ICommand ClearLogCommand { get; }
        public ICommand PauseLogCommand { get; }
        public ICommand ToggleAutoScrollCommand { get; }
        public ICommand ExportLogCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }

        private CancellationTokenSource? _searchCts;

        public RealtimeLogViewModel()
        {
            ClearLogCommand = new RelayCommand(_ => {
                lock (_lockObj) { _fullLogEntries.Clear(); }
                RefreshPagedLogs();
            });
            
            PauseLogCommand = new RelayCommand(_ => IsPaused = !IsPaused);
            ToggleAutoScrollCommand = new RelayCommand(_ => AutoScroll = !AutoScroll);
            ExportLogCommand = new RelayCommand(_ => ExportLogs());
            
            NextPageCommand = new RelayCommand(_ => { if (CurrentPage < TotalPages) CurrentPage++; });
            PrevPageCommand = new RelayCommand(_ => { if (CurrentPage > 1) CurrentPage--; });

            LoadInitialLogs();
            LoggingService.Instance.LogEmitted += OnLogEmitted;
        }

        private void LoadInitialLogs()
        {
            Task.Run(() =>
            {
                try
                {
                    var db = new DatabaseService();
                    var logs = db.GetAppLogs(null, null, 500).OrderByDescending(x => x.Timestamp);
                    
                    lock (_lockObj)
                    {
                        _fullLogEntries.Clear();
                        foreach (var log in logs)
                        {
                            log.Timestamp = log.Timestamp.ToLocalTime();
                            _fullLogEntries.Add(log);
                        }
                    }
                    RefreshPagedLogs();
                }
                catch { }
            });
        }

        private void OnLogEmitted(LogEntry entry)
        {
            if (IsPaused) return;

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
                // New logs always at top (Index 0)
                _fullLogEntries.Insert(0, newEntry);
                
                if (_fullLogEntries.Count > MAX_LOGS)
                {
                    _fullLogEntries.RemoveAt(_fullLogEntries.Count - 1);
                }
            }

            // If we are on page 1, we should update the UI
            if (CurrentPage == 1)
            {
                RefreshPagedLogs();
            }
            else
            {
                // Just update totals if on other pages
                UpdateTotals();
            }
        }

        private void RefreshPagedLogs()
        {
            List<LogEntry> filtered;
            lock (_lockObj)
            {
                var query = _fullLogEntries.AsEnumerable();

                // Filter
                if (SelectedLevel != "All")
                {
                    query = query.Where(x => string.Equals(x.Level, SelectedLevel, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var term = SearchText.ToLower();
                    query = query.Where(log => 
                        (log.Details?.ToLower().Contains(term) == true) ||
                        (log.Source?.ToLower().Contains(term) == true) ||
                        (log.EventType?.ToLower().Contains(term) == true) ||
                        (log.Plate?.ToLower().Contains(term) == true));
                }

                filtered = query.ToList();
            }

            int total = filtered.Count;
            int pages = (int)Math.Ceiling((double)total / PageSize);
            if (pages == 0) pages = 1;

            var pageItems = filtered
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                TotalItems = total;
                TotalPages = pages;
                
                LogEntries.Clear();
                foreach (var item in pageItems)
                {
                    LogEntries.Add(item);
                }
            }));
        }

        private void UpdateTotals()
        {
            lock (_lockObj)
            {
                var query = _fullLogEntries.AsEnumerable();
                if (SelectedLevel != "All") query = query.Where(x => string.Equals(x.Level, SelectedLevel, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var term = SearchText.ToLower();
                    query = query.Where(log => (log.Details?.ToLower().Contains(term) == true) || (log.Source?.ToLower().Contains(term) == true));
                }
                
                int total = query.Count();
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => {
                    TotalItems = total;
                    TotalPages = (int)Math.Ceiling((double)total / PageSize) == 0 ? 1 : (int)Math.Ceiling((double)total / PageSize);
                }));
            }
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
                    RefreshPagedLogs();
                }
            }, TaskScheduler.Default);
        }

        private void ExportLogs()
        {
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
