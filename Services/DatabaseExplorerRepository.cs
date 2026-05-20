using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Async repository cho Database Explorer.
    /// Hỗ trợ: server-side pagination (OFFSET/FETCH + keyset), cancellation, timeout.
    /// </summary>
    public class DatabaseExplorerRepository
    {
        private const int DefaultCommandTimeout = 30;

        private string ConnectionString => ConnectionManager.Instance.CurrentConnectionString;

        // ──────────────────────────────────────────────
        // Table List
        // ──────────────────────────────────────────────

        public async Task<List<string>> GetTablesAsync(CancellationToken ct = default)
        {
            var tables = new List<string>();
            const string sql = @"
                SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != 'sysdiagrams' 
                ORDER BY TABLE_NAME";

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultCommandTimeout })
                using (var reader = await cmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                        tables.Add(reader.GetString(0));
                }
            }
            return tables;
        }

        // ──────────────────────────────────────────────
        // Schema
        // ──────────────────────────────────────────────

        public async Task<DataTable> GetTableSchemaAsync(string tableName, CancellationToken ct = default)
        {
            const string sql = @"
                SELECT 
                    c.COLUMN_NAME       AS N'Tên Cột', 
                    c.DATA_TYPE         AS N'Kiểu Dữ Liệu', 
                    c.CHARACTER_MAXIMUM_LENGTH AS N'Độ Dài Max',
                    c.IS_NULLABLE       AS N'Cho Phép Null',
                    (
                        SELECT CASE WHEN COUNT(1) > 0 THEN 'YES' ELSE 'NO' END 
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                        JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                          ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        WHERE kcu.TABLE_NAME = c.TABLE_NAME 
                          AND kcu.COLUMN_NAME = c.COLUMN_NAME 
                          AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    ) AS N'Khóa Chính'
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = @TableName AND c.TABLE_SCHEMA = 'dbo'
                ORDER BY c.ORDINAL_POSITION;";

            var dt = new DataTable();
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultCommandTimeout })
                {
                    cmd.Parameters.AddWithValue("@TableName", tableName);
                    using (var reader = await cmd.ExecuteReaderAsync(ct))
                        dt.Load(reader);
                }
            }
            return dt;
        }

        // ──────────────────────────────────────────────
        // Column List (for building SELECT)
        // ──────────────────────────────────────────────

        public async Task<List<string>> GetColumnNamesAsync(string tableName, CancellationToken ct = default)
        {
            var cols = new List<string>();
            const string sql = @"
                SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo'
                ORDER BY ORDINAL_POSITION";

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultCommandTimeout })
                {
                    cmd.Parameters.AddWithValue("@TableName", tableName);
                    using (var reader = await cmd.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                            cols.Add(reader.GetString(0));
                    }
                }
            }
            return cols;
        }

        // ──────────────────────────────────────────────
        // Row Count
        // ──────────────────────────────────────────────

        public async Task<long> GetRowCountAsync(string tableName, string searchFilter = null, CancellationToken ct = default)
        {
            string whereClause = string.IsNullOrWhiteSpace(searchFilter) ? "" : $" WHERE {searchFilter}";
            string sql = $"SELECT COUNT_BIG(*) FROM [{tableName}]{whereClause}";

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultCommandTimeout })
                {
                    var result = await cmd.ExecuteScalarAsync(ct);
                    return Convert.ToInt64(result);
                }
            }
        }

        // ──────────────────────────────────────────────
        // Paged Data (OFFSET/FETCH)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Lấy dữ liệu phân trang bằng OFFSET/FETCH.
        /// Phù hợp cho bảng < 1 triệu rows.
        /// </summary>
        public async Task<DataTable> GetPagedDataAsync(
            string tableName,
            List<string> columns,
            int pageIndex,
            int pageSize,
            string orderByColumn,
            bool isDescending = true,
            string searchFilter = null,
            CancellationToken ct = default)
        {
            // Build column list (tránh SELECT *)
            string colList = columns != null && columns.Count > 0
                ? string.Join(", ", columns.ConvertAll(c => $"[{c}]"))
                : "*";

            string orderDir = isDescending ? "DESC" : "ASC";
            string whereClause = string.IsNullOrWhiteSpace(searchFilter) ? "" : $" WHERE {searchFilter}";
            int offset = pageIndex * pageSize;

            string sql = $@"
                SELECT {colList}
                FROM [{tableName}]{whereClause}
                ORDER BY [{orderByColumn}] {orderDir}
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

            var dt = new DataTable();
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultCommandTimeout })
                using (var reader = await cmd.ExecuteReaderAsync(ct))
                {
                    dt.Load(reader);
                }
            }
            return dt;
        }

        // ──────────────────────────────────────────────
        // Keyset Pagination (cho bảng >1M rows)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Lấy trang tiếp theo dùng keyset pagination.
        /// Nhanh hơn OFFSET khi page number lớn.
        /// lastKeyValue = giá trị Id/key cuối cùng của trang hiện tại.
        /// </summary>
        public async Task<DataTable> GetKeysetPageAsync(
            string tableName,
            List<string> columns,
            int pageSize,
            string keyColumn,
            object lastKeyValue,
            bool isDescending = true,
            string searchFilter = null,
            CancellationToken ct = default)
        {
            string colList = columns != null && columns.Count > 0
                ? string.Join(", ", columns.ConvertAll(c => $"[{c}]"))
                : "*";

            string orderDir = isDescending ? "DESC" : "ASC";
            string compareOp = isDescending ? "<" : ">";

            string whereClause = lastKeyValue != null
                ? $"WHERE [{keyColumn}] {compareOp} @lastKey"
                : "";

            if (!string.IsNullOrWhiteSpace(searchFilter))
            {
                whereClause = string.IsNullOrEmpty(whereClause)
                    ? $"WHERE {searchFilter}"
                    : $"{whereClause} AND {searchFilter}";
            }

            string sql = $@"
                SELECT TOP {pageSize} {colList}
                FROM [{tableName}]
                {whereClause}
                ORDER BY [{keyColumn}] {orderDir}";

            var dt = new DataTable();
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultCommandTimeout })
                {
                    if (lastKeyValue != null)
                        cmd.Parameters.AddWithValue("@lastKey", lastKeyValue);

                    using (var reader = await cmd.ExecuteReaderAsync(ct))
                        dt.Load(reader);
                }
            }
            return dt;
        }

        // ──────────────────────────────────────────────
        // Execute User Query (paged)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Thực thi user query. SELECT queries tự thêm pagination.
        /// Non-SELECT queries thực thi trực tiếp.
        /// </summary>
        public async Task<(DataTable Data, string Message, long TotalRows)> ExecuteQueryAsync(
            string query,
            int pageIndex = 0,
            int pageSize = 50,
            bool force = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return (new DataTable(), "Câu lệnh trống", 0);

            string upperQuery = query.ToUpper().Trim();

            // Kiểm tra bảo mật
            bool isDangerous = (upperQuery.StartsWith("UPDATE") || upperQuery.StartsWith("DELETE"))
                               && !upperQuery.Contains("WHERE");

            if (isDangerous && !force)
                throw new InvalidOperationException(
                    "Cảnh báo: Câu lệnh không có WHERE có thể ảnh hưởng toàn bộ dữ liệu.");

            bool isSelect = upperQuery.StartsWith("SELECT") || upperQuery.StartsWith("WITH")
                            || upperQuery.StartsWith("EXEC");

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);

                if (isSelect)
                {
                    // Đếm total rows bằng CTE
                    long totalRows = 0;
                    try
                    {
                        string countSql = $"SELECT COUNT_BIG(*) FROM ({query}) AS __CountQuery";
                        using (var countCmd = new SqlCommand(countSql, conn) { CommandTimeout = DefaultCommandTimeout })
                        {
                            var countResult = await countCmd.ExecuteScalarAsync(ct);
                            totalRows = Convert.ToInt64(countResult);
                        }
                    }
                    catch
                    {
                        // Nếu count fail (ví dụ EXEC sp), thực thi trực tiếp không phân trang
                        var dt2 = new DataTable();
                        using (var cmd2 = new SqlCommand(query, conn) { CommandTimeout = DefaultCommandTimeout })
                        using (var reader2 = await cmd2.ExecuteReaderAsync(ct))
                            dt2.Load(reader2);
                        return (dt2, "Thành công", dt2.Rows.Count);
                    }

                    // Phân trang SELECT
                    int offset = pageIndex * pageSize;
                    // Wrap original query, thêm OFFSET/FETCH
                    // Chỉ khi query chưa có OFFSET
                    string pagedSql;
                    if (upperQuery.Contains("OFFSET") && upperQuery.Contains("FETCH"))
                    {
                        pagedSql = query; // User đã tự phân trang
                    }
                    else if (upperQuery.Contains("ORDER BY"))
                    {
                        pagedSql = $"{query} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
                    }
                    else
                    {
                        // Không có ORDER BY → thêm ORDER BY (SELECT NULL) để OFFSET hoạt động
                        pagedSql = $"{query} ORDER BY (SELECT NULL) OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
                    }

                    var dt = new DataTable();
                    using (var cmd = new SqlCommand(pagedSql, conn) { CommandTimeout = DefaultCommandTimeout })
                    using (var reader = await cmd.ExecuteReaderAsync(ct))
                        dt.Load(reader);

                    return (dt, "Thành công", totalRows);
                }
                else
                {
                    // Non-SELECT: INSERT, UPDATE, DELETE
                    using (var cmd = new SqlCommand(query, conn) { CommandTimeout = DefaultCommandTimeout })
                    {
                        int affected = await cmd.ExecuteNonQueryAsync(ct);
                        var dt = new DataTable();
                        dt.Columns.Add("Message", typeof(string));
                        dt.Rows.Add($"{affected} dòng đã bị ảnh hưởng.");
                        return (dt, $"{affected} dòng đã bị ảnh hưởng.", 0);
                    }
                }
            }
        }

        // ──────────────────────────────────────────────
        // Index Check (auto recommendation)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra xem cột ORDER BY có index hay không.
        /// Trả về true nếu có index.
        /// </summary>
        public async Task<bool> HasIndexOnColumnAsync(string tableName, string columnName, CancellationToken ct = default)
        {
            const string sql = @"
                SELECT COUNT(1) 
                FROM sys.indexes i
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE OBJECT_NAME(i.object_id) = @table AND c.name = @col";

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(ct);
                using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = DefaultCommandTimeout })
                {
                    cmd.Parameters.AddWithValue("@table", tableName);
                    cmd.Parameters.AddWithValue("@col", columnName);
                    var result = await cmd.ExecuteScalarAsync(ct);
                    return Convert.ToInt32(result) > 0;
                }
            }
        }

        // ──────────────────────────────────────────────
        // Detect best ORDER BY column
        // ──────────────────────────────────────────────

        /// <summary>
        /// Tìm cột phù hợp nhất để ORDER BY.
        /// Ưu tiên: Id > TimestampUtc > Timestamp > ThoiGianVao > PRIMARY KEY đầu tiên
        /// </summary>
        public async Task<string> DetectOrderByColumnAsync(string tableName, DataTable schema = null, CancellationToken ct = default)
        {
            // Kiểm tra các cột phổ biến
            string[] preferredCols = { "Id", "TimestampUtc", "Timestamp", "ThoiGianVao", "NgayTao", "CreatedAt" };

            if (schema != null)
            {
                foreach (var preferred in preferredCols)
                {
                    foreach (DataRow row in schema.Rows)
                    {
                        string colName = row["Tên Cột"]?.ToString() ?? "";
                        if (colName.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                            return colName;
                    }
                }

                // Fallback: tìm PRIMARY KEY
                foreach (DataRow row in schema.Rows)
                {
                    if (row["Khóa Chính"]?.ToString() == "YES")
                        return row["Tên Cột"].ToString();
                }

                // Fallback cuối: cột đầu tiên
                if (schema.Rows.Count > 0)
                    return schema.Rows[0]["Tên Cột"].ToString();
            }

            return "Id"; // Default
        }
    }
}
