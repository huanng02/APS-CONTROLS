using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public class CardAccessPolicyService
    {
        private static readonly Lazy<CardAccessPolicyService> _lazy = new(() => new CardAccessPolicyService());
        public static CardAccessPolicyService Instance => _lazy.Value;

        private readonly DatabaseService _db = new DatabaseService();

        private List<CardGroupLanePermission>? _cachedLanePerms;
        private List<AccessSchedule>? _cachedSchedules;
        private List<RFIDAccessRule>? _cachedRfidRules;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private readonly object _cacheLock = new object();

        private async Task<(List<CardGroupLanePermission> lanePerms, List<AccessSchedule> schedules, List<RFIDAccessRule> rfidRules)> GetAccessPoliciesWithCacheAsync()
        {
            var now = DateTime.Now;
            lock (_cacheLock)
            {
                if (_cachedLanePerms != null && _cachedSchedules != null && _cachedRfidRules != null && (now - _lastCacheTime).TotalSeconds < 10)
                {
                    return (_cachedLanePerms, _cachedSchedules, _cachedRfidRules);
                }
            }

            var lanePerms = await GetCardGroupLanePermissionsFromSqlAsync();
            var schedules = await GetAccessSchedulesFromSqlAsync();
            var rfidRules = await GetRFIDAccessRulesFromSqlAsync();

            lock (_cacheLock)
            {
                _cachedLanePerms = lanePerms;
                _cachedSchedules = schedules;
                _cachedRfidRules = rfidRules;
                _lastCacheTime = now;
            }

            return (lanePerms, schedules, rfidRules);
        }

        private CardAccessPolicyService() { }

        /// <summary>
        /// Validates physical access for a card UID swiped at a specific lane.
        /// Returns (Allowed, Reason)
        /// </summary>
        public async Task<(bool Allowed, string Reason)> ValidatePhysicalAccessAsync(string cardUid, int laneId)
        {
            RFIDCard? card = await _db.GetRFIDCardByUidAsync(cardUid);
            if (card == null)
            {
                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "RFID", cardUid, $"Card UID: {cardUid} not found in database.", "CardAccessPolicyService");
                return (false, "Thẻ không tồn tại");
            }
            return await ValidatePhysicalAccessAsync(card, laneId);
        }

        public async Task<(bool Allowed, string Reason)> ValidatePhysicalAccessAsync(RFIDCard card, int laneId)
        {
            try
            {
                LoggingService.Instance.LogInfo("PHYSICAL_ACCESS", "Validate", $"Checking physical access for Card UID: {card.UID} at Lane ID: {laneId}");

                // 2. Check Card Status
                if (string.IsNullOrWhiteSpace(card.TrangThai) || !string.Equals(card.TrangThai, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    LoggingService.Instance.LogSecurity("ACCESS_DENIED", "RFID", card.UID, $"Card UID: {card.UID} is inactive (TrangThai: {card.TrangThai}).", "CardAccessPolicyService");
                    return (false, "Thẻ bị khóa hoặc không hoạt động");
                }

                // 3. Check Expiry
                if (card.NgayHetHan.HasValue && card.NgayHetHan.Value < DateTime.Now)
                {
                    LoggingService.Instance.LogSecurity("ACCESS_DENIED", "RFID", card.UID, $"Card UID: {card.UID} expired on {card.NgayHetHan.Value}.", "CardAccessPolicyService");
                    return (false, "Thẻ đã hết hạn");
                }

                // 4. Check Group Assignment
                if (!card.GroupId.HasValue || card.GroupId.Value == 0)
                {
                    // Fallback to standard validation (if no group is assigned, bypass schedule checks)
                    LoggingService.Instance.LogInfo("PHYSICAL_ACCESS", "Validate", $"Card UID: {card.UID} has no Card Group assigned. Standard fallback allowed.");
                    return (true, "Hợp lệ (Không phân nhóm)");
                }

                // 5. Query lane permissions and schedules
                List<CardGroupLanePermission> lanePerms;
                List<AccessSchedule> schedules;
                List<RFIDAccessRule> rfidRules;

                bool isOffline = ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline;

                if (isOffline)
                {
                    // Fetch from SQLite cache
                    lanePerms = await OfflineCacheService.Instance.GetCacheAsync<List<CardGroupLanePermission>>("LIST_CARD_GROUP_LANE_PERMISSIONS") ?? new List<CardGroupLanePermission>();
                    schedules = await OfflineCacheService.Instance.GetCacheAsync<List<AccessSchedule>>("LIST_ACCESS_SCHEDULES") ?? new List<AccessSchedule>();
                    rfidRules = await OfflineCacheService.Instance.GetCacheAsync<List<RFIDAccessRule>>("LIST_RFID_ACCESS_RULES") ?? new List<RFIDAccessRule>();
                }
                else
                {
                    // Query directly from SQL Server with in-memory cache
                    var policies = await GetAccessPoliciesWithCacheAsync();
                    lanePerms = policies.lanePerms;
                    schedules = policies.schedules;
                    rfidRules = policies.rfidRules;
                }

                // 6. Check explicit card-level RFID rules first
                var explicitRules = rfidRules.Where(r => string.Equals(r.CardUID, card.UID, StringComparison.OrdinalIgnoreCase) && r.LaneId == laneId && string.Equals(r.TrangThai, "Active", StringComparison.OrdinalIgnoreCase)).ToList();
                if (explicitRules.Any())
                {
                    foreach (var rule in explicitRules)
                    {
                        if (string.Equals(rule.RuleType, "Deny", StringComparison.OrdinalIgnoreCase))
                        {
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "RFID", card.UID, $"Card UID: {card.UID} explicitly blocked by RFID rule on Lane {laneId}.", "CardAccessPolicyService");
                            return (false, "Bị chặn theo thiết lập riêng");
                        }
                        if (string.Equals(rule.RuleType, "Allow", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!rule.ScheduleId.HasValue)
                            {
                                LoggingService.Instance.LogInfo("PHYSICAL_ACCESS", "Validate", $"Card UID: {card.UID} explicitly allowed by RFID rule on Lane {laneId}.");
                                return (true, "Cho phép theo thiết lập riêng");
                            }

                            var ruleSched = schedules.FirstOrDefault(s => s.Id == rule.ScheduleId.Value && string.Equals(s.TrangThai, "Active", StringComparison.OrdinalIgnoreCase));
                            if (ruleSched != null && IsTimeWithinSchedule(ruleSched))
                            {
                                LoggingService.Instance.LogInfo("PHYSICAL_ACCESS", "Validate", $"Card UID: {card.UID} allowed by RFID rule schedule '{ruleSched.ScheduleName}' on Lane {laneId}.");
                                return (true, "Cho phép theo thiết lập riêng");
                            }
                        }
                    }
                }

                // 7. Find group lane permissions
                var groupPerms = lanePerms.Where(p => p.GroupId == card.GroupId.Value && p.LaneId == laneId && string.Equals(p.TrangThai, "Active", StringComparison.OrdinalIgnoreCase)).ToList();
                if (!groupPerms.Any())
                {
                    LoggingService.Instance.LogSecurity("ACCESS_DENIED", "RFID", card.UID, $"Card UID: {card.UID} (Group: {card.GroupId.Value}) does not have permission on Lane ID: {laneId}.", "CardAccessPolicyService");
                    return (false, "Sai phân làn thẻ (Không có quyền vào cổng này)");
                }

                // 8. Validate schedule
                foreach (var perm in groupPerms)
                {
                    if (!perm.ScheduleId.HasValue)
                    {
                        // Unlimited access for this group on this lane
                        LoggingService.Instance.LogInfo("PHYSICAL_ACCESS", "Validate", $"Card UID: {card.UID} allowed (Unlimited access) on Lane ID: {laneId}.");
                        return (true, "Hợp lệ (Không giới hạn giờ)");
                    }

                    var sched = schedules.FirstOrDefault(s => s.Id == perm.ScheduleId.Value && string.Equals(s.TrangThai, "Active", StringComparison.OrdinalIgnoreCase));
                    if (sched == null)
                    {
                        continue;
                    }

                    // Emergency Override Bypass
                    if (sched.IsEmergencyOverride)
                    {
                        LoggingService.Instance.LogWarning("PHYSICAL_ACCESS", "Validate", $"Card UID: {card.UID} allowed on Lane ID: {laneId} due to active Emergency Override schedule '{sched.ScheduleName}'");
                        return (true, "Bypass khẩn cấp");
                    }

                    if (IsTimeWithinSchedule(sched))
                    {
                        LoggingService.Instance.LogInfo("PHYSICAL_ACCESS", "Validate", $"Card UID: {card.UID} allowed on Lane ID: {laneId} matching schedule '{sched.ScheduleName}'");
                        return (true, "Hợp lệ");
                    }
                }

                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "RFID", card.UID, $"Card UID: {card.UID} swiped outside of scheduled active hours on Lane ID: {laneId}.", "CardAccessPolicyService");
                return (false, "Ngoài khung giờ truy cập");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("PHYSICAL_ACCESS_ERROR", "Validate", $"Error validating physical access for {card.UID}", ex);
                return (false, "Lỗi kiểm tra quyền truy cập");
            }
        }

        private bool IsTimeWithinSchedule(AccessSchedule sched)
        {
            var now = DateTime.Now;
            var currentDay = now.DayOfWeek.ToString(); // e.g. "Monday"

            // 1. Verify Day of Week
            var days = (sched.DaysOfWeek ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim());
            bool dayMatches = days.Any(d => string.Equals(d, currentDay, StringComparison.OrdinalIgnoreCase));
            if (!dayMatches) return false;

            // 2. Verify Time range
            var timeOfDay = now.TimeOfDay;
            var start = sched.StartTime;
            var end = sched.EndTime;

            if (start <= end)
            {
                return timeOfDay >= start && timeOfDay <= end;
            }
            else
            {
                // Overnight schedule (e.g., 22:00 to 06:00)
                return timeOfDay >= start || timeOfDay <= end;
            }
        }

        #region SQL Server Queries

        public async Task<List<AccessSchedule>> GetAccessSchedulesFromSqlAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            var list = new List<AccessSchedule>();
            if (string.IsNullOrWhiteSpace(connStr)) return list;

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                string sql = "SELECT Id, ScheduleName, StartTime, EndTime, DaysOfWeek, IsEmergencyOverride, TrangThai, CreatedUtc FROM dbo.AccessSchedules";
                using var cmd = new SqlCommand(sql, conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new AccessSchedule
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        ScheduleName = r["ScheduleName"]?.ToString() ?? string.Empty,
                        StartTime = (TimeSpan)r["StartTime"],
                        EndTime = (TimeSpan)r["EndTime"],
                        DaysOfWeek = r["DaysOfWeek"]?.ToString() ?? string.Empty,
                        IsEmergencyOverride = Convert.ToBoolean(r["IsEmergencyOverride"]),
                        TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                        CreatedUtc = Convert.ToDateTime(r["CreatedUtc"])
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SQL_GET", "AccessSchedules", "Failed to get access schedules from SQL Server", ex);
            }
            return list;
        }

        public async Task<List<CardGroup>> GetCardGroupsFromSqlAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            var list = new List<CardGroup>();
            if (string.IsNullOrWhiteSpace(connStr)) return list;

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                string sql = "SELECT Id, GroupName, Description, TrangThai, CreatedUtc FROM dbo.CardGroups";
                using var cmd = new SqlCommand(sql, conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new CardGroup
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        GroupName = r["GroupName"]?.ToString() ?? string.Empty,
                        Description = r["Description"]?.ToString() ?? string.Empty,
                        TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                        CreatedUtc = Convert.ToDateTime(r["CreatedUtc"])
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SQL_GET", "CardGroups", "Failed to get card groups from SQL Server", ex);
            }
            return list;
        }

        public async Task<List<CardGroupLanePermission>> GetCardGroupLanePermissionsFromSqlAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            var list = new List<CardGroupLanePermission>();
            if (string.IsNullOrWhiteSpace(connStr)) return list;

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                string sql = "SELECT Id, GroupId, LaneId, ScheduleId, TrangThai, CreatedUtc FROM dbo.CardGroupLanePermissions";
                using var cmd = new SqlCommand(sql, conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new CardGroupLanePermission
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        GroupId = Convert.ToInt32(r["GroupId"]),
                        LaneId = Convert.ToInt32(r["LaneId"]),
                        ScheduleId = r["ScheduleId"] != DBNull.Value ? Convert.ToInt32(r["ScheduleId"]) : (int?)null,
                        TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                        CreatedUtc = Convert.ToDateTime(r["CreatedUtc"])
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SQL_GET", "CardGroupLanePermissions", "Failed to get lane permissions from SQL Server", ex);
            }
            return list;
        }

        public async Task<List<RFIDAccessRule>> GetRFIDAccessRulesFromSqlAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            var list = new List<RFIDAccessRule>();
            if (string.IsNullOrWhiteSpace(connStr)) return list;

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                string sql = "SELECT Id, CardUID, LaneId, ScheduleId, RuleType, TrangThai, CreatedUtc FROM dbo.RFIDAccessRules";
                using var cmd = new SqlCommand(sql, conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new RFIDAccessRule
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        CardUID = r["CardUID"]?.ToString() ?? string.Empty,
                        LaneId = Convert.ToInt32(r["LaneId"]),
                        ScheduleId = r["ScheduleId"] != DBNull.Value ? Convert.ToInt32(r["ScheduleId"]) : (int?)null,
                        RuleType = r["RuleType"]?.ToString() ?? string.Empty,
                        TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                        CreatedUtc = Convert.ToDateTime(r["CreatedUtc"])
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SQL_GET", "RFIDAccessRules", "Failed to get RFID access rules from SQL Server", ex);
            }
            return list;
        }

        #endregion
    }
}
