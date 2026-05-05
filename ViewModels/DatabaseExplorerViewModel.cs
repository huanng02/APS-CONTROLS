using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class DatabaseExplorerViewModel : BaseViewModel
    {
        private readonly DatabaseExplorerService _dbService;

        private string _connectedServerInfo;
        public string ConnectedServerInfo
        {
            get => _connectedServerInfo;
            set { _connectedServerInfo = value; OnPropertyChanged(nameof(ConnectedServerInfo)); }
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

        public ICommand RefreshCommand { get; }
        public ICommand ExecuteQueryCommand { get; }
        public ICommand GenerateSelectCommand { get; }
        public ICommand GenerateInsertCommand { get; }
        public ICommand GenerateUpdateCommand { get; }
        public ICommand GenerateDeleteCommand { get; }

        public DatabaseExplorerViewModel()
        {
            _dbService = new DatabaseExplorerService();
            
            var config = DbConnectionConfig.LoadFromFile();
            ConnectedServerInfo = $"Connected to: {config.ServerIP}:{config.Port}";

            RefreshCommand = new RelayCommand(_ => LoadTables());
            ExecuteQueryCommand = new RelayCommand(_ => ExecuteQuery());
            GenerateSelectCommand = new RelayCommand(_ => GenerateSelect());
            GenerateInsertCommand = new RelayCommand(_ => GenerateInsert());
            GenerateUpdateCommand = new RelayCommand(_ => GenerateUpdate());
            GenerateDeleteCommand = new RelayCommand(_ => GenerateDelete());

            LoadTables();
        }

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
                // 1. Get Schema
                CurrentSchema = _dbService.GetTableSchema(SelectedTable);

                // 2. Get Data (Top 100)
                string query = $"SELECT * FROM [{SelectedTable}]";
                CurrentData = _dbService.ExecuteQuery(query, out string _);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu bảng {SelectedTable}: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                // Prompt user to bypass restriction
                var result = MessageBox.Show($"{ex.Message}\n\nBạn có chắc chắn muốn CHẠY CƯỠNG CHẾ lệnh này không?", 
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

        private void GenerateSelect()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            QueryText = $"SELECT TOP 100 * \r\nFROM [{SelectedTable}]\r\n-- WHERE ...";
        }

        private void GenerateInsert()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            if (CurrentSchema == null) return;

            var columns = new System.Collections.Generic.List<string>();
            foreach (DataRow row in CurrentSchema.Rows)
            {
                columns.Add(row["Tên Cột"].ToString());
            }

            string cols = string.Join(", ", columns);
            string vals = string.Join(", ", columns.ConvertAll(c => $"@{c}"));

            QueryText = $"INSERT INTO [{SelectedTable}] ({cols})\r\nVALUES ({vals})";
        }

        private void GenerateUpdate()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            if (CurrentSchema == null) return;

            var columns = new System.Collections.Generic.List<string>();
            foreach (DataRow row in CurrentSchema.Rows)
            {
                columns.Add(row["Tên Cột"].ToString());
            }

            var assignments = new System.Collections.Generic.List<string>();
            foreach (var col in columns)
            {
                assignments.Add($"[{col}] = @{col}");
            }

            string assigns = string.Join(",\r\n    ", assignments);

            QueryText = $"UPDATE [{SelectedTable}]\r\nSET\r\n    {assigns}\r\nWHERE Id = @Id";
        }

        private void GenerateDelete()
        {
            if (string.IsNullOrEmpty(SelectedTable)) return;
            QueryText = $"DELETE FROM [{SelectedTable}]\r\nWHERE Id = @Id";
        }
    }
}
