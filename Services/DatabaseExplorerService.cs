using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace QuanLyGiuXe.Services
{
    public class DatabaseExplorerService
    {
        private readonly DatabaseService _dbService;

        public DatabaseExplorerService()
        {
            _dbService = new DatabaseService();
        }

        public List<string> GetTables()
        {
            var tables = new List<string>();
            string sql = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME != 'sysdiagrams' ORDER BY TABLE_NAME";
            
            using (var conn = new SqlConnection(_dbService.GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }
            return tables;
        }

        public DataTable GetTableSchema(string tableName)
        {
            string sql = @"
                SELECT 
                    c.COLUMN_NAME as 'Tên Cột', 
                    c.DATA_TYPE as 'Kiểu Dữ Liệu', 
                    c.CHARACTER_MAXIMUM_LENGTH as 'Độ Dài Max',
                    c.IS_NULLABLE as 'Cho Phép Null',
                    (
                        SELECT CASE WHEN count(1) > 0 THEN 'YES' ELSE 'NO' END 
                        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                        JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                        WHERE kcu.TABLE_NAME = c.TABLE_NAME AND kcu.COLUMN_NAME = c.COLUMN_NAME AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    ) as 'Khóa Chính'
                FROM INFORMATION_SCHEMA.COLUMNS c
                WHERE c.TABLE_NAME = @TableName AND c.TABLE_SCHEMA = 'dbo'
                ORDER BY c.ORDINAL_POSITION;";

            var dataTable = new DataTable();
            using (var conn = new SqlConnection(_dbService.GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@TableName", tableName);
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dataTable);
                    }
                }
            }
            return dataTable;
        }

        public DataTable ExecuteQuery(string query, out string message, bool force = false)
        {
            message = "Thành công";
            var dataTable = new DataTable();

            if (string.IsNullOrWhiteSpace(query))
            {
                message = "Câu lệnh trống";
                return dataTable;
            }

            string upperQuery = query.ToUpper().Trim();
            bool isDangerous = (upperQuery.StartsWith("UPDATE") || upperQuery.StartsWith("DELETE")) && !upperQuery.Contains("WHERE");

            if (isDangerous && !force)
            {
                throw new InvalidOperationException("Cảnh báo: Câu lệnh không có WHERE có thể ảnh hưởng toàn bộ dữ liệu. Bạn phải thêm WHERE hoặc xác nhận cưỡng chế.");
            }

            using (var conn = new SqlConnection(_dbService.GetConnectionString()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(query, conn))
                {
                    if (upperQuery.StartsWith("SELECT"))
                    {
                        using (var adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dataTable);
                        }
                    }
                    else
                    {
                        int rowsAffected = cmd.ExecuteNonQuery();
                        message = $"{rowsAffected} dòng đã bị ảnh hưởng.";
                        
                        // Return empty datatable with message for non-select queries
                        dataTable.Columns.Add("Message", typeof(string));
                        dataTable.Rows.Add(message);
                    }
                }
            }
            return dataTable;
        }
    }
}
