using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class SessionAuditService
    {
        private static readonly Lazy<SessionAuditService> _lazy = new(() => new SessionAuditService());
        public static SessionAuditService Instance => _lazy.Value;

        private readonly string _dbPath;
        private readonly string _connectionString;

        private SessionAuditService()
        {
            _dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
            _connectionString = $"Data Source={_dbPath};Default Timeout=5;";
        }

        public async Task LogAuditAsync(string sessionId, string eventType, string oldState, string newState, string message)
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                    INSERT INTO session_audit_logs (SessionId, EventType, OldState, NewState, Message, CreatedUtc)
                    VALUES (@sessionId, @eventType, @oldState, @newState, @message, @createdUtc)";

                using var cmd = new SqliteCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sessionId", sessionId ?? string.Empty);
                cmd.Parameters.AddWithValue("@eventType", eventType ?? string.Empty);
                cmd.Parameters.AddWithValue("@oldState", oldState ?? string.Empty);
                cmd.Parameters.AddWithValue("@newState", newState ?? string.Empty);
                cmd.Parameters.AddWithValue("@message", message ?? string.Empty);
                cmd.Parameters.AddWithValue("@createdUtc", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();

                // Format console logs exactly as requested in UI Requirement 12
                Console.WriteLine($"[SESSION {eventType.ToUpper()}] CardUID={sessionId} Old={oldState} New={newState} Msg={message}");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_AUDIT", "LogAuditAsync", "Failed to write session audit log", ex);
            }
        }

        public async Task<List<AuditLogModel>> GetAuditLogsAsync()
        {
            var list = new List<AuditLogModel>();
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "SELECT Id, SessionId, EventType, OldState, NewState, Message, CreatedUtc FROM session_audit_logs ORDER BY CreatedUtc DESC LIMIT 200";
                using var cmd = new SqliteCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new AuditLogModel
                    {
                        Id = reader.GetInt32(0),
                        SessionId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        EventType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        OldState = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        NewState = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Message = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        CreatedUtc = reader.GetDateTime(6)
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SESSION_AUDIT", "GetAuditLogsAsync", "Failed to fetch audit logs", ex);
            }
            return list;
        }
    }

    public class AuditLogModel
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string OldState { get; set; } = string.Empty;
        public string NewState { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
    }
}
