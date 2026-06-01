using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RealtimeEventFeedViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly List<RealtimeEvent> _allEvents = new();
        private readonly object _lock = new();

        public ObservableCollection<RealtimeEvent> DisplayEvents { get; } = new();

        // Active filters list
        public List<string> Filters { get; } = new() 
        { 
            "All", "Vehicle", "RFID", "Controller", "Reader", "Barrier", "Camera", "User", "System" 
        };

        private string _selectedFilter = "All";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged(nameof(SelectedFilter));
                    RefreshDisplayEvents();
                }
            }
        }

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

        // Live statistics counters for the SOC panel
        private int _totalCount; public int TotalCount { get => _totalCount; set { _totalCount = value; OnPropertyChanged(nameof(TotalCount)); } }
        private int _infoCount; public int InfoCount { get => _infoCount; set { _infoCount = value; OnPropertyChanged(nameof(InfoCount)); } }
        private int _successCount; public int SuccessCount { get => _successCount; set { _successCount = value; OnPropertyChanged(nameof(SuccessCount)); } }
        private int _warningCount; public int WarningCount { get => _warningCount; set { _warningCount = value; OnPropertyChanged(nameof(WarningCount)); } }
        private int _errorCount; public int ErrorCount { get => _errorCount; set { _errorCount = value; OnPropertyChanged(nameof(ErrorCount)); } }
        private int _criticalCount; public int CriticalCount { get => _criticalCount; set { _criticalCount = value; OnPropertyChanged(nameof(CriticalCount)); } }

        // Commands
        public ICommand ClearCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ToggleAutoScrollCommand { get; }
        public ICommand FilterCommand { get; }

        public RealtimeEventFeedViewModel()
        {
            ClearCommand = new RelayCommand(_ => ClearFeed());
            PauseCommand = new RelayCommand(_ => IsPaused = !IsPaused);
            ToggleAutoScrollCommand = new RelayCommand(_ => AutoScroll = !AutoScroll);
            FilterCommand = new RelayCommand(p => SelectedFilter = p?.ToString() ?? "All");

            // Register into centralized EventBus
            EventBus.Instance.Subscribe(OnRealtimeEventReceived);

            // Populate some demo historical logs on startup if SQL database contains logs
            LoadInitialLogs();
        }

        private void LoadInitialLogs()
        {
            // Gather last 30 logs from DB as initial context
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var db = new DatabaseService();
                    var logs = db.GetAppLogs(null, null, 40).OrderBy(x => x.Timestamp);
                    
                    lock (_lock)
                    {
                        foreach (var log in logs)
                        {
                            var ev = new RealtimeEvent
                            {
                                Id = Guid.NewGuid(),
                                Timestamp = log.Timestamp.ToLocalTime(),
                                Source = log.Source ?? "Startup",
                                Message = log.Details ?? string.Empty,
                                EventType = log.EventType ?? "SYSTEM_INFO",
                                Severity = log.Level?.ToUpper() switch
                                {
                                    "INFO" => RealtimeEventSeverity.Info,
                                    "SUCCESS" => RealtimeEventSeverity.Success,
                                    "WARNING" => RealtimeEventSeverity.Warning,
                                    "ERROR" => RealtimeEventSeverity.Error,
                                    "CRITICAL" => RealtimeEventSeverity.Critical,
                                    _ => RealtimeEventSeverity.Info
                                }
                            };

                            // Categorize event type
                            string action = ev.EventType.ToUpper();
                            if (action.Contains("LOGIN")) ev.EventType = "USER_LOGIN";
                            else if (action.Contains("LOGOUT")) ev.EventType = "USER_LOGOUT";
                            else if (action.Contains("BACKUP")) ev.EventType = "SYSTEM_WARNING";
                            else if (action.Contains("ERROR")) ev.EventType = "SYSTEM_ERROR";

                            ev.Site = "HQ Office";
                            ev.Zone = "Zone A";
                            ev.Gate = "Cổng chính";
                            ev.Lane = "Làn vận hành";

                            _allEvents.Add(ev);
                        }
                    }

                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        RefreshDisplayEvents();
                        RecalculateStats();
                    }));
                }
                catch { }
            });
        }

        private void OnRealtimeEventReceived(RealtimeEvent ev)
        {
            if (IsPaused || ev == null) return;

            // Marshall update onto UI Thread dispatcher
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                lock (_lock)
                {
                    _allEvents.Add(ev);
                    
                    // Keep memory buffer capped strictly at 500 events
                    if (_allEvents.Count > 500)
                    {
                        _allEvents.RemoveAt(0);
                    }
                }

                // Append to collection if it matches current active filter
                if (SelectedFilter == "All" || MatchFilter(ev, SelectedFilter))
                {
                    DisplayEvents.Add(ev);
                    if (DisplayEvents.Count > 500)
                    {
                        DisplayEvents.RemoveAt(0);
                    }
                }

                // Increment live stats
                RecalculateStats();
            }));
        }

        private void RefreshDisplayEvents()
        {
            lock (_lock)
            {
                var filtered = _allEvents.AsEnumerable();
                if (SelectedFilter != "All")
                {
                    filtered = filtered.Where(e => MatchFilter(e, SelectedFilter));
                }

                DisplayEvents.Clear();
                foreach (var ev in filtered)
                {
                    DisplayEvents.Add(ev);
                }
            }
        }

        private bool MatchFilter(RealtimeEvent ev, string filter)
        {
            string type = ev.EventType.ToUpper();
            return filter.ToUpper() switch
            {
                "VEHICLE" => type.Contains("VEHICLE") || type.Contains("ENTRY") || type.Contains("EXIT"),
                "RFID" => type.Contains("RFID") || type.Contains("CARD"),
                "CONTROLLER" => type.Contains("CONTROLLER"),
                "READER" => type.Contains("READER"),
                "BARRIER" => type.Contains("BARRIER"),
                "CAMERA" => type.Contains("CAMERA"),
                "USER" => type.Contains("USER"),
                "SYSTEM" => type.Contains("SYSTEM") || type.Contains("LOG"),
                _ => true
            };
        }

        private void RecalculateStats()
        {
            lock (_lock)
            {
                TotalCount = _allEvents.Count;
                InfoCount = _allEvents.Count(e => e.Severity == RealtimeEventSeverity.Info);
                SuccessCount = _allEvents.Count(e => e.Severity == RealtimeEventSeverity.Success);
                WarningCount = _allEvents.Count(e => e.Severity == RealtimeEventSeverity.Warning);
                ErrorCount = _allEvents.Count(e => e.Severity == RealtimeEventSeverity.Error);
                CriticalCount = _allEvents.Count(e => e.Severity == RealtimeEventSeverity.Critical);
            }
        }

        private void ClearFeed()
        {
            lock (_lock)
            {
                _allEvents.Clear();
                DisplayEvents.Clear();
            }
            RecalculateStats();
        }

        public void Dispose()
        {
            EventBus.Instance.Unsubscribe(OnRealtimeEventReceived);
        }
    }
}
