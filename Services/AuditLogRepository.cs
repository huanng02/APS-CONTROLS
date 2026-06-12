using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public sealed class AuditLogRepository
    {
        public async Task InsertAuditLogAsync(AuditLog log, SqlConnection conn, SqlTransaction transaction)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));

            using (var cmd = new SqlCommand(@"
                INSERT INTO dbo.AuditLogs (UserId, Username, ActionType, EntityType, EntityId, OldValue, NewValue, Description, CreatedAt)
                VALUES (@UserId, @Username, @ActionType, @EntityType, @EntityId, @OldValue, @NewValue, @Description, @CreatedAt);", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@UserId", (object?)log.UserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Username", (object?)log.Username ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ActionType", log.ActionType ?? string.Empty);
                cmd.Parameters.AddWithValue("@EntityType", log.EntityType ?? string.Empty);
                cmd.Parameters.AddWithValue("@EntityId", (object?)log.EntityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OldValue", (object?)log.OldValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NewValue", (object?)log.NewValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", (object?)log.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", log.CreatedAt == DateTime.MinValue ? DateTime.UtcNow : log.CreatedAt);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync(int? userId, string? actionType, string? entityType, DateTime? startDate, DateTime? endDate, string? search = null)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<AuditLog>>(
                $"AUDIT_LOGS_{userId}_{actionType}_{entityType}_{startDate}_{endDate}_{search}",
                async conn =>
                {
                    var list = new List<AuditLog>();
                    var sql = new StringBuilder(@"
                        SELECT AuditLogId, UserId, Username, ActionType, EntityType, EntityId, OldValue, NewValue, Description, CreatedAt
                        FROM dbo.AuditLogs
                        WHERE 1=1");

                    using (var cmd = new SqlCommand(string.Empty, conn))
                    {
                        if (userId.HasValue)
                        {
                            sql.Append(" AND UserId = @UserId");
                            cmd.Parameters.AddWithValue("@UserId", userId.Value);
                        }
                        if (!string.IsNullOrEmpty(actionType))
                        {
                            sql.Append(" AND ActionType = @ActionType");
                            cmd.Parameters.AddWithValue("@ActionType", actionType);
                        }
                        if (!string.IsNullOrEmpty(entityType))
                        {
                            sql.Append(" AND EntityType = @EntityType");
                            cmd.Parameters.AddWithValue("@EntityType", entityType);
                        }
                        if (startDate.HasValue)
                        {
                            sql.Append(" AND CreatedAt >= @StartDate");
                            cmd.Parameters.AddWithValue("@StartDate", startDate.Value);
                        }
                        if (endDate.HasValue)
                        {
                            sql.Append(" AND CreatedAt <= @EndDate");
                            cmd.Parameters.AddWithValue("@EndDate", endDate.Value);
                        }
                        if (!string.IsNullOrEmpty(search))
                        {
                            sql.Append(" AND (Username LIKE @Search OR Description LIKE @Search OR EntityId LIKE @Search)");
                            cmd.Parameters.AddWithValue("@Search", $"%{search}%");
                        }

                        sql.Append(" ORDER BY AuditLogId DESC;");
                        cmd.CommandText = sql.ToString();

                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                list.Add(new AuditLog
                                {
                                    AuditLogId = Convert.ToInt64(reader["AuditLogId"]),
                                    UserId = reader["UserId"] != DBNull.Value ? Convert.ToInt32(reader["UserId"]) : null,
                                    Username = reader["Username"]?.ToString(),
                                    ActionType = reader["ActionType"]?.ToString() ?? string.Empty,
                                    EntityType = reader["EntityType"]?.ToString() ?? string.Empty,
                                    EntityId = reader["EntityId"]?.ToString(),
                                    OldValue = reader["OldValue"]?.ToString(),
                                    NewValue = reader["NewValue"]?.ToString(),
                                    Description = reader["Description"]?.ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                                });
                            }
                        }
                    }
                    return list;
                }
            ).ConfigureAwait(false) ?? new List<AuditLog>();
        }

        public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
            int page, int pageSize, int? userId, string? actionType, string? entityType,
            DateTime? startDate, DateTime? endDate, string? search = null)
        {
            int offset = (page - 1) * pageSize;
            
            var where = new StringBuilder(" WHERE 1=1");
            var parameters = new List<SqlParameter>();

            if (userId.HasValue)
            {
                where.Append(" AND UserId = @UserId");
                parameters.Add(new SqlParameter("@UserId", userId.Value));
            }
            if (!string.IsNullOrEmpty(actionType) && actionType != "All")
            {
                where.Append(" AND ActionType = @ActionType");
                parameters.Add(new SqlParameter("@ActionType", actionType));
            }
            if (!string.IsNullOrEmpty(entityType) && entityType != "All")
            {
                where.Append(" AND EntityType = @EntityType");
                parameters.Add(new SqlParameter("@EntityType", entityType));
            }
            if (startDate.HasValue)
            {
                where.Append(" AND CreatedAt >= @StartDate");
                parameters.Add(new SqlParameter("@StartDate", startDate.Value));
            }
            if (endDate.HasValue)
            {
                where.Append(" AND CreatedAt <= @EndDate");
                parameters.Add(new SqlParameter("@EndDate", endDate.Value));
            }
            if (!string.IsNullOrEmpty(search))
            {
                where.Append(" AND (Username LIKE @Search OR Description LIKE @Search OR EntityId LIKE @Search)");
                parameters.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            string countSql = "SELECT COUNT(1) FROM dbo.AuditLogs" + where.ToString();
            string dataSql = $@"
                SELECT AuditLogId, UserId, Username, ActionType, EntityType, EntityId, OldValue, NewValue, Description, CreatedAt
                FROM dbo.AuditLogs
                {where.ToString()}
                ORDER BY AuditLogId DESC
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;";

            var result = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<(List<AuditLog> Items, int TotalCount)>(
                $"AUDIT_LOGS_PAGE_{page}_{pageSize}_{userId}_{actionType}_{entityType}_{startDate}_{endDate}_{search}",
                async conn =>
                {
                    int totalCount = 0;
                    using (var cmd = new SqlCommand(countSql, conn))
                    {
                        foreach (var p in parameters) cmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                        totalCount = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
                    }

                    var items = new List<AuditLog>();
                    using (var cmd = new SqlCommand(dataSql, conn))
                    {
                        foreach (var p in parameters) cmd.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                items.Add(new AuditLog
                                {
                                    AuditLogId = Convert.ToInt64(reader["AuditLogId"]),
                                    UserId = reader["UserId"] != DBNull.Value ? Convert.ToInt32(reader["UserId"]) : null,
                                    Username = reader["Username"]?.ToString(),
                                    ActionType = reader["ActionType"]?.ToString() ?? string.Empty,
                                    EntityType = reader["EntityType"]?.ToString() ?? string.Empty,
                                    EntityId = reader["EntityId"]?.ToString(),
                                    OldValue = reader["OldValue"]?.ToString(),
                                    NewValue = reader["NewValue"]?.ToString(),
                                    Description = reader["Description"]?.ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                                });
                            }
                        }
                    }

                    return (items, totalCount);
                }
            ).ConfigureAwait(false);

            if (result.Items == null)
            {
                return (new List<AuditLog>(), 0);
            }
            return result;
        }

        public async Task<AuditLog?> GetAuditLogByIdAsync(long auditLogId)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<AuditLog>(
                $"AUDIT_LOG_BY_ID_{auditLogId}",
                async conn =>
                {
                    using (var cmd = new SqlCommand("SELECT AuditLogId, UserId, Username, ActionType, EntityType, EntityId, OldValue, NewValue, Description, CreatedAt FROM dbo.AuditLogs WHERE AuditLogId = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", auditLogId);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            if (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                return new AuditLog
                                {
                                    AuditLogId = Convert.ToInt64(reader["AuditLogId"]),
                                    UserId = reader["UserId"] != DBNull.Value ? Convert.ToInt32(reader["UserId"]) : null,
                                    Username = reader["Username"]?.ToString(),
                                    ActionType = reader["ActionType"]?.ToString() ?? string.Empty,
                                    EntityType = reader["EntityType"]?.ToString() ?? string.Empty,
                                    EntityId = reader["EntityId"]?.ToString(),
                                    OldValue = reader["OldValue"]?.ToString(),
                                    NewValue = reader["NewValue"]?.ToString(),
                                    Description = reader["Description"]?.ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                                };
                            }
                        }
                    }
                    return null;
                }
            ).ConfigureAwait(false);
        }
    }
}
