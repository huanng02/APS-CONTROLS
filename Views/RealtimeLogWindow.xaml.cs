using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using QuanLyGiuXe.Services;
using System.Collections.Specialized;
using System.Windows.Media;
using System.Globalization;
using QuanLyGiuXe.Converters;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace QuanLyGiuXe.Views
{
    public partial class RealtimeLogWindow : Window
    {
        public RealtimeLogWindow()
        {
            InitializeComponent();

            // Bind to the application's main window DataContext (MainViewModel) so the log list is shared
            try
            {
                if (Application.Current?.MainWindow != null)
                {
                    this.DataContext = Application.Current.MainWindow.DataContext;
                }
            }
            catch { }

            // wire up simple filter handlers (deferred to Loaded to ensure controls exist)
            this.Loaded += (s, e) =>
            {
                try
                {
                    LevelFilter.SelectionChanged += (s2, e2) => ApplyFilters();
                    SearchBox.TextChanged += (s2, e2) => ApplyFilters();
                    // find SortOrder by name at runtime to avoid designer field dependency
                    try
                    {
                        if (this.FindName("ViewOrder") is ComboBox viewCb)
                            viewCb.SelectionChanged += (s2, e2) => { ApplyViewOrder(); };
                    }
                    catch { }
                    ClearFilters.Click += (s2, e2) => { LevelFilter.SelectedIndex = 0; SearchBox.Text = string.Empty; StartDate.SelectedDate = null; EndDate.SelectedDate = null; ApplyFilters(); };
                    StartDate.SelectedDateChanged += (s2, e2) => ApplyFilters();
                    EndDate.SelectedDateChanged += (s2, e2) => ApplyFilters();
                    // ensure default sort (newest first)
                    try
                    {
                        if (this.FindName("ViewOrder") is ComboBox cb) { cb.SelectedIndex = 0; }
                        ApplyViewOrder();
                    }
                    catch { }

                    // pager handlers (pager controls moved below DataGrid)
                    try
                    {
                        if (this.FindName("PrevPage") is Button prev) prev.Click += (s2, e2) => ChangePage(-1);
                        if (this.FindName("NextPage") is Button next) next.Click += (s2, e2) => ChangePage(1);
                        if (this.FindName("PageSize") is ComboBox ps) ps.SelectionChanged += (s2, e2) => { _pageIndex = 0; UpdatePaging(); };
                        if (this.FindName("LogGrid") is DataGrid dg) dg.LoadingRow += LogGrid_LoadingRow;
                    }
                    catch { }

                    // load previous persisted logs into the UI (once on load)
                    LoadPersistedLogs();
                    // subscribe to collection changes so paging updates and (if NewestOnTop) jump to first page
                    if (this.DataContext is QuanLyGiuXe.ViewModels.MainViewModel vm && vm.LogEntries is INotifyCollectionChanged nc)
                    {
                        nc.CollectionChanged += (s2, e2) => Application.Current?.Dispatcher?.BeginInvoke(() =>
                        {
                            try
                            {
                                if (vm.NewestOnTop)
                                {
                                    _pageIndex = 0;
                                }
                                UpdatePaging();
                            }
                            catch { }
                        });
                    }
                }
                catch { }
            };
        }

        private int _pageIndex = 0;
        private int _pageSize = 50;

        private void UpdatePaging()
        {
            try
            {
                if (this.DataContext is not QuanLyGiuXe.ViewModels.MainViewModel vm) return;
                if (this.FindName("PageSize") is ComboBox ps && ps.SelectedItem is ComboBoxItem ci)
                    _pageSize = int.TryParse(ci.Content?.ToString(), out var v) ? v : 50;

                var total = vm.LogEntries.Count;
                var pageCount = (int)Math.Ceiling(total / (double)_pageSize);
                if (_pageIndex >= pageCount) _pageIndex = Math.Max(0, pageCount - 1);

                // Build a display list that includes STT (global index)
                var pageItems = vm.LogEntries.Skip(_pageIndex * _pageSize).Take(_pageSize).ToList();
                var display = pageItems.Select((it, i) => new QuanLyGiuXe.Models.DisplayLogEntry
                {
                    STT = _pageIndex * _pageSize + i + 1,
                    Timestamp = it.Timestamp,
                    Level = it.Level,
                    EventType = it.EventType,
                    Source = it.Source,
                    Details = it.Details,
                    UserId = it.UserId,
                    Plate = it.Plate,
                    Exception = it.Exception
                }).ToList();

                // If sort is Newest first, ensure display shows newest on top for page items
                if (this.FindName("SortOrder") is ComboBox scb && scb.SelectedItem is ComboBoxItem sci && (sci.Content?.ToString() ?? string.Empty) == "Newest first")
                {
                    // pageItems already follows filtered/sorted view ordering; keep as-is
                }

                LogGrid.ItemsSource = display;

                if (this.FindName("PageInfo") is TextBlock pi) pi.Text = $"Page {_pageIndex+1}/{Math.Max(1,pageCount)}";
            }
            catch { }
        }

        private void ChangePage(int delta)
        {
            _pageIndex = Math.Max(0, _pageIndex + delta);
            UpdatePaging();
        }

        private void LogGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            try
            {
                // ensure row alternation index is set so earlier IndexPlusOne approach still works if needed
                // also update page info
                if (this.FindName("PageInfo") is TextBlock pi)
                {
                    if (this.DataContext is QuanLyGiuXe.ViewModels.MainViewModel vm)
                    {
                        var total = vm.LogEntries.Count;
                        var pageCount = (int)Math.Ceiling(total / (double)_pageSize);
                        pi.Text = $"Page {_pageIndex+1}/{Math.Max(1,pageCount)}";
                    }
                }
            }
            catch { }
        }

        private void ApplySort()
        {
            try
            {
                if (this.DataContext is not QuanLyGiuXe.ViewModels.MainViewModel vm) return;
                var view = CollectionViewSource.GetDefaultView(vm.LogEntries);
                var sortCb = this.FindName("SortOrder") as ComboBox;
                if (sortCb != null && sortCb.SelectedItem is ComboBoxItem c && (c.Content?.ToString() ?? "") == "Oldest first")
                {
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(Services.LogEntry.Timestamp), System.ComponentModel.ListSortDirection.Ascending));
                }
                else
                {
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(Services.LogEntry.Timestamp), System.ComponentModel.ListSortDirection.Descending));
                }
                view.Refresh();
                // update paging so STT follows current filtered/sorted view
                UpdatePaging();
            }
            catch { }
        }

        private void ApplyViewOrder()
        {
            try
            {
                if (this.DataContext is not QuanLyGiuXe.ViewModels.MainViewModel vm) return;
                var viewCb = this.FindName("ViewOrder") as ComboBox;
                if (viewCb != null && viewCb.SelectedItem is ComboBoxItem ci && (ci.Content?.ToString() ?? "") == "Oldest first")
                {
                    vm.NewestOnTop = false;
                }
                else
                {
                    vm.NewestOnTop = true;
                }

                // re-apply filter/sort/paging to reflect new ordering
                ApplySort();
                UpdatePaging();
            }
            catch { }
        }

        // export removed — persistent save is automatic to DB

        private void ApplyFilters()
        {
            try
            {
                if (this.DataContext is not QuanLyGiuXe.ViewModels.MainViewModel vm) return;
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(vm.LogEntries);
                view.Filter = obj =>
                {
                    if (obj is QuanLyGiuXe.Services.LogEntry entry)
                    {
                        // level
                        var lvl = (LevelFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
                        if (lvl != "All" && !string.Equals(entry.Level, lvl, System.StringComparison.OrdinalIgnoreCase))
                            return false;

                        // search term
                        var term = SearchBox.Text?.Trim();
                        if (!string.IsNullOrEmpty(term))
                        {
                            if (!( (entry.Details ?? "").IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0
                                || (entry.EventType ?? "").IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0
                                || (entry.Source ?? "").IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0
                                ))
                                return false;
                        }

                        // date range
                        if (StartDate.SelectedDate.HasValue)
                        {
                            if (entry.Timestamp.Date < StartDate.SelectedDate.Value.Date) return false;
                        }
                        if (EndDate.SelectedDate.HasValue)
                        {
                            if (entry.Timestamp.Date > EndDate.SelectedDate.Value.Date) return false;
                        }

                        return true;
                    }
                    return false;
                };
                view.Refresh();
                // update paging so STT and visible page respect the new filter
                UpdatePaging();
            }
            catch { }
        }

        // Load persisted logs from disk (jsonl files) into the UI collection on demand
        private void LoadPersistedLogs()
        {
            try
            {
                var vm = this.DataContext as QuanLyGiuXe.ViewModels.MainViewModel;
                if (vm == null) return;

                // prefer loading from DB AppLogs (if available); fallback to file logs
                try
                {
                    var db = new DatabaseService();
                    var rows = db.GetAppLogs(null, null, 2000);
                    // rows are newest-first from DB; convert timestamp to local and add to VM
                    foreach (var r in rows)
                    {
                        try { r.Timestamp = r.Timestamp.ToLocalTime(); } catch { }
                        Application.Current.Dispatcher.Invoke(() => vm.LogEntries.Add(r));
                    }
                    // default sort: newest first
                    Application.Current?.Dispatcher?.Invoke(() => {
                        if (this.FindName("SortOrder") is ComboBox cb) cb.SelectedIndex = 0;
                        ApplySort();
                    });
                    return;
                }
                catch { /* fallback to files */ }

                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "logs");
                if (!Directory.Exists(logDir)) return;

                // read all .jsonl files (sorted newest first)
                var files = Directory.GetFiles(logDir, "app-log-*.jsonl").OrderByDescending(f => f).ToList();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                foreach (var file in files)
                {
                    try
                    {
                        foreach (var line in File.ReadLines(file))
                        {
                            try
                            {
                                var entry = JsonSerializer.Deserialize<LogEntry>(line, options);
                                if (entry != null)
                                {
                                    // convert timestamp to local for display
                                    entry.Timestamp = entry.Timestamp.ToLocalTime();
                                    // insert at beginning to keep newest top
                                    Application.Current.Dispatcher.Invoke(() => vm.LogEntries.Add(entry));
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
