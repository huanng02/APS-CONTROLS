using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public sealed class DatabaseSyncService
    {
        private static readonly Lazy<DatabaseSyncService> _lazy =
            new Lazy<DatabaseSyncService>(() => new DatabaseSyncService());

        public static DatabaseSyncService Instance => _lazy.Value;

        private const int SyncIntervalSeconds = 3;
        private CancellationTokenSource _cts;
        private readonly object _lock = new object();
        private bool _isRunning = false;

        private DatabaseSyncService() { }

        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning) return;
                _isRunning = true;

                _cts = new CancellationTokenSource();
                Task.Run(() => SyncLoopAsync(_cts.Token));
                LoggingService.Instance.LogInfo("SYNC_SERVICE", "Service", "DatabaseSyncService started successfully.");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _cts?.Cancel();
                LoggingService.Instance.LogInfo("SYNC_SERVICE", "Service", "DatabaseSyncService stopped.");
            }
        }

        private async Task SyncLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var config = ConnectionManager.Instance.CurrentConfig;
                    if (config == null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(SyncIntervalSeconds), token);
                        continue;
                    }

                    string primaryConnStr = config.BuildConnectionString(timeout: 3);
                    string secondaryConnStr = config.BuildSecondaryConnectionString(timeout: 3);

                    if (string.IsNullOrWhiteSpace(secondaryConnStr) || string.IsNullOrWhiteSpace(primaryConnStr))
                    {
                        // Secondary server not configured, skip sync
                        await Task.Delay(TimeSpan.FromSeconds(SyncIntervalSeconds), token);
                        continue;
                    }

                    // 1. Check if both servers are online
                    bool primaryOnline = await TestConnectionAsync(primaryConnStr, token);
                    bool secondaryOnline = await TestConnectionAsync(secondaryConnStr, token);

                    if (primaryOnline && secondaryOnline)
                    {
                        // 2. Perform synchronization
                        await PerformSyncAsync(primaryConnStr, secondaryConnStr, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SYNC_SERVICE", "Loop", "Error in database sync loop", ex);
                }

                await Task.Delay(TimeSpan.FromSeconds(SyncIntervalSeconds), token);
            }
        }

        private async Task<bool> TestConnectionAsync(string connStr, CancellationToken token)
        {
            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync(token);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task PerformSyncAsync(string primaryConnStr, string secondaryConnStr, CancellationToken token)
        {
            try
            {
                // A. Cleanup exited cars from active inventory on both sides using matching Entry Time
                string cleanupSql = @"
                    DELETE FROM XeTrongBai 
                    WHERE EXISTS (
                        SELECT 1 FROM LichSuXe 
                        WHERE LichSuXe.CardId = XeTrongBai.CardId 
                          AND LichSuXe.ThoiGianVao = XeTrongBai.ThoiGianVao 
                          AND LichSuXe.ThoiGianRa IS NOT NULL
                    )";
                await ExecuteNonQueryAsync(primaryConnStr, cleanupSql, token);
                await ExecuteNonQueryAsync(secondaryConnStr, cleanupSql, token);

                // B. Bidirectional Sync for LichSuXe (Transactions)
                string selectLichSu = @"
                    SELECT CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai, AnhVao, AnhRa, 
                           LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, 
                           ExitReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId 
                    FROM LichSuXe";

                DataTable pLichSu = await GetSqlDataAsync(primaryConnStr, selectLichSu, token);
                DataTable sLichSu = await GetSqlDataAsync(secondaryConnStr, selectLichSu, token);

                if (pLichSu != null && sLichSu != null)
                {
                    // Primary to Secondary
                    foreach (DataRow pRow in pLichSu.Rows)
                    {
                        if (token.IsCancellationRequested) return;
                        if (pRow["CardId"] == DBNull.Value || pRow["ThoiGianVao"] == DBNull.Value) continue;

                        int cardId = Convert.ToInt32(pRow["CardId"]);
                        DateTime timeVao = Convert.ToDateTime(pRow["ThoiGianVao"]);

                        if (!ContainsRecord(sLichSu, cardId, timeVao))
                        {
                            LoggingService.Instance.LogInfo("SYNC_SERVICE", "Sync", $"Copying transaction CardId {cardId} (Vào: {timeVao}) from Primary -> Secondary");
                            await InsertLichSuRecordAsync(secondaryConnStr, pRow, token);
                        }
                    }

                    // Secondary to Primary
                    foreach (DataRow sRow in sLichSu.Rows)
                    {
                        if (token.IsCancellationRequested) return;
                        if (sRow["CardId"] == DBNull.Value || sRow["ThoiGianVao"] == DBNull.Value) continue;

                        int cardId = Convert.ToInt32(sRow["CardId"]);
                        DateTime timeVao = Convert.ToDateTime(sRow["ThoiGianVao"]);

                        if (!ContainsRecord(pLichSu, cardId, timeVao))
                        {
                            LoggingService.Instance.LogInfo("SYNC_SERVICE", "Sync", $"Copying transaction CardId {cardId} (Vào: {timeVao}) from Secondary -> Primary");
                            await InsertLichSuRecordAsync(primaryConnStr, sRow, token);
                        }
                    }
                }

                // C. Bidirectional Sync for XeTrongBai (Active Inventory)
                string selectInventory = @"
                    SELECT CardId, BienSo, ThoiGianVao, AnhXe, ThoiGianRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, 
                           EntryLaneId, EntryReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, 
                           ExitControllerId, ExitLaneId 
                    FROM XeTrongBai";

                DataTable pInventory = await GetSqlDataAsync(primaryConnStr, selectInventory, token);
                DataTable sInventory = await GetSqlDataAsync(secondaryConnStr, selectInventory, token);

                if (pInventory != null && sInventory != null)
                {
                    // Primary to Secondary
                    foreach (DataRow pRow in pInventory.Rows)
                    {
                        if (token.IsCancellationRequested) return;
                        if (pRow["CardId"] == DBNull.Value) continue;

                        int cardId = Convert.ToInt32(pRow["CardId"]);
                        if (!ContainsInventoryRecord(sInventory, cardId))
                        {
                            LoggingService.Instance.LogInfo("SYNC_SERVICE", "Sync", $"Copying parked vehicle CardId {cardId} (Biển: {pRow["BienSo"]}) from Primary -> Secondary");
                            await InsertInventoryRecordAsync(secondaryConnStr, pRow, token);
                        }
                    }

                    // Secondary to Primary
                    foreach (DataRow sRow in sInventory.Rows)
                    {
                        if (token.IsCancellationRequested) return;
                        if (sRow["CardId"] == DBNull.Value) continue;

                        int cardId = Convert.ToInt32(sRow["CardId"]);
                        if (!ContainsInventoryRecord(pInventory, cardId))
                        {
                            LoggingService.Instance.LogInfo("SYNC_SERVICE", "Sync", $"Copying parked vehicle CardId {cardId} (Biển: {sRow["BienSo"]}) from Secondary -> Primary");
                            await InsertInventoryRecordAsync(primaryConnStr, sRow, token);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SYNC_SERVICE", "PerformSync", "Bidirectional sync failed", ex);
            }
        }

        private bool ContainsRecord(DataTable dt, int cardId, DateTime timeVao)
        {
            foreach (DataRow row in dt.Rows)
            {
                int rowCardId = Convert.ToInt32(row["CardId"]);
                DateTime rowTimeVao = Convert.ToDateTime(row["ThoiGianVao"]);
                if (rowCardId == cardId && Math.Abs((rowTimeVao - timeVao).TotalSeconds) < 1.0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ContainsInventoryRecord(DataTable dt, int cardId)
        {
            foreach (DataRow row in dt.Rows)
            {
                if (Convert.ToInt32(row["CardId"]) == cardId)
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<DataTable> GetSqlDataAsync(string connStr, string query, CancellationToken token)
        {
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync(token);
                using (var cmd = new SqlCommand(query, conn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        private async Task ExecuteNonQueryAsync(string connStr, string query, CancellationToken token)
        {
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync(token);
                using (var cmd = new SqlCommand(query, conn))
                {
                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        private async Task InsertLichSuRecordAsync(string connStr, DataRow row, CancellationToken token)
        {
            string insertSql = @"
                INSERT INTO LichSuXe (CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai, AnhVao, AnhRa, 
                                      LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, 
                                      ExitReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId)
                VALUES (@CardId, @BienSo, @ThoiGianVao, @ThoiGianRa, @Tien, @TrangThai, @AnhVao, @AnhRa, 
                        @LoaiXeId, @LoaiVeId, @SiteId, @ZoneId, @EntryLaneId, @ExitLaneId, @EntryReaderId, 
                        @ExitReaderId, @EntryWorkstationId, @ExitWorkstationId, @EntryControllerId, @ExitControllerId)";

            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync(token);
                using (var cmd = new SqlCommand(insertSql, conn))
                {
                    AddParam(cmd, "@CardId", row["CardId"]);
                    AddParam(cmd, "@BienSo", row["BienSo"]);
                    AddParam(cmd, "@ThoiGianVao", row["ThoiGianVao"]);
                    AddParam(cmd, "@ThoiGianRa", row["ThoiGianRa"]);
                    AddParam(cmd, "@Tien", row["Tien"]);
                    AddParam(cmd, "@TrangThai", row["TrangThai"]);
                    AddParam(cmd, "@AnhVao", row["AnhVao"]);
                    AddParam(cmd, "@AnhRa", row["AnhRa"]);
                    AddParam(cmd, "@LoaiXeId", row["LoaiXeId"]);
                    AddParam(cmd, "@LoaiVeId", row["LoaiVeId"]);
                    AddParam(cmd, "@SiteId", row["SiteId"]);
                    AddParam(cmd, "@ZoneId", row["ZoneId"]);
                    AddParam(cmd, "@EntryLaneId", row["EntryLaneId"]);
                    AddParam(cmd, "@ExitLaneId", row["ExitLaneId"]);
                    AddParam(cmd, "@EntryReaderId", row["EntryReaderId"]);
                    AddParam(cmd, "@ExitReaderId", row["ExitReaderId"]);
                    AddParam(cmd, "@EntryWorkstationId", row["EntryWorkstationId"]);
                    AddParam(cmd, "@ExitWorkstationId", row["ExitWorkstationId"]);
                    AddParam(cmd, "@EntryControllerId", row["EntryControllerId"]);
                    AddParam(cmd, "@ExitControllerId", row["ExitControllerId"]);

                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        private async Task InsertInventoryRecordAsync(string connStr, DataRow row, CancellationToken token)
        {
            string insertSql = @"
                INSERT INTO XeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe, ThoiGianRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, 
                                        EntryLaneId, EntryReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, 
                                        ExitControllerId, ExitLaneId)
                VALUES (@CardId, @BienSo, @ThoiGianVao, @AnhXe, @ThoiGianRa, @LoaiXeId, @LoaiVeId, @SiteId, @ZoneId, 
                        @EntryLaneId, @EntryReaderId, @EntryWorkstationId, @ExitWorkstationId, @EntryControllerId, 
                        @ExitControllerId, @ExitLaneId)";

            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync(token);
                using (var cmd = new SqlCommand(insertSql, conn))
                {
                    AddParam(cmd, "@CardId", row["CardId"]);
                    AddParam(cmd, "@BienSo", row["BienSo"]);
                    AddParam(cmd, "@ThoiGianVao", row["ThoiGianVao"]);
                    AddParam(cmd, "@AnhXe", row["AnhXe"]);
                    AddParam(cmd, "@ThoiGianRa", row["ThoiGianRa"]);
                    AddParam(cmd, "@LoaiXeId", row["LoaiXeId"]);
                    AddParam(cmd, "@LoaiVeId", row["LoaiVeId"]);
                    AddParam(cmd, "@SiteId", row["SiteId"]);
                    AddParam(cmd, "@ZoneId", row["ZoneId"]);
                    AddParam(cmd, "@EntryLaneId", row["EntryLaneId"]);
                    AddParam(cmd, "@EntryReaderId", row["EntryReaderId"]);
                    AddParam(cmd, "@EntryWorkstationId", row["EntryWorkstationId"]);
                    AddParam(cmd, "@ExitWorkstationId", row["ExitWorkstationId"]);
                    AddParam(cmd, "@EntryControllerId", row["EntryControllerId"]);
                    AddParam(cmd, "@ExitControllerId", row["ExitControllerId"]);
                    AddParam(cmd, "@ExitLaneId", row["ExitLaneId"]);

                    await cmd.ExecuteNonQueryAsync(token);
                }
            }
        }

        private void AddParam(SqlCommand cmd, string paramName, object value)
        {
            cmd.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
        }
    }
}
