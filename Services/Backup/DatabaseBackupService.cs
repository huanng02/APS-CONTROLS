using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services.Backup
{
    public class DatabaseBackupService
    {
        private static readonly Lazy<DatabaseBackupService> _instance = 
            new Lazy<DatabaseBackupService>(() => new DatabaseBackupService());
            
        public static DatabaseBackupService Instance => _instance.Value;

        public string GetCurrentBackupDirectory() => GetBackupDirectory();

        public event Action<int> ProgressChanged;

        private static bool _tableChecked = false;

        private DatabaseBackupService() { }

        private async Task EnsureBackupTableExistsAsync()
        {
            if (_tableChecked) return;
            
            string sql = @"
                IF OBJECT_ID('dbo.BackupHistory') IS NULL
                BEGIN
                    CREATE TABLE dbo.BackupHistory (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TimestampUtc DATETIME2 DEFAULT GETUTCDATE(),
                        BackupDate DATETIME,
                        FileName NVARCHAR(255),
                        FilePath NVARCHAR(MAX),
                        FileSize NVARCHAR(50),
                        SizeBytes BIGINT,
                        Status NVARCHAR(50),
                        Source NVARCHAR(50),
                        ErrorMessage NVARCHAR(MAX)
                    )
                END";

            try
            {
                using (var conn = await ConnectionManager.Instance.GetOpenConnectionAsync())
                using (var cmd = new SqlCommand(sql, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                _tableChecked = true;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("BACKUP", "TableInitFailed", "Could not ensure BackupHistory table exists", ex);
            }
        }

        private async Task LogBackupHistoryAsync(string fileName, string filePath, long sizeBytes, string status, string source, string errorMessage = null)
        {
            await EnsureBackupTableExistsAsync();

            string sql = @"INSERT INTO dbo.BackupHistory 
                           (BackupDate, FileName, FilePath, FileSize, SizeBytes, Status, Source, ErrorMessage)
                           VALUES (@date, @file, @path, @sizeStr, @size, @status, @src, @err)";

            try
            {
                using (var conn = await ConnectionManager.Instance.GetOpenConnectionAsync())
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@date", DateTime.Now);
                    cmd.Parameters.AddWithValue("@file", fileName);
                    cmd.Parameters.AddWithValue("@path", filePath);
                    cmd.Parameters.AddWithValue("@sizeStr", $"{sizeBytes / 1024.0 / 1024.0:F2} MB");
                    cmd.Parameters.AddWithValue("@size", sizeBytes);
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@src", source);
                    cmd.Parameters.AddWithValue("@err", (object)errorMessage ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("BACKUP", "LogHistoryFailed", "Lỗi lưu lịch sử backup", ex);
            }
        }

        private string GetBackupDirectory()
        {
            var config = AppConfig.Load().Backup;
            string dir = config.BackupDirectory;
            
            if (string.IsNullOrWhiteSpace(dir)) dir = "Backups";

            if (!Path.IsPathRooted(dir))
            {
                dir = Path.Combine(AppContext.BaseDirectory, dir);
            }

            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("BACKUP", "CreateDirFailed", $"Không thể tạo thư mục backup: {dir}", ex);
                    // Fallback to a safer location if possible
                }
            }

            return dir;
        }

        public async Task<string> BackupNowAsync(string source = "Manual")
        {
            var dbConfig = ConnectionManager.Instance.CurrentConfig;
            string dbName = dbConfig.Database;
            string dir = GetBackupDirectory();
            
            string fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            string filePath = Path.Combine(dir, fileName);

            // Use N'...' for SQL path to handle Unicode characters
            // Removed COMPRESSION because it's not supported on some SQL Express editions
            string query = $@"BACKUP DATABASE [{dbName}] 
                              TO DISK = N'{filePath}' 
                              WITH FORMAT, MEDIANAME = 'APS_Backups', 
                              NAME = N'Full Backup of {dbName}', STATS = 10;";

            LoggingService.Instance.LogInfo("BACKUP", "Start", $"Bắt đầu sao lưu DB {dbName} vào {filePath} (Nguồn: {source})");

            // Collect any SQL error messages that come through InfoMessage
            var sqlErrors = new List<string>();

            try
            {
                using (var conn = await ConnectionManager.Instance.GetOpenConnectionAsync())
                {
                    conn.InfoMessage += (s, e) =>
                    {
                        // Parse "10 percent processed.", "20 percent processed.", etc.
                        if (e.Message.Contains("percent processed"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(e.Message, @"(\d+)");
                            if (match.Success && int.TryParse(match.Value, out int pct))
                            {
                                ProgressChanged?.Invoke(pct);
                            }
                        }

                        // Capture any SQL errors routed through InfoMessage
                        foreach (SqlError err in e.Errors)
                        {
                            if (err.Class > 10) // Severity > 10 = actual error
                            {
                                sqlErrors.Add($"[Severity {err.Class}] {err.Message}");
                            }
                        }
                    };
                    // IMPORTANT: This routes errors through InfoMessage instead of throwing.
                    // We collect them above and check after execution.
                    conn.FireInfoMessageEventOnUserErrors = true;

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 600; // 10 minutes
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // Check if SQL reported errors via InfoMessage
                if (sqlErrors.Count > 0)
                {
                    string errorDetail = string.Join(Environment.NewLine, sqlErrors);
                    LoggingService.Instance.LogError("BACKUP", "SQLError", $"SQL báo lỗi khi backup: {errorDetail}");
                    await LogBackupHistoryAsync(fileName, filePath, 0, "Failed", source, errorDetail);
                    throw new Exception($"SQL Server báo lỗi khi sao lưu:\n{errorDetail}");
                }

                // Verify the file actually exists on disk
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    string msg = $"SQL thực thi thành công nhưng không tìm thấy file tại:\n{filePath}\n\nCó thể SQL Server không có quyền ghi vào thư mục này, hoặc SQL Server đang chạy trên máy khác.";
                    LoggingService.Instance.LogWarning("BACKUP", "FileNotFound", msg);
                    await LogBackupHistoryAsync(fileName, filePath, 0, "Failed", source, msg);
                    throw new Exception(msg);
                }

                long size = fileInfo.Length;
                LoggingService.Instance.LogAudit("BACKUP_SUCCESS", "System", filePath, null, null, source: "Backup", details: $"Sao lưu thành công vào file: {fileName} ({size / 1024.0 / 1024.0:F2} MB)");
                await LogBackupHistoryAsync(fileName, filePath, size, "Success", source);
                
                // Cleanup sau khi backup
                await CleanupOldBackupsAsync();
                
                return filePath;
            }
            catch (Exception ex) when (!ex.Message.Contains("SQL Server báo lỗi") && !ex.Message.Contains("không tìm thấy file"))
            {
                LoggingService.Instance.LogError("BACKUP", "Failed", $"Lỗi trong quá trình sao lưu CSDL vào {filePath}", ex);
                await LogBackupHistoryAsync(fileName, filePath, 0, "Failed", source, ex.Message);
                throw new Exception($"Không thể sao lưu Database: {ex.Message}");
            }
        }

        public async Task<List<BackupFile>> GetBackupFilesAsync()
        {
            return await Task.Run(() =>
            {
                string dir = GetBackupDirectory();
                var files = new DirectoryInfo(dir).GetFiles("*.bak")
                    .OrderByDescending(f => f.CreationTime)
                    .Select(f => new BackupFile
                    {
                        FileName = f.Name,
                        FilePath = f.FullName,
                        CreatedAt = f.CreationTime,
                        SizeBytes = f.Length
                    })
                    .ToList();
                return files;
            });
        }

        public async Task<bool> VerifyBackupAsync(string backupFilePath)
        {
            var dbConfig = ConnectionManager.Instance.CurrentConfig;
            // Connect to master to perform verify
            string masterConnString = $"Server={dbConfig.ServerIP},{dbConfig.Port};Database=master;User Id={dbConfig.Username};Password={dbConfig.Password};TrustServerCertificate=True;Connect Timeout=30;";

            try
            {
                using (var conn = new SqlConnection(masterConnString))
                {
                    await conn.OpenAsync();
                    // RESTORE VERIFYONLY checks if the backup is complete and readable
                    string query = $"RESTORE VERIFYONLY FROM DISK = N'{backupFilePath}';";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return true;
            }
            catch (SqlException ex)
            {
                string msg = ex.Number switch
                {
                    3201 => $"SQL Server không thể mở file backup. Có thể SQL không có quyền truy cập thư mục: {Path.GetDirectoryName(backupFilePath)}",
                    262  => "Tài khoản của bạn không có quyền thực hiện lệnh RESTORE (Cần quyền dbcreator hoặc sysadmin).",
                    _    => ex.Message
                };
                LoggingService.Instance.LogError("BACKUP", "VerifySqlError", msg, ex);
                throw new Exception(msg);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("BACKUP", "VerifyFailed", $"Lỗi kiểm tra file: {backupFilePath}", ex);
                throw;
            }
        }

        public async Task DeleteBackupAsync(string filePath)
        {
            await Task.Run(() =>
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    LoggingService.Instance.LogInfo("BACKUP", "Delete", $"Đã xóa file backup: {filePath}");
                }
            });
        }

        public async Task CleanupOldBackupsAsync()
        {
            var config = AppConfig.Load().Backup;
            if (config.RetentionDays <= 0) return; // Không cleanup

            var limitDate = DateTime.Now.AddDays(-config.RetentionDays);

            await Task.Run(() =>
            {
                try
                {
                    string dir = GetBackupDirectory();
                    var oldFiles = new DirectoryInfo(dir).GetFiles("*.bak")
                                        .Where(f => f.CreationTime < limitDate)
                                        .ToList();

                    foreach (var file in oldFiles)
                    {
                        file.Delete();
                        LoggingService.Instance.LogInfo("BACKUP", "Cleanup", $"Đã xóa file backup cũ: {file.Name}");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("BACKUP", "CleanupFailed", "Lỗi dọn dẹp file backup cũ", ex);
                }
            });
        }
    }
}
