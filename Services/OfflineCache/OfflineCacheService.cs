using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class OfflineCacheService
    {
        private static readonly Lazy<OfflineCacheService> _lazy = new(() => new OfflineCacheService());
        public static OfflineCacheService Instance => _lazy.Value;

        private readonly string _dbPath;
        private readonly string _connectionString;

        private OfflineCacheService()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
            _connectionString = $"Data Source={_dbPath};";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(_dbPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

                // Corruption Recovery Logic
                if (File.Exists(_dbPath))
                {
                    try
                    {
                        using var testConn = new SqliteConnection(_connectionString);
                        testConn.Open();
                        using var testCmd = new SqliteCommand("PRAGMA integrity_check;", testConn);
                        var status = testCmd.ExecuteScalar()?.ToString();
                        if (status != "ok") throw new Exception($"Integrity check failed: {status}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("SQLITE_CORRUPTION", "Init", "Database corrupt, rebuilding...", ex);
                        string backupPath = _dbPath + ".corrupt." + DateTime.Now.Ticks;
                        File.Move(_dbPath, backupPath);
                    }
                }

                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                using (var cmd = new SqliteCommand("PRAGMA journal_mode=WAL;", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                string sql = @"
                    CREATE TABLE IF NOT EXISTS OfflineCache (
                        Key TEXT PRIMARY KEY,
                        Value TEXT,
                        LastUpdatedUtc DATETIME
                    );

                    CREATE TABLE IF NOT EXISTS PendingTransactions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CreatedUtc DATETIME,
                        PayloadJson TEXT,
                        RetryCount INTEGER,
                        LastRetryUtc DATETIME,
                        SyncStatus TEXT,
                        ErrorMessage TEXT,
                        TransactionType TEXT,
                        Fingerprint TEXT UNIQUE
                    );";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "Initialize", "Failed to initialize SQLite", ex);
            }
        }

        public async Task SaveCacheAsync(string key, object data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data);
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "INSERT OR REPLACE INTO OfflineCache (Key, Value, LastUpdatedUtc) VALUES (@key, @val, @date)";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@val", json);
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "Save", $"Failed to save cache for key: {key}", ex);
            }
        }

        public async Task<T?> GetCacheAsync<T>(string key)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "SELECT Value FROM OfflineCache WHERE Key = @key";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@key", key);

                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return JsonConvert.DeserializeObject<T>(result.ToString()!);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "Read", $"Failed to read cache for key: {key}", ex);
            }
            return default;
        }

        // --- Transaction Management ---

        public async Task<int> EnqueueTransactionAsync(PendingTransaction tx)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"INSERT INTO PendingTransactions 
                    (CreatedUtc, PayloadJson, RetryCount, LastRetryUtc, SyncStatus, ErrorMessage, TransactionType, Fingerprint)
                    VALUES (@date, @payload, @retry, @last, @status, @err, @type, @finger);
                    SELECT last_insert_rowid();";

                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@date", tx.CreatedUtc);
                cmd.Parameters.AddWithValue("@payload", tx.PayloadJson ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@retry", tx.RetryCount);
                cmd.Parameters.AddWithValue("@last", tx.LastRetryUtc ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", tx.SyncStatus.ToString());
                cmd.Parameters.AddWithValue("@err", tx.ErrorMessage ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@type", tx.TransactionType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@finger", tx.Fingerprint ?? (object)DBNull.Value);

                var id = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(id);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // Constraint (Unique Fingerprint)
            {
                LoggingService.Instance.LogWarning("OFFLINE_QUEUE", "Enqueue", $"Duplicate transaction skipped: {tx.Fingerprint}");
                return -2;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_QUEUE", "Enqueue", "Failed to enqueue transaction", ex);
                return -1;
            }
        }

        public async Task<List<PendingTransaction>> GetPendingTransactionsAsync()
        {
            var list = new List<PendingTransaction>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "SELECT * FROM PendingTransactions WHERE SyncStatus IN ('Pending', 'Failed') ORDER BY CreatedUtc ASC";
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new PendingTransaction
                    {
                        Id = reader.GetInt32(0),
                        CreatedUtc = reader.GetDateTime(1),
                        PayloadJson = reader.IsDBNull(2) ? null : reader.GetString(2),
                        RetryCount = reader.GetInt32(3),
                        LastRetryUtc = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                        SyncStatus = Enum.Parse<SyncStatus>(reader.GetString(5)),
                        ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                        TransactionType = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Fingerprint = reader.IsDBNull(8) ? null : reader.GetString(8)
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_QUEUE", "List", "Failed to list pending transactions", ex);
            }
            return list;
        }

        public async Task UpdateTransactionStatusAsync(int id, SyncStatus status, string? error = null)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "UPDATE PendingTransactions SET SyncStatus = @status, ErrorMessage = @err, LastRetryUtc = @now, RetryCount = RetryCount + 1 WHERE Id = @id";
                if (status == SyncStatus.Completed)
                {
                    sql = "DELETE FROM PendingTransactions WHERE Id = @id"; // Remove on success to keep it clean
                }

                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@status", status.ToString());
                cmd.Parameters.AddWithValue("@err", error ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_QUEUE", "Update", $"Failed to update transaction {id}", ex);
            }
        }

        public async Task SyncLookupTablesAsync()
        {
            try
            {
                if (!ConnectivityStateService.Instance.IsOnline) return;
 
                LoggingService.Instance.LogInfo("CACHE_SAVE", "Sync", "Starting lookup tables sync...");
 
                // 1. Sync Base Data
                await SaveCacheAsync("LIST_LOAI_XE", await new DatabaseService().GetLoaiXeAsync());
                await SaveCacheAsync("LIST_LOAI_VE", await new LoaiVeRepository().GetAllAsync());
                
                // 2. Sync Pricing Data (Critical for validations)
                await SaveCacheAsync("LIST_BANG_GIA", await new BangGiaRepository().GetAllAsync());
                await SaveCacheAsync("LIST_BANG_GIA_KHUNG_GIO", await new BangGiaKhungGioRepository().GetAllAsync());
 
                // 3. System Settings
                await SaveCacheAsync("SYSTEM_CONFIG", AppConfig.Load());
                
                // 4. Dashboard (Pre-fetch standard filters for offline)
                await new DashboardService().SyncDashboardCacheAsync();
                
                LoggingService.Instance.LogInfo("CACHE_SAVE", "Sync", "Lookup tables sync completed.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CACHE_SAVE", "Sync", "Failed to sync lookup tables", ex);
            }
        }

        public async Task PreloadCacheAsync()
        {
            try
            {
                LoggingService.Instance.LogInfo("CACHE_INIT", "Preload", "Ensuring initial cache exists...");
                
                // If SQL is online, sync now to be sure we have fresh data
                if (ConnectivityStateService.Instance.IsOnline)
                {
                    await SyncLookupTablesAsync();
                }
                else
                {
                    LoggingService.Instance.LogInfo("CACHE_INIT", "Preload", "App started offline, relying on existing SQLite cache.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CACHE_INIT", "Preload", "Preload failed", ex);
            }
        }
    }
}
