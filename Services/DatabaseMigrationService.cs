using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class DatabaseMigrationService
    {
        public static async Task MigrateDatabaseAsync(ConnectionConfig config)
        {
            try
            {
                // 1. Thực thi các file script Migration của hệ thống
                await DatabaseService.EnsureMigrationsAppliedAsync();

                // 2. Đồng thời, nâng cấp hoặc bổ sung bảng SystemInfo lên schema version 2
                using var conn = await SqlConnectionService.GetConnectionAsync(config);
                
                string ensureSystemInfo = @"
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
                ";

                using var cmd = new SqlCommand(ensureSystemInfo, conn);
                await cmd.ExecuteNonQueryAsync();
                LoggingService.Instance.LogInfo("DB_MIGRATION", "Migrate", "Database đã được nâng cấp lên version 2 thành công.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DB_MIGRATION", "Migrate", "Lỗi nâng cấp database", ex);
                throw;
            }
        }
    }
}
