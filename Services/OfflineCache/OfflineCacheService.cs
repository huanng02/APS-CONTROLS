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
                    );

                    CREATE TABLE IF NOT EXISTS CardsCache (
                        CardId INTEGER PRIMARY KEY,
                        CardUID TEXT UNIQUE,
                        CardName TEXT,
                        BienSo TEXT,
                        TicketTypeId INTEGER,
                        VehicleTypeId INTEGER,
                        TrangThai TEXT,
                        NgayTao DATETIME,
                        ExpireDate DATETIME,
                        LastSyncUtc DATETIME
                    );

                    CREATE TABLE IF NOT EXISTS ParkingSessionsLocal (
                        LocalSessionId INTEGER PRIMARY KEY AUTOINCREMENT,
                        CardNumber TEXT,
                        VehiclePlate TEXT,
                        VehicleTypeId INTEGER,
                        TicketTypeId INTEGER,
                        EntryTimeUtc DATETIME,
                        ExitTimeUtc DATETIME,
                        EntryLaneId INTEGER,
                        ExitLaneId INTEGER,
                        EntryImagePath TEXT,
                        ExitImagePath TEXT,
                        IsActive BOOLEAN,
                        IsPendingSync BOOLEAN,
                        SyncStatus TEXT,
                        CreatedOfflineUtc DATETIME,
                        LastSyncUtc DATETIME,
                        RetryCount INTEGER,
                        ErrorMessage TEXT
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
                
                // Sync RFID Cards to dedicated SQLite table
                var cards = await new DatabaseService().GetRFIDCardsAsync();
                if (cards != null)
                {
                    await SaveCardsToCacheAsync(cards);
                }
                
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

        // --- Cards Cache Management ---

        public async Task SaveCardsToCacheAsync(List<RFIDCard> cards)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                using var tx = conn.BeginTransaction();
                string sql = @"INSERT OR REPLACE INTO CardsCache 
                    (CardId, CardUID, CardName, BienSo, TicketTypeId, VehicleTypeId, TrangThai, NgayTao, ExpireDate, LastSyncUtc) 
                    VALUES (@id, @uid, @name, @bienso, @ticketType, @vehicleType, @trangThai, @ngayTao, @expire, @syncDate)";

                using var cmd = new SqliteCommand(sql, conn, tx);
                
                var pId = cmd.Parameters.Add("@id", SqliteType.Integer);
                var pUid = cmd.Parameters.Add("@uid", SqliteType.Text);
                var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                var pBienso = cmd.Parameters.Add("@bienso", SqliteType.Text);
                var pTicketType = cmd.Parameters.Add("@ticketType", SqliteType.Integer);
                var pVehicleType = cmd.Parameters.Add("@vehicleType", SqliteType.Integer);
                var pTrangThai = cmd.Parameters.Add("@trangThai", SqliteType.Text);
                var pNgayTao = cmd.Parameters.Add("@ngayTao", SqliteType.Text);
                var pExpire = cmd.Parameters.Add("@expire", SqliteType.Text);
                var pSyncDate = cmd.Parameters.Add("@syncDate", SqliteType.Text);

                string now = DateTime.UtcNow.ToString("o");

                foreach (var card in cards)
                {
                    pId.Value = card.Id;
                    pUid.Value = card.UID ?? string.Empty;
                    pName.Value = card.CardName ?? string.Empty;
                    pBienso.Value = card.BienSo ?? string.Empty;
                    pTicketType.Value = card.LoaiVeId;
                    pVehicleType.Value = card.LoaiXeId;
                    pTrangThai.Value = card.TrangThai ?? string.Empty;
                    pNgayTao.Value = card.NgayTao.ToString("o");
                    pExpire.Value = card.NgayHetHan?.ToString("o") ?? (object)DBNull.Value;
                    pSyncDate.Value = now;

                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "SaveCards", "Failed to bulk save cards to cache", ex);
            }
        }

        public async Task<RFIDCard?> GetCardFromCacheAsync(string uid)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "SELECT * FROM CardsCache WHERE CardUID = @uid";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@uid", uid);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new RFIDCard
                    {
                        Id = reader.GetInt32(0),
                        UID = reader.GetString(1),
                        CardName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        BienSo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        LoaiVeId = reader.GetInt32(4),
                        LoaiXeId = reader.GetInt32(5),
                        TrangThai = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        NgayTao = reader.IsDBNull(7) ? DateTime.MinValue : DateTime.Parse(reader.GetString(7)),
                        NgayHetHan = reader.IsDBNull(8) ? (DateTime?)null : DateTime.Parse(reader.GetString(8))
                    };
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "GetCard", $"Failed to read card cache for UID: {uid}", ex);
            }
            return null;
        }

        // --- Local Parking Session Management ---

        public async Task<int> CreateOfflineSessionAsync(ParkingSession session)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"INSERT INTO ParkingSessionsLocal 
                    (CardNumber, VehiclePlate, VehicleTypeId, TicketTypeId, EntryTimeUtc, EntryLaneId, EntryImagePath, IsActive, IsPendingSync, SyncStatus, CreatedOfflineUtc) 
                    VALUES (@card, @plate, @vType, @tType, @entryTime, @laneId, @imgPath, @isActive, @pending, @status, @created)";
                
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@card", session.CardNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@plate", session.BienSoXe ?? string.Empty);
                cmd.Parameters.AddWithValue("@vType", session.LoaiXeId);
                cmd.Parameters.AddWithValue("@tType", session.LoaiVeId);
                cmd.Parameters.AddWithValue("@entryTime", session.ThoiGianVao.ToString("o"));
                cmd.Parameters.AddWithValue("@laneId", session.LanVaoId);
                cmd.Parameters.AddWithValue("@imgPath", session.HinhAnhVao ?? string.Empty);
                cmd.Parameters.AddWithValue("@isActive", true);
                cmd.Parameters.AddWithValue("@pending", true);
                cmd.Parameters.AddWithValue("@status", "PENDING");
                cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));

                await cmd.ExecuteNonQueryAsync();

                // Get the generated ID
                cmd.CommandText = "SELECT last_insert_rowid();";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_SESSION", "Create", "Failed to create local session", ex);
                return -1;
            }
        }

        public async Task<ParkingSession?> GetActiveSessionByCardAsync(string cardNumber)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "SELECT * FROM ParkingSessionsLocal WHERE CardNumber = @card AND IsActive = 1";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@card", cardNumber);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new ParkingSession
                    {
                        // We map LocalSessionId to Id for local tracking if needed, 
                        // but be careful when syncing to SQL.
                        Id = reader.GetInt32(0), 
                        CardNumber = reader.GetString(1),
                        BienSoXe = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        LoaiXeId = reader.GetInt32(3),
                        LoaiVeId = reader.GetInt32(4),
                        ThoiGianVao = DateTime.Parse(reader.GetString(5)),
                        LanVaoId = reader.GetInt32(7),
                        HinhAnhVao = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                    };
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_SESSION", "GetActive", $"Failed to find active session for card: {cardNumber}", ex);
            }
            return null;
        }

        public async Task UpdateOfflineSessionAsync(ParkingSession session)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"UPDATE ParkingSessionsLocal SET 
                    ExitTimeUtc = @exitTime, 
                    ExitLaneId = @exitLane, 
                    ExitImagePath = @exitImg, 
                    IsActive = @isActive, 
                    IsPendingSync = @pending,
                    SyncStatus = @status,
                    LastSyncUtc = @lastSync
                    WHERE LocalSessionId = @id";
                
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@exitTime", session.ThoiGianRa?.ToString("o") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@exitLane", session.LanRaId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@exitImg", session.HinhAnhRa ?? string.Empty);
                cmd.Parameters.AddWithValue("@isActive", false);
                cmd.Parameters.AddWithValue("@pending", true);
                cmd.Parameters.AddWithValue("@status", "PENDING_EXIT");
                cmd.Parameters.AddWithValue("@lastSync", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@id", session.Id); // This is our LocalSessionId

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_SESSION", "Update", $"Failed to update session {session.Id}", ex);
            }
        }

        public async Task<List<ParkingSession>> GetAllLocalSessionsAsync()
        {
            var list = new List<ParkingSession>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "SELECT * FROM ParkingSessionsLocal ORDER BY EntryTimeUtc DESC LIMIT 500";
                using var cmd = new SqliteCommand(sql, conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ParkingSession
                    {
                        Id = reader.GetInt32(0),
                        CardNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        BienSoXe = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        LoaiXeId = reader.GetInt32(3),
                        LoaiVeId = reader.GetInt32(4),
                        ThoiGianVao = DateTime.Parse(reader.GetString(5)),
                        ThoiGianRa = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                        LanVaoId = reader.GetInt32(7),
                        LanRaId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        HinhAnhVao = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                        HinhAnhRa = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                        IsActive = reader.GetBoolean(11),
                        IsPendingSync = reader.GetBoolean(12),
                        SyncStatus = reader.IsDBNull(13) ? string.Empty : reader.GetString(13)
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_SESSION", "GetAll", "Failed to retrieve all local sessions", ex);
            }
            return list;
        }
    }
}
