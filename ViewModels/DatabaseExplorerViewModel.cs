using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class DatabaseExplorerViewModel : BaseViewModel, IDisposable
    {
        private readonly DatabaseExplorerRepository _repo;
        private readonly DatabaseExplorerService _dbService; // backward compat for ExecuteQuery safety checks
        private CancellationTokenSource _dataCts;
        private CancellationTokenSource _reloadCts;
        private DispatcherTimer _searchDebounceTimer;

        // ──────────────────────────────────────────────
        // Sidebar Properties
        // ──────────────────────────────────────────────

        private string _connectedServerInfo;
        public string ConnectedServerInfo
        {
            get => _connectedServerInfo;
            set { _connectedServerInfo = value; OnPropertyChanged(nameof(ConnectedServerInfo)); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObservableCollection<string> Tables { get; } = new ObservableCollection<string>();

        private string _selectedTable;
        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (_selectedTable != value)
                {
                    _selectedTable = value;
                    OnPropertyChanged(nameof(SelectedTable));
                    // Reset pagination khi đổi bảng
                    CurrentPage = 0;
                    SearchText = "";
                    _ = LoadTableDataAsync();
                }
            }
        }

        // ──────────────────────────────────────────────
        // Data Tab Properties
        // ──────────────────────────────────────────────

        private DataTable _currentData;
        public DataTable CurrentData
        {
            get => _currentData;
            set { _currentData = value; OnPropertyChanged(nameof(CurrentData)); }
        }

        private DataTable _currentSchema;
        public DataTable CurrentSchema
        {
            get => _currentSchema;
            set { _currentSchema = value; OnPropertyChanged(nameof(CurrentSchema)); }
        }

        private bool _isDataLoading;
        public bool IsDataLoading
        {
            get => _isDataLoading;
            set
            {
                _isDataLoading = value;
                OnPropertyChanged(nameof(IsDataLoading));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ──────────────────────────────────────────────
        // Pagination Properties
        // ──────────────────────────────────────────────

        private int _currentPage;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(CurrentPageDisplay));
            }
        }

        /// <summary>Hiển thị 1-based cho UI</summary>
        public int CurrentPageDisplay => CurrentPage + 1;

        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(nameof(TotalPages)); }
        }

        private long _totalRecords;
        public long TotalRecords
        {
            get => _totalRecords;
            set { _totalRecords = value; OnPropertyChanged(nameof(TotalRecords)); OnPropertyChanged(nameof(TotalRecordsDisplay)); }
        }

        public string TotalRecordsDisplay => TotalRecords.ToString("N0");

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    OnPropertyChanged(nameof(PageSize));
                    CurrentPage = 0;
                    _ = LoadPageAsync();
                }
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                // Debounce: đợi 400ms sau lần gõ cuối mới search
                RestartSearchDebounce();
            }
        }

        private string _indexWarning;
        public string IndexWarning
        {
            get => _indexWarning;
            set { _indexWarning = value; OnPropertyChanged(nameof(IndexWarning)); OnPropertyChanged(nameof(HasIndexWarning)); }
        }
        public bool HasIndexWarning => !string.IsNullOrEmpty(IndexWarning);

        private string _orderByColumn = "Id";
        private List<string> _tableColumns;

        // ──────────────────────────────────────────────
        // Query Tab Properties
        // ──────────────────────────────────────────────

        private string _queryText;
        public string QueryText
        {
            get => _queryText;
            set { _queryText = value; OnPropertyChanged(nameof(QueryText)); }
        }

        private DataTable _queryResult;
        public DataTable QueryResult
        {
            get => _queryResult;
            set { _queryResult = value; OnPropertyChanged(nameof(QueryResult)); }
        }

        // Query pagination
        private int _queryCurrentPage;
        public int QueryCurrentPage
        {
            get => _queryCurrentPage;
            set { _queryCurrentPage = value; OnPropertyChanged(nameof(QueryCurrentPage)); OnPropertyChanged(nameof(QueryCurrentPageDisplay)); }
        }
        public int QueryCurrentPageDisplay => QueryCurrentPage + 1;

        private int _queryTotalPages;
        public int QueryTotalPages
        {
            get => _queryTotalPages;
            set { _queryTotalPages = value; OnPropertyChanged(nameof(QueryTotalPages)); }
        }

        private long _queryTotalRecords;
        public long QueryTotalRecords
        {
            get => _queryTotalRecords;
            set { _queryTotalRecords = value; OnPropertyChanged(nameof(QueryTotalRecords)); OnPropertyChanged(nameof(QueryTotalRecordsDisplay)); }
        }
        public string QueryTotalRecordsDisplay => QueryTotalRecords.ToString("N0");

        private bool _isQueryLoading;
        public bool IsQueryLoading
        {
            get => _isQueryLoading;
            set { _isQueryLoading = value; OnPropertyChanged(nameof(IsQueryLoading)); CommandManager.InvalidateRequerySuggested(); }
        }

        private string _lastExecutedQuery;

        // ──────────────────────────────────────────────
        // Page Sizes for ComboBox
        // ──────────────────────────────────────────────
        public int[] PageSizeOptions { get; } = { 20, 50, 100, 200, 500 };

        // ──────────────────────────────────────────────
        // Commands
        // ──────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand ExecuteQueryCommand { get; }
        public ICommand GenerateSelectCommand { get; }
        public ICommand GenerateInsertCommand { get; }
        public ICommand GenerateUpdateCommand { get; }
        public ICommand GenerateDeleteCommand { get; }
        public ICommand ChangeConnectionCommand { get; }

        // Data pagination
        public ICommand FirstPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }
        public ICommand CancelLoadCommand { get; }

        // Query pagination
        public ICommand QueryFirstPageCommand { get; }
        public ICommand QueryPrevPageCommand { get; }
        public ICommand QueryNextPageCommand { get; }
        public ICommand QueryLastPageCommand { get; }

        // ──────────────────────────────────────────────
        // Constructor
        // ──────────────────────────────────────────────

        public DatabaseExplorerViewModel()
        {
            _repo = new DatabaseExplorerRepository();
            _dbService = new DatabaseExplorerService();

            ConnectedServerInfo = ConnectionManager.Instance.GetDisplayInfo();
            ConnectionManager.Instance.ConnectionChanged += OnConnectionChanged;

            // Search debounce timer
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _searchDebounceTimer.Tick += async (s, e) =>
            {
                _searchDebounceTimer.Stop();
                CurrentPage = 0;
                await LoadPageAsync();
            };

            // Commands
            RefreshCommand          = new RelayCommand(async _ => await LoadTablesAsync(), _ => !IsLoading);
            ExecuteQueryCommand     = new RelayCommand(async _ => await ExecuteQueryAsync(), _ => !IsQueryLoading);
            GenerateSelectCommand   = new RelayCommand(_ => GenerateSelect());
            GenerateInsertCommand   = new RelayCommand(_ => GenerateInsert());
            GenerateUpdateCommand   = new RelayCommand(_ => GenerateUpdate());
            GenerateDeleteCommand   = new RelayCommand(_ => GenerateDelete());
            ChangeConnectionCommand = new RelayCommand(_ => ChangeConnection(), _ => !IsLoading);
            CancelLoadCommand       = new RelayCommand(_ => CancelCurrentLoad());

            // Data pagination commands
            FirstPageCommand = new RelayCommand(async _ => { CurrentPage = 0; await LoadPageAsync(); }, _ => CurrentPage > 0 && !IsDataLoading);
            PrevPageCommand  = new RelayCommand(async _ => { CurrentPage--; await LoadPageAsync(); }, _ => CurrentPage > 0 && !IsDataLoading);
            NextPageCommand  = new RelayCommand(async _ => { CurrentPage++; await LoadPageAsync(); }, _ => CurrentPage < TotalPages - 1 && !IsDataLoading);
            LastPageCommand  = new RelayCommand(async _ => { CurrentPage = TotalPages - 1; await LoadPageAsync(); }, _ => CurrentPage < TotalPages - 1 && !IsDataLoading);

            // Query pagination commands
            QueryFirstPageCommand = new RelayCommand(async _ => { QueryCurrentPage = 0; await ReExecuteQueryPageAsync(); }, _ => QueryCurrentPage > 0 && !IsQueryLoading);
            QueryPrevPageCommand  = new RelayCommand(async _ => { QueryCurrentPage--; await ReExecuteQueryPageAsync(); }, _ => QueryCurrentPage > 0 && !IsQueryLoading);
            QueryNextPageCommand  = new RelayCommand(async _ => { QueryCurrentPage++; await ReExecuteQueryPageAsync(); }, _ => QueryCurrentPage < QueryTotalPages - 1 && !IsQueryLoading);
            QueryLastPageCommand  = new RelayCommand(async _ => { QueryCurrentPage = QueryTotalPages - 1; await ReExecuteQueryPageAsync(); }, _ => QueryCurrentPage < QueryTotalPages - 1 && !IsQueryLoading);

            _ = LoadTablesAsync();
        }

        // ──────────────────────────────────────────────
        // Debounce Search
        // ──────────────────────────────────────────────

        private void RestartSearchDebounce()
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        // ──────────────────────────────────────────────
        // Cancel
        // ──────────────────────────────────────────────

        private void CancelCurrentLoad()
        {
            _dataCts?.Cancel();
        }

        // ──────────────────────────────────────────────
        // Connection Change
        // ──────────────────────────────────────────────

        private void ChangeConnection()
        {
            var window = new Views.ConnectDatabaseWindow
            {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => _ = ReloadAsync());
        }

        private async Task ReloadAsync()
        {
            _reloadCts?.Cancel();
            _reloadCts = new CancellationTokenSource();
            var token = _reloadCts.Token;

            IsLoading = true;
            ConnectedServerInfo = ConnectionManager.Instance.GetDisplayInfo();

            Tables.Clear();
            CurrentData = null;
            CurrentSchema = null;
            QueryResult = null;
            _selectedTable = null;
            OnPropertyChanged(nameof(SelectedTable));

            try
            {
                var tables = await _repo.GetTablesAsync(token);
                if (token.IsCancellationRequested) return;

                foreach (var t in tables) Tables.Add(t);

                if (Tables.Count > 0)
                    SelectedTable = Tables[0];
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DatabaseExplorer", "ReloadAsync", "Lỗi reload sau đổi connection", ex);
                MessageBox.Show($"Không thể tải dữ liệu từ server mới.\n\nChi tiết: {ex.Message}",
                    "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (!token.IsCancellationRequested) IsLoading = false;
            }
        }

        // ──────────────────────────────────────────────
        // Load Tables (async)
        // ──────────────────────────────────────────────

        private async Task LoadTablesAsync()
        {
            IsLoading = true;
            try
            {
                Tables.Clear();
                var list = await _repo.GetTablesAsync();
                foreach (var t in list) Tables.Add(t);

                if (Tables.Count > 0 && string.IsNullOrEmpty(SelectedTable))
                    SelectedTable = Tables[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách bảng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ──────────────────────────────────────────────
        // Load Table Data + Schema (async, paged)
        // ──────────────────────────────────────────────

        private async Task LoadTableDataAsync()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;

            // Cancel previous load
            _dataCts?.Cancel();
            _dataCts = new CancellationTokenSource();
            var ct = _dataCts.Token;

            IsDataLoading = true;
            IndexWarning = null;

            try
            {
                // Load schema
                CurrentSchema = await _repo.GetTableSchemaAsync(SelectedTable, ct);
                if (ct.IsCancellationRequested) return;

                // Get columns
                _tableColumns = await _repo.GetColumnNamesAsync(SelectedTable, ct);
                if (ct.IsCancellationRequested) return;

                // Detect ORDER BY column
                _orderByColumn = await _repo.DetectOrderByColumnAsync(SelectedTable, CurrentSchema, ct);
                if (ct.IsCancellationRequested) return;

                // Check index
                bool hasIndex = await _repo.HasIndexOnColumnAsync(SelectedTable, _orderByColumn, ct);
                if (!hasIndex)
                    IndexWarning = $"⚠ Cột [{_orderByColumn}] chưa có index, truy vấn có thể chậm với bảng lớn.";

                // Load first page
                await LoadPageAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DatabaseExplorer", "LoadTableDataAsync", $"Lỗi tải bảng {SelectedTable}", ex);
                MessageBox.Show($"Lỗi tải dữ liệu bảng {SelectedTable}: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!ct.IsCancellationRequested) IsDataLoading = false;
            }
        }

        /// <summary>
        /// Load 1 page dữ liệu cho Data tab.
        /// </summary>
        private async Task LoadPageAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(SelectedTable) || string.IsNullOrEmpty(_orderByColumn)) return;

            if (ct == default)
            {
                _dataCts?.Cancel();
                _dataCts = new CancellationTokenSource();
                ct = _dataCts.Token;
            }

            IsDataLoading = true;

            try
            {
                // Build search filter
                string filter = BuildSearchFilter();

                // Get total count
                TotalRecords = await _repo.GetRowCountAsync(SelectedTable, filter, ct);
                TotalPages = (int)Math.Max(1, Math.Ceiling((double)TotalRecords / PageSize));

                // Clamp page
                if (CurrentPage >= TotalPages) CurrentPage = Math.Max(0, TotalPages - 1);

                if (ct.IsCancellationRequested) return;

                // Load page data
                CurrentData = await _repo.GetPagedDataAsync(
                    SelectedTable, _tableColumns, CurrentPage, PageSize,
                    _orderByColumn, isDescending: true, searchFilter: filter, ct: ct);

                // Refresh pagination button states
                CommandManager.InvalidateRequerySuggested();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DatabaseExplorer", "LoadPageAsync", $"Lỗi tải trang {CurrentPage}", ex);
            }
            finally
            {
                if (!ct.IsCancellationRequested) IsDataLoading = false;
            }
        }

        /// <summary>
        /// Build WHERE clause từ SearchText.
        /// Tìm kiếm trong tất cả cột kiểu string.
        /// </summary>
        private string BuildSearchFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText) || _tableColumns == null || CurrentSchema == null)
                return null;

            string searchEscaped = SearchText.Replace("'", "''");
            var conditions = new List<string>();

            foreach (DataRow row in CurrentSchema.Rows)
            {
                string colName = row["Tên Cột"]?.ToString() ?? "";
                string dataType = row["Kiểu Dữ Liệu"]?.ToString()?.ToLower() ?? "";

                // Chỉ search trên cột string-like
                if (dataType.Contains("char") || dataType.Contains("text") || dataType.Contains("varchar"))
                {
                    conditions.Add($"[{colName}] LIKE N'%{searchEscaped}%'");
                }
            }

            return conditions.Count > 0 ? $"({string.Join(" OR ", conditions)})" : null;
        }

        // ──────────────────────────────────────────────
        // Execute Query (async, paged)
        // ──────────────────────────────────────────────

        private async Task ExecuteQueryAsync()
        {
            IsQueryLoading = true;
            QueryCurrentPage = 0;
            _lastExecutedQuery = QueryText;

            try
            {
                var (data, message, totalRows) = await _repo.ExecuteQueryAsync(
                    QueryText, QueryCurrentPage, PageSize, force: false);

                QueryResult = data;
                QueryTotalRecords = totalRows;
                QueryTotalPages = totalRows > 0 ? (int)Math.Ceiling((double)totalRows / PageSize) : 1;

                if (message != "Thành công")
                    MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                var result = MessageBox.Show(
                    $"{ex.Message}\n\nBạn có chắc chắn muốn CHẠY CƯỠNG CHẾ lệnh này không?",
                    "Cảnh báo bảo mật", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var (data, msg, total) = await _repo.ExecuteQueryAsync(
                            QueryText, QueryCurrentPage, PageSize, force: true);
                        QueryResult = data;
                        QueryTotalRecords = total;
                        QueryTotalPages = total > 0 ? (int)Math.Ceiling((double)total / PageSize) : 1;

                        if (msg != "Thành công")
                            MessageBox.Show(msg, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show($"Lỗi SQL: {ex2.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi SQL: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsQueryLoading = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task ReExecuteQueryPageAsync()
        {
            if (string.IsNullOrWhiteSpace(_lastExecutedQuery)) return;

            IsQueryLoading = true;
            try
            {
                var (data, message, totalRows) = await _repo.ExecuteQueryAsync(
                    _lastExecutedQuery, QueryCurrentPage, PageSize);

                QueryResult = data;
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsQueryLoading = false;
            }
        }

        // ──────────────────────────────────────────────
        // Generate SQL helpers
        // ──────────────────────────────────────────────

        private void GenerateSelect()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;

            string cols = _tableColumns != null && _tableColumns.Count > 0
                ? string.Join(", ", _tableColumns.ConvertAll(c => $"[{c}]"))
                : "*";

            string orderBy = !string.IsNullOrEmpty(_orderByColumn)
                ? $"\r\nORDER BY [{_orderByColumn}] DESC"
                : "";

            QueryText = $"SELECT {cols}\r\nFROM [{SelectedTable}]{orderBy}\r\n-- WHERE ...";
        }

        private void GenerateInsert()
        {
            if (string.IsNullOrEmpty(SelectedTable) || _tableColumns == null) return;

            string cols = string.Join(", ", _tableColumns.ConvertAll(c => $"[{c}]"));
            string vals = string.Join(", ", _tableColumns.ConvertAll(c => $"@{c}"));
            QueryText = $"INSERT INTO [{SelectedTable}] ({cols})\r\nVALUES ({vals})";
        }

        private void GenerateUpdate()
        {
            if (string.IsNullOrEmpty(SelectedTable) || _tableColumns == null) return;

            var assignments = _tableColumns.ConvertAll(c => $"[{c}] = @{c}");
            string assigns = string.Join(",\r\n    ", assignments);
            QueryText = $"UPDATE [{SelectedTable}]\r\nSET\r\n    {assigns}\r\nWHERE Id = @Id";
        }

        private void GenerateDelete()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            QueryText = $"DELETE FROM [{SelectedTable}]\r\nWHERE Id = @Id";
        }

        // ──────────────────────────────────────────────
        // IDisposable
        // ──────────────────────────────────────────────

        public void Dispose()
        {
            ConnectionManager.Instance.ConnectionChanged -= OnConnectionChanged;
            _dataCts?.Cancel();
            _dataCts?.Dispose();
            _reloadCts?.Cancel();
            _reloadCts?.Dispose();
            _searchDebounceTimer?.Stop();
        }
    }
}
