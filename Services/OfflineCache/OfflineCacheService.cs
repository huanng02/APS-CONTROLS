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
            _connectionString = $"Data Source={_dbPath};Default Timeout=5;";
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
                    );

                    CREATE TABLE IF NOT EXISTS LocalXeTrongBai (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardId INTEGER,
                        BienSo TEXT,
                        ThoiGianVao DATETIME,
                        AnhXe TEXT,
                        IsSynced INTEGER DEFAULT 0
                    );

                    CREATE INDEX IF NOT EXISTS idx_localxe_cardid ON LocalXeTrongBai(CardId);

                    CREATE TABLE IF NOT EXISTS session_audit_logs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SessionId TEXT,
                        EventType TEXT,
                        OldState TEXT,
                        NewState TEXT,
                        Message TEXT,
                        CreatedUtc DATETIME
                    );

                    CREATE TABLE IF NOT EXISTS ParkingSites (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SiteCode TEXT NOT NULL UNIQUE,
                        SiteName TEXT NOT NULL,
                        Description TEXT,
                        IsActive INTEGER DEFAULT 1,
                        CreatedUtc DATETIME
                    );

                    CREATE TABLE IF NOT EXISTS ParkingZones (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SiteId INTEGER NOT NULL,
                        ZoneCode TEXT NOT NULL UNIQUE,
                        ZoneName TEXT NOT NULL,
                        Description TEXT,
                        MaxCapacity INTEGER DEFAULT 100,
                        IsActive INTEGER DEFAULT 1,
                        CreatedUtc DATETIME,
                        FOREIGN KEY (SiteId) REFERENCES ParkingSites(Id)
                    );

                    CREATE TABLE IF NOT EXISTS C3Controllers (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ControllerName TEXT NOT NULL,
                        IpAddress TEXT NOT NULL UNIQUE,
                        ZoneId INTEGER NOT NULL,
                        IsActive INTEGER DEFAULT 1,
                        CreatedUtc DATETIME,
                        FOREIGN KEY (ZoneId) REFERENCES ParkingZones(Id)
                    );

                    CREATE TABLE IF NOT EXISTS Lanes (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        LaneCode TEXT NOT NULL UNIQUE,
                        LaneName TEXT NOT NULL,
                        Direction TEXT NOT NULL,
                        ZoneId INTEGER,
                        IsActive INTEGER DEFAULT 1,
                        CreatedUtc DATETIME,
                        FOREIGN KEY (ZoneId) REFERENCES ParkingZones(Id)
                    );

                    CREATE TABLE IF NOT EXISTS VehicleSessions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardId INTEGER,
                        BienSo TEXT,
                        ThoiGianVao DATETIME NOT NULL,
                        ThoiGianRa DATETIME,
                        Tien REAL,
                        TrangThai TEXT,
                        AnhVao TEXT,
                        AnhRa TEXT,
                        SiteId INTEGER,
                        ZoneId INTEGER,
                        EntryLaneId INTEGER,
                        ExitLaneId INTEGER,
                        CreatedUtc DATETIME,
                        FOREIGN KEY (SiteId) REFERENCES ParkingSites(Id),
                        FOREIGN KEY (ZoneId) REFERENCES ParkingZones(Id),
                        FOREIGN KEY (EntryLaneId) REFERENCES Lanes(Id),
                        FOREIGN KEY (ExitLaneId) REFERENCES Lanes(Id)
                    );

                    CREATE INDEX IF NOT EXISTS idx_sessions_siteid ON VehicleSessions(SiteId);
                    CREATE INDEX IF NOT EXISTS idx_sessions_zoneid ON VehicleSessions(ZoneId);
                    CREATE INDEX IF NOT EXISTS idx_lanes_zoneid ON Lanes(ZoneId);
                ";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // Seed SQLite data
                string seedSql = @"
                    INSERT OR IGNORE INTO ParkingSites (SiteCode, SiteName, Description, IsActive, CreatedUtc)
                    VALUES ('DEFAULT-SITE', 'Default Parking Site', 'Auto-seeded default site', 1, CURRENT_TIMESTAMP);

                    INSERT OR IGNORE INTO ParkingZones (SiteId, ZoneCode, ZoneName, Description, MaxCapacity, IsActive, CreatedUtc)
                    VALUES (
                        (SELECT Id FROM ParkingSites WHERE SiteCode = 'DEFAULT-SITE'),
                        'DEFAULT-ZONE', 'Default Parking Zone', 'Auto-seeded default zone', 500, 1, CURRENT_TIMESTAMP
                    );

                    INSERT OR IGNORE INTO Lanes (LaneCode, LaneName, Direction, ZoneId, IsActive, CreatedUtc)
                    VALUES 
                    ('LANE-1', 'Cổng Vào 1', 'IN', (SELECT Id FROM ParkingZones WHERE ZoneCode = 'DEFAULT-ZONE'), 1, CURRENT_TIMESTAMP),
                    ('LANE-2', 'Cổng Ra 1', 'OUT', (SELECT Id FROM ParkingZones WHERE ZoneCode = 'DEFAULT-ZONE'), 1, CURRENT_TIMESTAMP);
                ";

                using (var cmd = new SqliteCommand(seedSql, conn))
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
                
                // Sync Topology Data
                await SaveCacheAsync("LIST_SITES", await ParkingTopologyService.Instance.GetSitesAsync());
                await SaveCacheAsync("LIST_ZONES", await ParkingTopologyService.Instance.GetZonesAsync());
                await SaveCacheAsync("LIST_CONTROLLERS", await ParkingTopologyService.Instance.GetControllersAsync());
                await SaveCacheAsync("LIST_LANES", await ParkingTopologyService.Instance.GetLanesAsync());
                
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

        // --- Local Active Session Management ---

        public async Task SaveActiveSessionLocalAsync(int cardId, string bienSo, DateTime time, string anhXe)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                // First delete existing active session for this card to prevent duplicates during normal flows
                string delSql = "DELETE FROM LocalXeTrongBai WHERE CardId = @cardId";
                using (var delCmd = new SqliteCommand(delSql, conn, tx))
                {
                    delCmd.Parameters.AddWithValue("@cardId", cardId);
                    await delCmd.ExecuteNonQueryAsync();
                }

                string sql = @"INSERT INTO LocalXeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe, IsSynced) 
                               VALUES (@cardId, @bienSo, @time, @anh, 0)";
                using var cmd = new SqliteCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@cardId", cardId);
                cmd.Parameters.AddWithValue("@bienSo", bienSo ?? string.Empty);
                cmd.Parameters.AddWithValue("@time", time);
                cmd.Parameters.AddWithValue("@anh", anhXe ?? string.Empty);
                await cmd.ExecuteNonQueryAsync();

                await tx.CommitAsync();

                // Log audit trail
                await SessionAuditService.Instance.LogAuditAsync(
                    cardId.ToString(),
                    "CREATE",
                    "None",
                    "Active",
                    $"Successfully saved active session locally for card {cardId}"
                );
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                LoggingService.Instance.LogError("OFFLINE_CACHE", "SaveActiveSessionLocalAsync", $"Failed to save session for card {cardId}", ex);
                throw;
            }
        }

        public async Task DeleteActiveSessionLocalAsync(int cardId)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                string sql = "DELETE FROM LocalXeTrongBai WHERE CardId = @cardId";
                using var cmd = new SqliteCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@cardId", cardId);
                await cmd.ExecuteNonQueryAsync();

                await tx.CommitAsync();

                // Log audit trail
                await SessionAuditService.Instance.LogAuditAsync(
                    cardId.ToString(),
                    "CLOSE",
                    "Active",
                    "None",
                    $"Successfully closed session locally for card {cardId}"
                );
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                LoggingService.Instance.LogError("OFFLINE_CACHE", "DeleteActiveSessionLocalAsync", $"Failed to delete session for card {cardId}", ex);
                throw;
            }
        }

        public async Task<bool> IsXeTrongBaiLocalAsync(int cardId)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = "SELECT COUNT(*) FROM LocalXeTrongBai WHERE CardId = @cardId";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cardId", cardId);
                var val = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(val) > 0;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "IsXeTrongBaiLocalAsync", $"Failed for card {cardId}", ex);
                return false;
            }
        }

        public async Task<(int Id, string BienSo, DateTime ThoiGianVao)?> GetXeTrongBaiRecordLocalAsync(int cardId)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = "SELECT CardId, BienSo, ThoiGianVao FROM LocalXeTrongBai WHERE CardId = @cardId";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cardId", cardId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    int id = r.GetInt32(0);
                    string bs = r.IsDBNull(1) ? string.Empty : r.GetString(1);
                    DateTime vao = r.GetDateTime(2);
                    return (id, bs, vao);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "GetXeTrongBaiRecordLocalAsync", $"Failed for card {cardId}", ex);
            }
            return null;
        }
    }
}
