using System;
using System.Data;
using System.Windows.Input;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class SQLQueryToolViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _db = new DatabaseService();

        private string _sqlText = "";
        public string SqlText
        {
            get => _sqlText;
            set { _sqlText = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private DataTable _queryResult;
        public DataTable QueryResult
        {
            get => _queryResult;
            set { _queryResult = value; OnPropertyChanged(); }
        }

        public ICommand ExecuteCommand { get; }
        public ICommand ClearCommand { get; }

        public SQLQueryToolViewModel()
        {
            ExecuteCommand = new RelayCommand(_ => ExecuteSql());
            ClearCommand = new RelayCommand(_ =>
            {
                SqlText = "";
                QueryResult = null;
                ErrorMessage = "";
                StatusMessage = "Cleared";
            });
        }

        /// <summary>
        /// Thực thi câu lệnh SQL với các kiểm tra bảo mật cơ bản.
        /// </summary>
        private void ExecuteSql()
        {
            ErrorMessage = "";
            StatusMessage = "Executing...";
            QueryResult = null;

            if (string.IsNullOrWhiteSpace(SqlText))
            {
                ErrorMessage = "Vui lòng nhập câu lệnh SQL.";
                StatusMessage = "Chưa có lệnh";
                return;
            }

            string sql = SqlText.Trim();
            string sqlUpper = sql.ToUpper();

            // 1. Chặn lệnh DROP TABLE để bảo vệ cấu trúc DB
            if (sqlUpper.Contains("DROP "))
            {
                ErrorMessage = "❌ Bảo mật: Không được phép sử dụng lệnh DROP trong công cụ này.";
                StatusMessage = "Bị chặn";
                return;
            }

            // 2. Cảnh báo/Chặn lệnh UPDATE/DELETE không có WHERE
            bool isModification = sqlUpper.StartsWith("UPDATE") || sqlUpper.StartsWith("DELETE");
            if (isModification && !sqlUpper.Contains("WHERE"))
            {
                ErrorMessage = "⚠ Nguy hiểm: Lệnh UPDATE/DELETE thiếu điều kiện WHERE. Vui lòng thêm WHERE để tránh mất dữ liệu toàn bộ bảng.";
                StatusMessage = "Bị chặn (An toàn)";
                return;
            }

            try
            {
                // Tự động xác định kiểu thực thi dựa trên từ khóa đầu tiên
                bool isSelect = sqlUpper.StartsWith("SELECT") || sqlUpper.StartsWith("WITH") || sqlUpper.StartsWith("SHOW") || sqlUpper.StartsWith("EXEC");

                if (isSelect)
                {
                    DataTable dt = _db.ExecuteQuery(sql);
                    QueryResult = dt;
                    StatusMessage = $"Thành công: Trả về {dt.Rows.Count} dòng kết quả.";
                }
                else
                {
                    int affected = _db.ExecuteNonQuery(sql);
                    StatusMessage = $"Thành công: {affected} dòng bị ảnh hưởng.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "❌ Lỗi thực thi SQL:\n" + ex.Message;
                StatusMessage = "Thất bại";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
