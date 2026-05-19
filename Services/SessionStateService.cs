using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Zone-Aware Session Engine (Phase 8.2)
    /// Central service for creating, finding, closing, and repairing vehicle sessions
    /// across the entire multi-zone topology. Offline-safe, reconnect-safe, crash-safe.
    /// </summary>
    public sealed class SessionStateService
    {
        private static readonly Lazy<SessionStateService> _lazy = new(() => new SessionStateService());
        public static SessionStateService Instance => _lazy.Value;

        private readonly string _sqliteConnStr;
        private SessionStateService()
        {
            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
            _sqliteConnStr = $"Data Source={dbPath};Default Timeout=5;";
        }

        // ──────────────────────────────────────────────
        // CREATE SESSION (Entry Flow)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Creates an ACTIVE session by resolving the full topology chain:
        /// Reader → Lane → Zone → Site.
        /// Supports both online (SQL Server) and offline (SQLite) modes.
        /// </summary>
        public async Task<(bool Success, string Message)> CreateEntrySessionAsync(
            int cardId, string plate, int laneId, int? readerId = null)
        {
            // 1. Resolve topology: Lane → Zone → Site
            var lane = (await ParkingTopologyService.Instance.GetLanesAsync())
                .FirstOrDefault(l => l.Id == laneId);
            if (lane == null)
                return (false, $"Lane ID {laneId} not found in topology.");

            int? zoneId = lane.ZoneId;
            int? siteId = null;

            if (zoneId.HasValue)
            {
                var zone = await ParkingTopologyService.Instance.GetZoneAsync(zoneId.Value);
                siteId = zone?.SiteId;

                // Check zone capacity before allowing entry
                bool isFull = await GarageStateService.Instance.IsZoneFullAsync(zoneId.Value);
                if (isFull)
                    return (false, $"ZONE_IS_FULL: Zone '{zone?.ZoneName}' has reached max capacity.");
            }

            // 2. Anti-passback: check if card already has active session anywhere in same Site
            bool alreadyInside = await HasVehicleInsideAsync(cardId, siteId);
            if (alreadyInside)
                return (false, $"ANTI_PASSBACK: Card {cardId} already has an active session in this site.");

            // 3. Build session
            var session = new VehicleSession
            {
                CardId = cardId,
                BienSo = plate,
                ThoiGianVao = DateTime.Now,
                SiteId = siteId,
                ZoneId = zoneId,
                EntryLaneId = laneId,
                EntryReaderId = readerId,
                TrangThai = "Active",
                CreatedUtc = DateTime.UtcNow
            };

            // 4. Write to DB (online) or SQLite (offline)
            bool written = await WriteSessionAsync(session);
            if (!written)
                return (false, "Failed to persist session to any storage layer.");

            LoggingService.Instance.LogVehicle("SESSION_ENTRY", plate,
                entityId: cardId,
                details: $"Zone={zoneId}, Lane={laneId}, Reader={readerId}, Site={siteId}",
                source: "SessionStateService");

            return (true, $"Session created: Zone={zoneId}, Lane={laneId}");
        }

        // ──────────────────────────────────────────────
        // CLOSE SESSION (Exit Flow)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Closes an active session by CardId. Supports cross-zone exit:
        /// the exit lane/zone does NOT need to match the entry lane/zone.
        /// </summary>
        public async Task<(bool Success, string Message, VehicleSession? Session)> CloseSessionAsync(
            int cardId, int exitLaneId, int? exitReaderId = null, double? fee = null)
        {
            // 1. Find active session by CardId (anywhere in any zone/site)
            var session = await GetActiveSessionAsync(cardId);
            if (session == null)
                return (false, $"No active session found for Card {cardId}.", null);

            // 2. Resolve exit topology
            var exitLane = (await ParkingTopologyService.Instance.GetLanesAsync())
                .FirstOrDefault(l => l.Id == exitLaneId);

            // 3. Close the session
            session.ThoiGianRa = DateTime.Now;
            session.ExitLaneId = exitLaneId;
            session.ExitReaderId = exitReaderId;
            session.TrangThai = "Closed";
            session.Tien = fee;

            bool closed = await UpdateSessionCloseAsync(session);
            if (!closed)
                return (false, "Failed to close session in storage.", null);

            LoggingService.Instance.LogVehicle("SESSION_EXIT", session.BienSo,
                entityId: cardId,
                details: $"EntryZone={session.ZoneId}, ExitLane={exitLaneId}, ExitReader={exitReaderId}, Fee={fee:N0}",
                source: "SessionStateService");

            return (true, "Session closed successfully.", session);
        }

        // ──────────────────────────────────────────────
        // QUERY Methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// Gets the active session for a specific card.
        /// Checks SQL Server first, falls back to SQLite.
        /// </summary>
        public async Task<VehicleSession?> GetActiveSessionAsync(int cardId)
        {
            // Try SQL Server
            if (ConnectivityStateService.Instance.IsOnline && !ConnectivityStateService.Instance.IsSimulatingOffline)
            {
                try
                {
                    string connStr = ConnectionManager.Instance.CurrentConnectionString;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    string sql = @"SELECT TOP 1 Id, CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai,
                                          SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, CreatedUtc
                                   FROM dbo.VehicleSessions
                                   WHERE CardId = @cardId AND ThoiGianRa IS NULL AND TrangThai = 'Active'
                                   ORDER BY ThoiGianVao DESC";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@cardId", cardId);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync()) return ReadSessionFromSql(r);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogWarning("SESSION_STATE", "GetActiveSession", $"SQL failed, falling back: {ex.Message}");
                }
            }

            // Fallback to SQLite
            return await GetActiveSessionFromSqliteAsync(cardId);
        }

        /// <summary>
        /// Gets the active session for a specific plate number.
        /// </summary>
        public async Task<VehicleSession?> GetActiveSessionByPlateAsync(string plate)
        {
            if (string.IsNullOrEmpty(plate)) return null;

            if (ConnectivityStateService.Instance.IsOnline && !ConnectivityStateService.Instance.IsSimulatingOffline)
            {
                try
                {
                    string connStr = ConnectionManager.Instance.CurrentConnectionString;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    string sql = @"SELECT TOP 1 Id, CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai,
                                          SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, CreatedUtc
                                   FROM dbo.VehicleSessions
                                   WHERE BienSo = @plate AND ThoiGianRa IS NULL AND TrangThai = 'Active'
                                   ORDER BY ThoiGianVao DESC";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@plate", plate);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync()) return ReadSessionFromSql(r);
                }
                catch { }
            }

            return await GetActiveSessionByPlateFromSqliteAsync(plate);
        }

        /// <summary>Gets all active sessions for a specific zone.</summary>
        public async Task<List<VehicleSession>> GetSessionsByZoneAsync(int zoneId)
        {
            return await GetActiveSessionsFilteredAsync("ZoneId", zoneId);
        }

        /// <summary>Gets all active sessions for a specific site.</summary>
        public async Task<List<VehicleSession>> GetSessionsBySiteAsync(int siteId)
        {
            return await GetActiveSessionsFilteredAsync("SiteId", siteId);
        }

        /// <summary>
        /// Site-wide anti-passback: returns true if card already has an active session 
        /// anywhere within the same site. NOT restricted to same lane or zone.
        /// </summary>
        public async Task<bool> HasVehicleInsideAsync(int cardId, int? siteId = null)
        {
            // Try SQL Server
            if (ConnectivityStateService.Instance.IsOnline && !ConnectivityStateService.Instance.IsSimulatingOffline)
            {
                try
                {
                    string connStr = ConnectionManager.Instance.CurrentConnectionString;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();

                    string sql = "SELECT COUNT(*) FROM dbo.VehicleSessions WHERE CardId = @cardId AND ThoiGianRa IS NULL AND TrangThai = 'Active'";
                    if (siteId.HasValue) sql += " AND SiteId = @siteId";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@cardId", cardId);
                    if (siteId.HasValue) cmd.Parameters.AddWithValue("@siteId", siteId.Value);
                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return count > 0;
                }
                catch { }
            }

            // Fallback to SQLite
            try
            {
                using var conn = new SqliteConnection(_sqliteConnStr);
                await conn.OpenAsync();
                string sql = "SELECT COUNT(*) FROM VehicleSessions WHERE CardId = @cardId AND ThoiGianRa IS NULL AND TrangThai = 'Active'";
                if (siteId.HasValue) sql += " AND SiteId = @siteId";

                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cardId", cardId);
                if (siteId.HasValue) cmd.Parameters.AddWithValue("@siteId", siteId.Value);
                int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return count > 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Detects and repairs orphan sessions (active sessions older than 24 hours
        /// with no exit — likely caused by crashes, power failures, or missed exits).
        /// </summary>
        public async Task<int> RepairOrphanSessionsAsync(int maxAgeHours = 24)
        {
            int repaired = 0;
            try
            {
                if (ConnectivityStateService.Instance.IsOnline && !ConnectivityStateService.Instance.IsSimulatingOffline)
                {
                    string connStr = ConnectionManager.Instance.CurrentConnectionString;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    string sql = @"UPDATE dbo.VehicleSessions 
                                   SET TrangThai = 'OrphanClosed', ThoiGianRa = GETDATE()
                                   WHERE ThoiGianRa IS NULL AND TrangThai = 'Active'
                                   AND DATEDIFF(HOUR, ThoiGianVao, GETDATE()) > @maxAge";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@maxAge", maxAgeHours);
                    repaired = await cmd.ExecuteNonQueryAsync();
                }

                // Also repair in SQLite
                using (var conn = new SqliteConnection(_sqliteConnStr))
                {
                    await conn.OpenAsync();
                    string sql = @"UPDATE VehicleSessions 
                                   SET TrangThai = 'OrphanClosed', ThoiGianRa = datetime('now')
                                   WHERE ThoiGianRa IS NULL AND TrangThai = 'Active'
                                   AND (julianday('now') - julianday(ThoiGianVao)) * 24 > @maxAge";
                    using var cmd = new SqliteCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@maxAge", maxAgeHours);
                    repaired += await cmd.ExecuteNonQueryAsync();
                }

                if (repaired > 0)
                    LoggingService.Instance.LogWarning("SESSION_STATE", "RepairOrphans", $"Closed {repaired} orphan sessions (>{maxAgeHours}h old).");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_STATE", "RepairOrphans", "Failed", ex);
            }
            return repaired;
        }

        // ──────────────────────────────────────────────
        // WRITE Methods (Online/Offline-aware)
        // ──────────────────────────────────────────────

        private async Task<bool> WriteSessionAsync(VehicleSession session)
        {
            // Try SQL Server first
            if (ConnectivityStateService.Instance.IsOnline && !ConnectivityStateService.Instance.IsSimulatingOffline)
            {
                try
                {
                    string connStr = ConnectionManager.Instance.CurrentConnectionString;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    string sql = @"INSERT INTO dbo.VehicleSessions 
                                   (CardId, BienSo, ThoiGianVao, TrangThai, SiteId, ZoneId, EntryLaneId, EntryReaderId, CreatedUtc)
                                   VALUES (@cardId, @plate, @time, 'Active', @siteId, @zoneId, @laneId, @readerId, GETUTCDATE())";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@cardId", (object?)session.CardId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@plate", string.IsNullOrEmpty(session.BienSo) ? DBNull.Value : session.BienSo);
                    cmd.Parameters.AddWithValue("@time", session.ThoiGianVao);
                    cmd.Parameters.AddWithValue("@siteId", (object?)session.SiteId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@zoneId", (object?)session.ZoneId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@laneId", (object?)session.EntryLaneId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@readerId", (object?)session.EntryReaderId ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();

                    // Also mirror to SQLite for offline resilience
                    await WriteSessionToSqliteAsync(session);
                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogWarning("SESSION_STATE", "WriteSession", $"SQL failed, writing to SQLite only: {ex.Message}");
                }
            }

            // Fallback: write to SQLite only + queue for sync
            await WriteSessionToSqliteAsync(session);
            await OfflineQueueService.Instance.EnqueueAsync("INSERT_SESSION", session);
            return true;
        }

        private async Task<bool> UpdateSessionCloseAsync(VehicleSession session)
        {
            // Try SQL Server first
            if (ConnectivityStateService.Instance.IsOnline && !ConnectivityStateService.Instance.IsSimulatingOffline)
            {
                try
                {
                    string connStr = ConnectionManager.Instance.CurrentConnectionString;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    string sql = @"UPDATE dbo.VehicleSessions 
                                   SET ThoiGianRa = @timeOut, ExitLaneId = @exitLane, ExitReaderId = @exitReader,
                                       Tien = @fee, TrangThai = 'Closed'
                                   WHERE CardId = @cardId AND ThoiGianRa IS NULL AND TrangThai = 'Active'";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@timeOut", session.ThoiGianRa ?? DateTime.Now);
                    cmd.Parameters.AddWithValue("@exitLane", (object?)session.ExitLaneId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@exitReader", (object?)session.ExitReaderId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fee", (object?)session.Tien ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cardId", session.CardId);
                    await cmd.ExecuteNonQueryAsync();

                    // Also update SQLite mirror
                    await CloseSessionInSqliteAsync(session);
                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogWarning("SESSION_STATE", "CloseSession", $"SQL failed, updating SQLite only: {ex.Message}");
                }
            }

            // Fallback
            await CloseSessionInSqliteAsync(session);
            await OfflineQueueService.Instance.EnqueueAsync("CLOSE_SESSION", session);
            return true;
        }

        // ──────────────────────────────────────────────
        // SQLite Helpers
        // ──────────────────────────────────────────────

        private async Task WriteSessionToSqliteAsync(VehicleSession session)
        {
            try
            {
                using var conn = new SqliteConnection(_sqliteConnStr);
                await conn.OpenAsync();
                string sql = @"INSERT OR IGNORE INTO VehicleSessions 
                               (CardId, BienSo, ThoiGianVao, TrangThai, SiteId, ZoneId, EntryLaneId, EntryReaderId, CreatedUtc)
                               VALUES (@cardId, @plate, @time, 'Active', @siteId, @zoneId, @laneId, @readerId, @created)";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cardId", (object?)session.CardId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@plate", string.IsNullOrEmpty(session.BienSo) ? (object)DBNull.Value : session.BienSo);
                cmd.Parameters.AddWithValue("@time", session.ThoiGianVao.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@siteId", (object?)session.SiteId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@zoneId", (object?)session.ZoneId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@laneId", (object?)session.EntryLaneId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@readerId", (object?)session.EntryReaderId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_STATE", "WriteSQLite", "Failed", ex);
            }
        }

        private async Task CloseSessionInSqliteAsync(VehicleSession session)
        {
            try
            {
                using var conn = new SqliteConnection(_sqliteConnStr);
                await conn.OpenAsync();
                string sql = @"UPDATE VehicleSessions 
                               SET ThoiGianRa = @timeOut, ExitLaneId = @exitLane, ExitReaderId = @exitReader,
                                   Tien = @fee, TrangThai = 'Closed'
                               WHERE CardId = @cardId AND ThoiGianRa IS NULL AND TrangThai = 'Active'";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@timeOut", (session.ThoiGianRa ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@exitLane", (object?)session.ExitLaneId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@exitReader", (object?)session.ExitReaderId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fee", (object?)session.Tien ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cardId", session.CardId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_STATE", "CloseSQLite", "Failed", ex);
            }
        }

        private async Task<VehicleSession?> GetActiveSessionFromSqliteAsync(int cardId)
        {
            try
            {
                using var conn = new SqliteConnection(_sqliteConnStr);
                await conn.OpenAsync();
                string sql = @"SELECT Id, CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai,
                                      SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, CreatedUtc
                               FROM VehicleSessions
                               WHERE CardId = @cardId AND ThoiGianRa IS NULL AND TrangThai = 'Active'
                               ORDER BY ThoiGianVao DESC LIMIT 1";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cardId", cardId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) return ReadSessionFromSqlite(r);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_STATE", "GetActiveSQLite", "Failed", ex);
            }
            return null;
        }

        private async Task<VehicleSession?> GetActiveSessionByPlateFromSqliteAsync(string plate)
        {
            try
            {
                using var conn = new SqliteConnection(_sqliteConnStr);
                await conn.OpenAsync();
                string sql = @"SELECT Id, CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai,
                                      SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, CreatedUtc
                               FROM VehicleSessions
                               WHERE BienSo = @plate AND ThoiGianRa IS NULL AND TrangThai = 'Active'
                               ORDER BY ThoiGianVao DESC LIMIT 1";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@plate", plate);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) return ReadSessionFromSqlite(r);
            }
            catch { }
            return null;
        }

        private async Task<List<VehicleSession>> GetActiveSessionsFilteredAsync(string filterColumn, int filterValue)
        {
            var list = new List<VehicleSession>();

            // Validate column name (prevent SQL injection)
            if (filterColumn != "ZoneId" && filterColumn != "SiteId") return list;

            if (ConnectivityStateService.Instance.IsOnline && !ConnectivityStateService.Instance.IsSimulatingOffline)
            {
                try
                {
                    string connStr = ConnectionManager.Instance.CurrentConnectionString;
                    using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    string sql = $@"SELECT Id, CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai,
                                           SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, CreatedUtc
                                    FROM dbo.VehicleSessions
                                    WHERE {filterColumn} = @val AND ThoiGianRa IS NULL AND TrangThai = 'Active'
                                    ORDER BY ThoiGianVao DESC";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@val", filterValue);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync()) list.Add(ReadSessionFromSql(r));
                    return list;
                }
                catch { }
            }

            // Fallback SQLite
            try
            {
                using var conn = new SqliteConnection(_sqliteConnStr);
                await conn.OpenAsync();
                string sql = $@"SELECT Id, CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai,
                                       SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, CreatedUtc
                                FROM VehicleSessions
                                WHERE {filterColumn} = @val AND ThoiGianRa IS NULL AND TrangThai = 'Active'
                                ORDER BY ThoiGianVao DESC";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@val", filterValue);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) list.Add(ReadSessionFromSqlite(r));
            }
            catch { }
            return list;
        }

        // ──────────────────────────────────────────────
        // Reader Helpers
        // ──────────────────────────────────────────────

        private VehicleSession ReadSessionFromSql(SqlDataReader r)
        {
            return new VehicleSession
            {
                Id = r.GetInt32(0),
                CardId = r.IsDBNull(1) ? null : r.GetInt32(1),
                BienSo = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                ThoiGianVao = r.GetDateTime(3),
                ThoiGianRa = r.IsDBNull(4) ? null : r.GetDateTime(4),
                Tien = r.IsDBNull(5) ? null : Convert.ToDouble(r.GetDecimal(5)),
                TrangThai = r.IsDBNull(6) ? "Active" : r.GetString(6),
                SiteId = r.IsDBNull(7) ? null : r.GetInt32(7),
                ZoneId = r.IsDBNull(8) ? null : r.GetInt32(8),
                EntryLaneId = r.IsDBNull(9) ? null : r.GetInt32(9),
                ExitLaneId = r.IsDBNull(10) ? null : r.GetInt32(10),
                EntryReaderId = r.IsDBNull(11) ? null : r.GetInt32(11),
                ExitReaderId = r.IsDBNull(12) ? null : r.GetInt32(12),
                CreatedUtc = r.IsDBNull(13) ? DateTime.UtcNow : r.GetDateTime(13)
            };
        }

        private VehicleSession ReadSessionFromSqlite(Microsoft.Data.Sqlite.SqliteDataReader r)
        {
            return new VehicleSession
            {
                Id = r.GetInt32(0),
                CardId = r.IsDBNull(1) ? null : r.GetInt32(1),
                BienSo = r.IsDBNull(2) ? string.Empty : r.GetString(2),
                ThoiGianVao = r.IsDBNull(3) ? DateTime.Now : DateTime.Parse(r.GetString(3)),
                ThoiGianRa = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                Tien = r.IsDBNull(5) ? null : r.GetDouble(5),
                TrangThai = r.IsDBNull(6) ? "Active" : r.GetString(6),
                SiteId = r.IsDBNull(7) ? null : r.GetInt32(7),
                ZoneId = r.IsDBNull(8) ? null : r.GetInt32(8),
                EntryLaneId = r.IsDBNull(9) ? null : r.GetInt32(9),
                ExitLaneId = r.IsDBNull(10) ? null : r.GetInt32(10),
                EntryReaderId = r.IsDBNull(11) ? null : r.GetInt32(11),
                ExitReaderId = r.IsDBNull(12) ? null : r.GetInt32(12),
                CreatedUtc = r.IsDBNull(13) ? DateTime.UtcNow : DateTime.Parse(r.GetString(13))
            };
        }
    }
}
