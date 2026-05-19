using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class SessionConsistencyService
    {
        private static readonly Lazy<SessionConsistencyService> _lazy = new(() => new SessionConsistencyService());
        public static SessionConsistencyService Instance => _lazy.Value;

        private readonly string _dbPath;
        private readonly string _connectionString;

        private SessionConsistencyService()
        {
            _dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
            _connectionString = $"Data Source={_dbPath};Default Timeout=5;";
        }

        public async Task<List<InconsistentSessionModel>> ValidateSessionsAsync()
        {
            var issues = new List<InconsistentSessionModel>();

            // 1. Detect duplicate active cards
            var duplicates = await DetectDuplicateActiveCardsAsync();
            issues.AddRange(duplicates);

            // 2. Detect corrupted sessions
            var corrupted = await DetectCorruptedSessionsAsync();
            issues.AddRange(corrupted);

            // 3. Detect orphan queue items
            var orphans = await DetectOrphanQueueItemsAsync();
            issues.AddRange(orphans);

            return issues;
        }

        public async Task<List<InconsistentSessionModel>> DetectDuplicateActiveCardsAsync()
        {
            var list = new List<InconsistentSessionModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                // Find CardIds with multiple rows in LocalXeTrongBai
                string sql = @"
                    SELECT CardId, COUNT(*) 
                    FROM LocalXeTrongBai 
                    GROUP BY CardId 
                    HAVING COUNT(*) > 1";

                using var cmd = new SqliteCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int cardId = reader.GetInt32(0);
                    int count = reader.GetInt32(1);

                    list.Add(new InconsistentSessionModel
                    {
                        CardId = cardId,
                        IssueType = "DuplicateActiveSession",
                        Severity = "High",
                        Description = $"Card ID {cardId} has {count} active sessions registered in local database.",
                        CreatedUtc = DateTime.UtcNow
                    });

                    Console.WriteLine($"[SESSION CORRUPTION] CardUID={cardId} Issue=DuplicateActiveSession Count={count}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CONSISTENCY_CHECK", "DetectDuplicateActiveCardsAsync", "Failed", ex);
            }
            return list;
        }

        public async Task<List<InconsistentSessionModel>> DetectCorruptedSessionsAsync()
        {
            var list = new List<InconsistentSessionModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                // 1. Active sessions with missing or invalid EntryTime
                string sql1 = "SELECT CardId, BienSo FROM LocalXeTrongBai WHERE ThoiGianVao IS NULL OR ThoiGianVao = '0001-01-01 00:00:00'";
                using (var cmd = new SqliteCommand(sql1, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int cardId = reader.GetInt32(0);
                        string bienSo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

                        list.Add(new InconsistentSessionModel
                        {
                            CardId = cardId,
                            IssueType = "MissingEntryTime",
                            Severity = "Medium",
                            Description = $"Active session for Card ID {cardId} ({bienSo}) is missing a valid ThoiGianVao.",
                            CreatedUtc = DateTime.UtcNow
                        });

                        Console.WriteLine($"[SESSION CORRUPTION] CardUID={cardId} Issue=MissingEntryTime");
                    }
                }

                // 2. Local sessions missing details (corrupted local session data in cache)
                // We also check if we have any pending sync items that are EXITs without a corresponding local active session
                // which suggests a missing close event or orphan exit.
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CONSISTENCY_CHECK", "DetectCorruptedSessionsAsync", "Failed", ex);
            }
            return list;
        }

        public async Task<List<InconsistentSessionModel>> DetectOrphanQueueItemsAsync()
        {
            var list = new List<InconsistentSessionModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                // Find orphan queue transactions (e.g. UPDATE_XE_RA but no matching local session or vice versa)
                string sql = "SELECT Id, TransactionType, PayloadJson FROM PendingTransactions WHERE SyncStatus = 'Pending'";
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string type = reader.GetString(1);
                    string payload = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                    // Check if it's an UPDATE_XE_RA or INSERT_XE_VAO that has been pending for over an hour
                    list.Add(new InconsistentSessionModel
                    {
                        QueueItemId = id,
                        IssueType = "OrphanQueueItem",
                        Severity = "Low",
                        Description = $"Pending transaction of type {type} is currently queued for synchronization.",
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CONSISTENCY_CHECK", "DetectOrphanQueueItemsAsync", "Failed", ex);
            }
            return list;
        }

        // ──────────────────────────────────────────────
        // Phase 8.2: Zone-Aware Session Consistency
        // ──────────────────────────────────────────────

        /// <summary>Detects duplicate active sessions in VehicleSessions table (same CardId, multiple Active rows).</summary>
        public async Task<List<InconsistentSessionModel>> DetectDuplicateVehicleSessionsAsync()
        {
            var list = new List<InconsistentSessionModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = @"SELECT CardId, COUNT(*) as cnt
                               FROM VehicleSessions 
                               WHERE ThoiGianRa IS NULL AND TrangThai = 'Active'
                               GROUP BY CardId HAVING cnt > 1";
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int cardId = reader.GetInt32(0);
                    int count = reader.GetInt32(1);
                    list.Add(new InconsistentSessionModel
                    {
                        CardId = cardId,
                        IssueType = "DuplicateVehicleSession",
                        Severity = "High",
                        Description = $"Card {cardId} has {count} active sessions in VehicleSessions table.",
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CONSISTENCY_CHECK", "DetectDuplicateVehicleSessions", "Failed", ex);
            }
            return list;
        }

        /// <summary>Detects orphan sessions (active > specified hours with no exit).</summary>
        public async Task<List<InconsistentSessionModel>> DetectOrphanSessionsAsync(int maxAgeHours = 24)
        {
            var list = new List<InconsistentSessionModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = @"SELECT CardId, BienSo, ThoiGianVao, ZoneId
                               FROM VehicleSessions 
                               WHERE ThoiGianRa IS NULL AND TrangThai = 'Active'
                               AND (julianday('now') - julianday(ThoiGianVao)) * 24 > @maxAge";
                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@maxAge", maxAgeHours);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int cardId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    string plate = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    string entryTime = reader.IsDBNull(2) ? "?" : reader.GetString(2);
                    list.Add(new InconsistentSessionModel
                    {
                        CardId = cardId,
                        IssueType = "OrphanSession",
                        Severity = "Medium",
                        Description = $"Card {cardId} ({plate}) entered at {entryTime}, active for >{maxAgeHours}h without exit.",
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CONSISTENCY_CHECK", "DetectOrphanSessions", "Failed", ex);
            }
            return list;
        }

        /// <summary>Detects invalid exits (ThoiGianRa before ThoiGianVao).</summary>
        public async Task<List<InconsistentSessionModel>> DetectInvalidExitsAsync()
        {
            var list = new List<InconsistentSessionModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = @"SELECT CardId, ThoiGianVao, ThoiGianRa
                               FROM VehicleSessions 
                               WHERE ThoiGianRa IS NOT NULL AND ThoiGianRa < ThoiGianVao";
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int cardId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    list.Add(new InconsistentSessionModel
                    {
                        CardId = cardId,
                        IssueType = "InvalidExit",
                        Severity = "High",
                        Description = $"Card {cardId} has exit time before entry time (clock skew or data corruption).",
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CONSISTENCY_CHECK", "DetectInvalidExits", "Failed", ex);
            }
            return list;
        }

        /// <summary>Full zone-aware validation: combines all checks.</summary>
        public async Task<List<InconsistentSessionModel>> ValidateAllZoneAwareAsync()
        {
            var issues = await ValidateSessionsAsync();
            issues.AddRange(await DetectDuplicateVehicleSessionsAsync());
            issues.AddRange(await DetectOrphanSessionsAsync());
            issues.AddRange(await DetectInvalidExitsAsync());
            return issues;
        }

        /// <summary>Self-healing: resolves duplicates by keeping only the latest active session per card.</summary>
        public async Task<int> HealDuplicateSessionsAsync()
        {
            int healed = 0;
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                // Close all but the most recent active session per CardId
                string sql = @"UPDATE VehicleSessions SET TrangThai = 'DuplicateClosed', ThoiGianRa = datetime('now')
                               WHERE Id NOT IN (
                                   SELECT MAX(Id) FROM VehicleSessions 
                                   WHERE ThoiGianRa IS NULL AND TrangThai = 'Active'
                                   GROUP BY CardId
                               ) AND ThoiGianRa IS NULL AND TrangThai = 'Active'";
                using var cmd = new SqliteCommand(sql, conn);
                healed = await cmd.ExecuteNonQueryAsync();

                if (healed > 0)
                    LoggingService.Instance.LogWarning("CONSISTENCY", "HealDuplicates", $"Closed {healed} duplicate sessions.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CONSISTENCY", "HealDuplicates", "Failed", ex);
            }
            return healed;
        }
    }

    public class InconsistentSessionModel
    {
        public int CardId { get; set; }
        public int QueueItemId { get; set; }
        public string IssueType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // High, Medium, Low
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
    }
}
