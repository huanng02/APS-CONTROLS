using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class DatabaseExplorerViewModel : BaseViewModel, IDisposable
    {
        private readonly DatabaseExplorerService _dbService;
        private CancellationTokenSource _reloadCts;

        // ──────────────────────────────────────────────
        // Properties
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
                    LoadTableDataAndSchema();
                }
            }
        }

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

        // ──────────────────────────────────────────────
        // Constructor
        // ──────────────────────────────────────────────
        public DatabaseExplorerViewModel()
        {
            _dbService = new DatabaseExplorerService();

            // Hiển thị server info từ ConnectionManager
            ConnectedServerInfo = ConnectionManager.Instance.GetDisplayInfo();

            // Subscribe ConnectionChanged → tự reload khi connection đổi
            ConnectionManager.Instance.ConnectionChanged += OnConnectionChanged;

            RefreshCommand         = new RelayCommand(_ => LoadTables(), _ => !IsLoading);
            ExecuteQueryCommand    = new RelayCommand(_ => ExecuteQuery(), _ => !IsLoading);
            GenerateSelectCommand  = new RelayCommand(_ => GenerateSelect());
            GenerateInsertCommand  = new RelayCommand(_ => GenerateInsert());
            GenerateUpdateCommand  = new RelayCommand(_ => GenerateUpdate());
            GenerateDeleteCommand  = new RelayCommand(_ => GenerateDelete());
            ChangeConnectionCommand = new RelayCommand(_ => ChangeConnection(), _ => !IsLoading);

            LoadTables();
        }

        // ──────────────────────────────────────────────
        // Change Connection
        // ──────────────────────────────────────────────

        /// <summary>
        /// Mở ConnectDatabaseWindow. Nếu user bấm Connect thành công,
        /// ConnectionManager sẽ raise ConnectionChanged và ReloadAsync() sẽ được gọi tự động.
        /// </summary>
        private void ChangeConnection()
        {
            var window = new Views.ConnectDatabaseWindow
            {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
            // Reload được kích hoạt tự động qua OnConnectionChanged event,
            // không cần gọi lại ở đây.
        }

        /// <summary>
        /// Handler cho ConnectionManager.ConnectionChanged.
        /// Luôn marshal về UI thread vì event có thể đến từ bất kỳ thread nào.
        /// </summary>
        private void OnConnectionChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => _ = ReloadAsync());
        }

        /// <summary>
        /// Clear toàn bộ dữ liệu cũ rồi load lại từ DB mới.
        /// Hỗ trợ cancellation để tránh race condition khi user đổi connection nhanh nhiều lần.
        /// </summary>
        private async Task ReloadAsync()
        {
            // Huỷ reload đang chạy nếu có
            _reloadCts?.Cancel();
            _reloadCts = new CancellationTokenSource();
            var token = _reloadCts.Token;

            IsLoading = true;

            // Cập nhật label server
            ConnectedServerInfo = ConnectionManager.Instance.GetDisplayInfo();

            // Clear dữ liệu cũ ngay lập tức
            Tables.Clear();
            CurrentData   = null;
            CurrentSchema = null;
            QueryResult   = null;
            _selectedTable = null;
            OnPropertyChanged(nameof(SelectedTable));

            try
            {
                // Chạy query trên background thread
                var tables = await Task.Run(() => _dbService.GetTables(), token);

                if (token.IsCancellationRequested) return;

                foreach (var t in tables) Tables.Add(t);

                if (Tables.Count > 0)
                    SelectedTable = Tables[0];
                else
                    LoggingService.Instance.LogInfo("DatabaseExplorer", "Reload",
                        "DB mới không có bảng nào hoặc không có quyền đọc.");
            }
            catch (OperationCanceledException)
            {
                // Reload bị huỷ, bỏ qua
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DatabaseExplorer", "ReloadAsync", "Lỗi reload sau đổi connection", ex);
                MessageBox.Show(
                    $"Không thể tải dữ liệu từ server mới.\n\nChi tiết: {ex.Message}",
                    "Lỗi kết nối",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        // ──────────────────────────────────────────────
        // Load data
        // ──────────────────────────────────────────────

        private void LoadTables()
        {
            try
            {
                Tables.Clear();
                var list = _dbService.GetTables();
                foreach (var t in list) Tables.Add(t);

                if (Tables.Count > 0 && string.IsNullOrEmpty(SelectedTable))
                    SelectedTable = Tables[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách bảng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTableDataAndSchema()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;

            try
            {
                CurrentSchema = _dbService.GetTableSchema(SelectedTable);

                string query = $"SELECT * FROM [{SelectedTable}]";
                CurrentData  = _dbService.ExecuteQuery(query, out string _);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu bảng {SelectedTable}: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────
        // Execute Query
        // ──────────────────────────────────────────────

        private void ExecuteQuery()
        {
            try
            {
                QueryResult = _dbService.ExecuteQuery(QueryText, out string message, force: false);
                if (message != "Thành công")
                {
                    MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
                        QueryResult = _dbService.ExecuteQuery(QueryText, out string msg, force: true);
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
        }

        // ──────────────────────────────────────────────
        // Generate SQL helpers
        // ──────────────────────────────────────────────

        private void GenerateSelect()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            QueryText = $"SELECT TOP 100 * \r\nFROM [{SelectedTable}]\r\n-- WHERE ...";
        }

        private void GenerateInsert()
        {
            if (string.IsNullOrEmpty(SelectedTable) || CurrentSchema == null) return;

            var columns = new System.Collections.Generic.List<string>();
            foreach (DataRow row in CurrentSchema.Rows)
                columns.Add(row["Tên Cột"].ToString());

            string cols = string.Join(", ", columns);
            string vals = string.Join(", ", columns.ConvertAll(c => $"@{c}"));
            QueryText = $"INSERT INTO [{SelectedTable}] ({cols})\r\nVALUES ({vals})";
        }

        private void GenerateUpdate()
        {
            if (string.IsNullOrEmpty(SelectedTable) || CurrentSchema == null) return;

            var columns = new System.Collections.Generic.List<string>();
            foreach (DataRow row in CurrentSchema.Rows)
                columns.Add(row["Tên Cột"].ToString());

            var assignments = columns.ConvertAll(c => $"[{c}] = @{c}");
            string assigns  = string.Join(",\r\n    ", assignments);
            QueryText = $"UPDATE [{SelectedTable}]\r\nSET\r\n    {assigns}\r\nWHERE Id = @Id";
        }

        private void GenerateDelete()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            QueryText = $"DELETE FROM [{SelectedTable}]\r\nWHERE Id = @Id";
        }

        // ──────────────────────────────────────────────
        // IDisposable – unsubscribe event để tránh memory leak
        // ──────────────────────────────────────────────
        public void Dispose()
        {
            ConnectionManager.Instance.ConnectionChanged -= OnConnectionChanged;
            _reloadCts?.Cancel();
            _reloadCts?.Dispose();
        }
    }
}
