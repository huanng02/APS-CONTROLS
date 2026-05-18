using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class GarageStateService
    {
        private static readonly Lazy<GarageStateService> _lazy = new(() => new GarageStateService());
        public static GarageStateService Instance => _lazy.Value;

        private readonly string _dbPath;
        private readonly string _connectionString;

        private GarageStateService()
        {
            _dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
            _connectionString = $"Data Source={_dbPath};Default Timeout=5;";
        }

        public async Task<int> GetCurrentVehicleCount()
        {
            int count = 0;
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = "SELECT COUNT(*) FROM LocalXeTrongBai";
                using var cmd = new SqliteCommand(sql, conn);
                count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("GARAGE_STATE", "GetCurrentVehicleCount", "Failed", ex);
            }
            return count;
        }

        public async Task<List<LocalActiveSession>> GetActiveSessions()
        {
            var list = new List<LocalActiveSession>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = "SELECT CardId, BienSo, ThoiGianVao, AnhXe FROM LocalXeTrongBai ORDER BY ThoiGianVao DESC";
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new LocalActiveSession
                    {
                        CardId = reader.GetInt32(0),
                        BienSo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        ThoiGianVao = reader.GetDateTime(2),
                        AnhXe = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("GARAGE_STATE", "GetActiveSessions", "Failed", ex);
            }
            return list;
        }

        public async Task<int> GetPendingSyncCount()
        {
            int count = 0;
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();
                string sql = "SELECT COUNT(*) FROM PendingTransactions WHERE SyncStatus = 'Pending'";
                using var cmd = new SqliteCommand(sql, conn);
                count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("GARAGE_STATE", "GetPendingSyncCount", "Failed", ex);
            }
            return count;
        }

        public async Task<int> GetSuspiciousSessionsCount()
        {
            try
            {
                var corrupted = await SessionConsistencyService.Instance.ValidateSessionsAsync();
                return corrupted.Count;
            }
            catch
            {
                return 0;
            }
        }
    }
}
