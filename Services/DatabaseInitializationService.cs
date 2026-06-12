using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class DatabaseInitializationService
    {
        public static async Task InitializeDatabaseAsync(ConnectionConfig config)
        {
            try
            {
                string connStr = config.BuildConnectionString();
                
                // 1. Tận dụng phương thức của dự án gốc để tạo các bảng nền tảng và seed admin/roles
                await DatabaseService.EnsureBaseSchemaAndAdminSeededAsync(connStr);

                // 2. Tạo bảng SystemInfo và ghi nhận SchemaVersion v2
                using var conn = await SqlConnectionService.GetConnectionAsync(config);
                
                using var cmd = new SqlCommand(@"
                IF OBJECT_ID(N'dbo.SystemInfo', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.SystemInfo (
                        SchemaVersion INT NOT NULL,
                        CreatedDate DATETIME NOT NULL,
                        ProjectName NVARCHAR(100) NOT NULL
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM dbo.SystemInfo WHERE ProjectName = 'QuanLyGiuXe-Enterprise')
                BEGIN
                    INSERT INTO dbo.SystemInfo (SchemaVersion, CreatedDate, ProjectName)
                    VALUES (2, GETUTCDATE(), 'QuanLyGiuXe-Enterprise');
                END
                ELSE
                BEGIN
                    UPDATE dbo.SystemInfo 
                    SET SchemaVersion = 2, CreatedDate = GETUTCDATE()
                    WHERE ProjectName = 'QuanLyGiuXe-Enterprise';
                END
                ", conn);
                await cmd.ExecuteNonQueryAsync();
                LoggingService.Instance.LogInfo("DB_INIT", "Init", "Database và bảng SystemInfo đã được khởi tạo thành công với version 2.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DB_INIT", "Initialize", "Lỗi khởi tạo database", ex);
                throw;
            }
        }
    }
}
