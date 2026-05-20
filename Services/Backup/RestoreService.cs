using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using QuanLyGiuXe.Services.Connection;

namespace QuanLyGiuXe.Services.Backup
{
    public class RestoreService
    {
        private static readonly Lazy<RestoreService> _instance = 
            new Lazy<RestoreService>(() => new RestoreService());
            
        public static RestoreService Instance => _instance.Value;

        private RestoreService() { }

        public async Task<bool> RestoreDatabaseAsync(string backupFilePath)
        {
            var dbConfig = ConnectionManager.Instance.CurrentConfig;
            string dbName = dbConfig.Database;
            
            // Xây dựng chuỗi kết nối vào master db (tránh việc db đang được restore lại bị khóa)
            string masterConnString = $"Server={dbConfig.ServerIP},{dbConfig.Port};Database=master;User Id={dbConfig.Username};Password={dbConfig.Password};TrustServerCertificate=True;Connect Timeout=30;";

            try
            {
                LoggingService.Instance.LogInfo("RESTORE", "Start", $"Bắt đầu quá trình phục hồi dữ liệu từ: {backupFilePath}");

                // 1. Tạm dừng AutoReconnect
                AutoReconnectService.Instance.Stop();

                using (var conn = new SqlConnection(masterConnString))
                {
                    await conn.OpenAsync();

                    // 2. Ép ngắt tất cả kết nối hiện tại đến Database (Single User Mode)
                    string singleUserCmd = $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
                    using (var cmd = new SqlCommand(singleUserCmd, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    LoggingService.Instance.LogInfo("RESTORE", "Mode", $"Đã set SINGLE_USER mode cho {dbName}");

                    // 3. Thực thi phục hồi - Use N'...' for Unicode path
                    string restoreCmd = $@"RESTORE DATABASE [{dbName}] 
                                           FROM DISK = N'{backupFilePath}' 
                                           WITH REPLACE;";
                    using (var cmd = new SqlCommand(restoreCmd, conn))
                    {
                        cmd.CommandTimeout = 600; // 10 phút
                        await cmd.ExecuteNonQueryAsync();
                    }
                    LoggingService.Instance.LogInfo("RESTORE", "Success", $"Đã phục hồi xong từ file {backupFilePath}");

                    // 4. Trả lại trạng thái Multi User
                    string multiUserCmd = $"ALTER DATABASE [{dbName}] SET MULTI_USER;";
                    using (var cmd = new SqlCommand(multiUserCmd, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                LoggingService.Instance.LogRestore(backupFilePath, true, "Phục hồi thành công từ file backup.");

                // Bật lại kết nối
                AutoReconnectService.Instance.Start();
                AutoReconnectService.Instance.ForceCheckAsync();

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogRestore(backupFilePath, false, ex.Message);

                // Cố gắng trả lại Multi User nếu bị lỗi giữa chừng
                try
                {
                    using (var conn = new SqlConnection(masterConnString))
                    {
                        await conn.OpenAsync();
                        string multiUserCmd = $"ALTER DATABASE [{dbName}] SET MULTI_USER;";
                        using (var cmd = new SqlCommand(multiUserCmd, conn))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                catch { /* Ignore fallback errors */ }

                AutoReconnectService.Instance.Start();

                throw new Exception($"Không thể phục hồi Database: {ex.Message}");
            }
        }
    }
}
