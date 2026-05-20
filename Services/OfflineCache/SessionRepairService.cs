using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class SessionRepairService
    {
        private static readonly Lazy<SessionRepairService> _lazy = new(() => new SessionRepairService());
        public static SessionRepairService Instance => _lazy.Value;

        private readonly string _dbPath;
        private readonly string _connectionString;

        private SessionRepairService()
        {
            _dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
            _connectionString = $"Data Source={_dbPath};Default Timeout=5;";
        }

        public async Task<int> RepairAllInconsistenciesAsync()
        {
            int repairedCount = 0;

            // 1. Repair duplicate active cards
            repairedCount += await RepairDuplicateActiveCardsAsync();

            // 2. Repair corrupted sessions (missing entry time, etc.)
            repairedCount += await RepairCorruptedSessionsAsync();

            // 3. Reset stalled or suspicious transactions in queue
            repairedCount += await RepairStalledQueueAsync();

            return repairedCount;
        }

        public async Task<int> RepairDuplicateActiveCardsAsync()
        {
            int count = 0;
            try
            {
                var duplicates = await SessionConsistencyService.Instance.DetectDuplicateActiveCardsAsync();
                foreach (var dup in duplicates)
                {
                    using var conn = new SqliteConnection(_connectionString);
                    await conn.OpenAsync();

                    using var tx = conn.BeginTransaction();
                    var auditsToLog = new List<(string CardId, string EventType, string OldState, string NewState, string Message)>();
                    try
                    {
                        // Strategy: Keep the newest ACTIVE session (newest ThoiGianVao)
                        // Archive/Delete others.
                        string selectSql = @"
                            SELECT Id, ThoiGianVao 
                            FROM LocalXeTrongBai 
                            WHERE CardId = @cardId 
                            ORDER BY ThoiGianVao DESC";

                        var sessions = new List<(long RowId, DateTime Time)>();
                        using (var cmd = new SqliteCommand(selectSql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@cardId", dup.CardId);
                            using var reader = await cmd.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                sessions.Add((reader.GetInt64(0), reader.GetDateTime(1)));
                            }
                        }

                        if (sessions.Count > 1)
                        {
                            // Newest is at index 0. We delete/archive the others.
                            for (int i = 1; i < sessions.Count; i++)
                            {
                                var oldSession = sessions[i];

                                // Log before deleting (collected to write after commit)
                                auditsToLog.Add((
                                    dup.CardId.ToString(),
                                    "REPAIR",
                                    "DuplicateActive",
                                    "DeletedOldDuplicate",
                                    $"Removed old duplicate session with entry time: {oldSession.Time}"
                                ));

                                string deleteSql = "DELETE FROM LocalXeTrongBai WHERE Id = @rowId";
                                using (var delCmd = new SqliteCommand(deleteSql, conn, tx))
                                {
                                    delCmd.Parameters.AddWithValue("@rowId", oldSession.RowId);
                                    await delCmd.ExecuteNonQueryAsync();
                                }

                                count++;
                                Console.WriteLine($"[SESSION REPAIR] Action=ArchiveOldSession CardUID={dup.CardId} Removed entry from {oldSession.Time}");
                            }
                        }

                        await tx.CommitAsync();

                        // Perform audit logging outside transaction to prevent deadlocks
                        foreach (var audit in auditsToLog)
                        {
                            await SessionAuditService.Instance.LogAuditAsync(audit.CardId, audit.EventType, audit.OldState, audit.NewState, audit.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync();
                        LoggingService.Instance.LogError("SESSION_REPAIR", "RepairDuplicateActiveCardsAsync", $"Failed to repair card {dup.CardId}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_REPAIR", "RepairDuplicateActiveCardsAsync", "Repair failed", ex);
            }
            return count;
        }

        public async Task<int> RepairCorruptedSessionsAsync()
        {
            int count = 0;
            try
            {
                var corrupted = await SessionConsistencyService.Instance.DetectCorruptedSessionsAsync();
                foreach (var corr in corrupted)
                {
                    using var conn = new SqliteConnection(_connectionString);
                    await conn.OpenAsync();

                    using var tx = conn.BeginTransaction();
                    var auditsToLog = new List<(string CardId, string EventType, string OldState, string NewState, string Message)>();
                    try
                    {
                        // Strategy: assign default entry time if missing (fallback to current time minus 5 mins or similar)
                        if (corr.IssueType == "MissingEntryTime")
                        {
                            DateTime backupTime = DateTime.UtcNow.AddMinutes(-5);

                            auditsToLog.Add((
                                corr.CardId.ToString(),
                                "REPAIR",
                                "MissingEntryTime",
                                "TimeAssigned",
                                $"Assigned recovery entry time: {backupTime}"
                            ));

                            string updateSql = "UPDATE LocalXeTrongBai SET ThoiGianVao = @time WHERE CardId = @cardId";
                            using (var cmd = new SqliteCommand(updateSql, conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@time", backupTime);
                                cmd.Parameters.AddWithValue("@cardId", corr.CardId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            count++;
                            Console.WriteLine($"[SESSION REPAIR] Action=AssignTimestamp CardUID={corr.CardId}");
                        }

                        await tx.CommitAsync();

                        // Perform audit logging outside transaction to prevent deadlocks
                        foreach (var audit in auditsToLog)
                        {
                            await SessionAuditService.Instance.LogAuditAsync(audit.CardId, audit.EventType, audit.OldState, audit.NewState, audit.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync();
                        LoggingService.Instance.LogError("SESSION_REPAIR", "RepairCorruptedSessionsAsync", $"Failed to repair card {corr.CardId}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_REPAIR", "RepairCorruptedSessionsAsync", "Repair failed", ex);
            }
            return count;
        }

        private async Task<int> RepairStalledQueueAsync()
        {
            int count = 0;
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                // If a transaction has been failed more than 10 times, let's log and reset the retry count or mark suspicious
                string sql = "UPDATE PendingTransactions SET RetryCount = 0 WHERE SyncStatus = 'Failed' AND RetryCount > 10";
                using var cmd = new SqliteCommand(sql, conn);
                count = await cmd.ExecuteNonQueryAsync();

                if (count > 0)
                {
                    Console.WriteLine($"[SESSION REPAIR] Action=ResetStalledQueue Count={count}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_REPAIR", "RepairStalledQueueAsync", "Failed", ex);
            }
            return count;
        }
    }
}
