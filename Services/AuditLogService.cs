using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public sealed class AuditLogService
    {
        private static readonly Lazy<AuditLogService> _lazy = new(() => new AuditLogService());
        public static AuditLogService Instance => _lazy.Value;

        private readonly AuditLogRepository _repo = new();

        private AuditLogService() { }

        /// <summary>
        /// Logs an audit trail event inside an existing database transaction.
        /// </summary>
        public Task LogAuditAsync(AuditLog log, SqlConnection conn, SqlTransaction transaction)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            return _repo.InsertAuditLogAsync(log, conn, transaction);
        }

        /// <summary>
        /// Logs an audit trail event standalone (no transaction required).
        /// </summary>
        public async Task LogAuditAsync(AuditLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));

            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "INSERT_AUDIT_LOG",
                log,
                async conn =>
                {
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            await _repo.InsertAuditLogAsync(log, conn, transaction).ConfigureAwait(false);
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Fetches audit logs using multiple criteria.
        /// </summary>
        public Task<List<AuditLog>> GetAuditLogsAsync(int? userId, string? actionType, string? entityType, DateTime? startDate, DateTime? endDate, string? search = null)
        {
            AuthorizationGuard.Protect("VIEW_AUDIT_LOG", "View Audit Logs");
            return _repo.GetAuditLogsAsync(userId, actionType, entityType, startDate, endDate, search);
        }

        /// <summary>
        /// Fetches paginated audit logs using multiple criteria.
        /// </summary>
        public Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
            int page, int pageSize, int? userId, string? actionType, string? entityType,
            DateTime? startDate, DateTime? endDate, string? search = null)
        {
            AuthorizationGuard.Protect("VIEW_AUDIT_LOG", "View Audit Logs");
            return _repo.GetAuditLogsPagedAsync(page, pageSize, userId, actionType, entityType, startDate, endDate, search);
        }

        /// <summary>
        /// Fetches a single audit log entry by ID.
        /// </summary>
        public Task<AuditLog?> GetAuditLogByIdAsync(long auditLogId)
        {
            AuthorizationGuard.Protect("VIEW_AUDIT_LOG", "View Audit Log Details");
            return _repo.GetAuditLogByIdAsync(auditLogId);
        }
    }
}
