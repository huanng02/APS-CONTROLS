using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public sealed class PermissionMatrixItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
    }

    public sealed class PermissionChange
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int PermissionId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
        public bool OriginalValue { get; set; }
        public bool NewValue { get; set; }

        public override string ToString() =>
            $"[Role: {RoleName} ({RoleId}), Perm: {PermissionCode} ({PermissionId})] {OriginalValue} -> {NewValue}";
    }

    public sealed class PermissionMatrixService
    {
        private static readonly PermissionMatrixService _instance = new();
        public static PermissionMatrixService Instance => _instance;

        private readonly DatabaseService _db = new();

        private static readonly object _roleLevelsLock = new();
        private static readonly Dictionary<string, int> _roleLevels = new(StringComparer.OrdinalIgnoreCase);

        public static void InitializeRoleLevels(List<RoleOption> roles)
        {
            if (roles == null) return;
            lock (_roleLevelsLock)
            {
                _roleLevels.Clear();
                foreach (var role in roles)
                {
                    _roleLevels[role.Name] = role.Level;
                }
            }
        }

        public async Task<List<RoleOption>> GetRolesAsync()
        {
            var roles = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<RoleOption>>(
                "MATRIX_LOOKUP_ROLES",
                async conn =>
                {
                    var list = new List<RoleOption>();
                    using (var cmd = new SqlCommand("SELECT Id, Name, TrangThai, Level FROM dbo.Roles ORDER BY Id;", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new RoleOption
                            {
                                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                TrangThai = reader["TrangThai"]?.ToString() ?? string.Empty,
                                Level = reader["Level"] != DBNull.Value ? Convert.ToInt32(reader["Level"]) : 0
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<RoleOption>();

            InitializeRoleLevels(roles);
            return roles;
        }

        public async Task<List<PermissionMatrixItem>> GetPermissionsAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<PermissionMatrixItem>>(
                "MATRIX_LOOKUP_PERMISSIONS",
                async conn =>
                {
                    var list = new List<PermissionMatrixItem>();
                    using (var cmd = new SqlCommand("SELECT Id, Code, Name, Description FROM dbo.Permissions ORDER BY Code;", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var code = reader["Code"]?.ToString() ?? string.Empty;
                            list.Add(new PermissionMatrixItem
                            {
                                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                                Code = code,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                Description = reader["Description"]?.ToString() ?? string.Empty,
                                Module = GetModuleGroup(code)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<PermissionMatrixItem>();
        }

        public async Task<HashSet<(int RoleId, int PermissionId)>> GetRolePermissionsAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<HashSet<(int RoleId, int PermissionId)>>(
                "MATRIX_LOOKUP_ROLE_PERMISSIONS",
                async conn =>
                {
                    var set = new HashSet<(int RoleId, int PermissionId)>();
                    using (var cmd = new SqlCommand("SELECT RoleId, PermissionId FROM dbo.RolePermissions;", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int rId = Convert.ToInt32(reader["RoleId"]);
                            int pId = Convert.ToInt32(reader["PermissionId"]);
                            set.Add((rId, pId));
                        }
                    }
                    return set;
                }
            ) ?? new HashSet<(int RoleId, int PermissionId)>();
        }

        public static int GetRoleLevel(string roleName)
        {
            if (string.IsNullOrEmpty(roleName)) return 0;

            lock (_roleLevelsLock)
            {
                if (_roleLevels.TryGetValue(roleName, out int lvl))
                {
                    return lvl;
                }
            }

            return roleName.ToLowerInvariant() switch
            {
                "superadmin" => 100,
                "admin"      => 80,
                "manager"    => 60,
                "operator"   => 40,
                "technician" => 30,
                "cashier"    => 20,
                "guard"      => 10,
                "auditor"    => 5,
                "viewer"     => 1,
                _            => 0
            };
        }

        public static void ValidateChanges(List<PermissionChange> changes, string currentUserRole)
        {
            int currentUserLevel = GetRoleLevel(currentUserRole);

            // Quyền hệ thống bắt buộc bảo vệ cho SuperAdmin
            var mandatoryPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ROLE_MANAGEMENT",
                "USER_MANAGEMENT",
                "PERMISSION_MANAGEMENT"
            };

            foreach (var change in changes)
            {
                int targetLevel = GetRoleLevel(change.RoleName);

                // 1. Tự sửa đổi vai trò của bản thân
                if (string.Equals(change.RoleName, currentUserRole, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Không thể tự sửa đổi phân quyền cho vai trò của chính mình ({change.RoleName}).");
                }

                // 2. Kiểm tra cấp bậc (Role Hierarchy)
                if (targetLevel >= currentUserLevel)
                {
                    throw new UnauthorizedAccessException($"Không có quyền sửa đổi vai trò '{change.RoleName}' (cấp độ {targetLevel}) có cấp tương đương hoặc cao hơn vai trò hiện tại của bạn '{currentUserRole}' (cấp độ {currentUserLevel}).");
                }

                // 3. Bảo vệ SuperAdmin
                if (string.Equals(change.RoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && 
                    !string.Equals(currentUserRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Chỉ có vai trò SuperAdmin mới được quyền sửa đổi vai trò SuperAdmin.");
                }

                // 4. Bảo vệ quyền hệ thống bắt buộc của SuperAdmin
                if (string.Equals(change.RoleName, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && 
                    !change.NewValue && 
                    mandatoryPermissions.Contains(change.PermissionCode))
                {
                    throw new InvalidOperationException($"Không thể thu hồi quyền hệ thống bắt buộc '{change.PermissionCode}' từ vai trò SuperAdmin.");
                }
            }
        }

        public async Task SavePermissionChangesAsync(List<PermissionChange> changes)
        {
            if (changes == null || !changes.Any()) return;

            // Kiểm tra bảo mật backend
            ValidateChanges(changes, CurrentUserContext.Instance.Role);

            using (var conn = new SqlConnection(ConnectionManager.Instance.CurrentConnectionString))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int? actorId = CurrentUserContext.Instance.Id > 0 ? CurrentUserContext.Instance.Id : (int?)null;
                        string actorUsername = !string.IsNullOrEmpty(CurrentUserContext.Instance.Username) ? CurrentUserContext.Instance.Username : "system";
                        string actorRole = !string.IsNullOrEmpty(CurrentUserContext.Instance.Role) ? CurrentUserContext.Instance.Role : "System";

                        foreach (var change in changes)
                        {
                            if (change.NewValue && !change.OriginalValue)
                            {
                                // Grant: Insert role-permission mapping
                                using (var cmd = new SqlCommand(@"
                                    IF NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @RoleId AND PermissionId = @PermissionId)
                                    BEGIN
                                        INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (@RoleId, @PermissionId);
                                    END;", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RoleId", change.RoleId);
                                    cmd.Parameters.AddWithValue("@PermissionId", change.PermissionId);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // Write Audit Log
                                var auditLog = new AuditLog
                                {
                                    UserId = actorId,
                                    Username = actorUsername,
                                    ActionType = "GRANT_PERMISSION",
                                    EntityType = "RolePermission",
                                    EntityId = $"Role:{change.RoleId},Permission:{change.PermissionId}",
                                    OldValue = System.Text.Json.JsonSerializer.Serialize(new { PermissionCode = change.PermissionCode, Granted = false }),
                                    NewValue = System.Text.Json.JsonSerializer.Serialize(new { PermissionCode = change.PermissionCode, Granted = true }),
                                    Description = $"{actorRole} {actorUsername} granted {change.PermissionCode} permission to {change.RoleName} role.",
                                    CreatedAt = DateTime.UtcNow
                                };
                                await AuditLogService.Instance.LogAuditAsync(auditLog, conn, transaction);
                            }
                            else if (!change.NewValue && change.OriginalValue)
                            {
                                // Revoke: Delete role-permission mapping
                                using (var cmd = new SqlCommand(@"
                                    DELETE FROM dbo.RolePermissions WHERE RoleId = @RoleId AND PermissionId = @PermissionId;", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@RoleId", change.RoleId);
                                    cmd.Parameters.AddWithValue("@PermissionId", change.PermissionId);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // Write Audit Log
                                var auditLog = new AuditLog
                                {
                                    UserId = actorId,
                                    Username = actorUsername,
                                    ActionType = "REVOKE_PERMISSION",
                                    EntityType = "RolePermission",
                                    EntityId = $"Role:{change.RoleId},Permission:{change.PermissionId}",
                                    OldValue = System.Text.Json.JsonSerializer.Serialize(new { PermissionCode = change.PermissionCode, Granted = true }),
                                    NewValue = System.Text.Json.JsonSerializer.Serialize(new { PermissionCode = change.PermissionCode, Granted = false }),
                                    Description = $"{actorRole} {actorUsername} revoked {change.PermissionCode} permission from {change.RoleName} role.",
                                    CreatedAt = DateTime.UtcNow
                                };
                                await AuditLogService.Instance.LogAuditAsync(auditLog, conn, transaction);
                            }
                        }

                        transaction.Commit();

                        // Invalidate local SQLite cache key
                        await OfflineCacheService.Instance.InvalidateCacheAsync("MATRIX_LOOKUP_ROLE_PERMISSIONS");
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        LoggingService.Instance.LogError("MATRIX_SAVE_FAILED", "PermissionMatrixService", "Failed to save permission matrix updates under transaction", ex);
                        throw;
                    }
                }
            }
        }

        private static string GetModuleGroup(string code)
        {
            if (code.StartsWith("USER_") || code.StartsWith("ROLE_")) 
                return "Quản lý nhân viên & Phân quyền";
            if (code.StartsWith("RFID_") || code.Equals("MANAGE_PRICING", StringComparison.OrdinalIgnoreCase)) 
                return "Quản lý Thẻ & Bảng giá";
            if (code.StartsWith("CONFIG_") || code.Equals("VIEW_DEVICE_STATUS", StringComparison.OrdinalIgnoreCase) || code.Equals("RESTART_CONTROLLER", StringComparison.OrdinalIgnoreCase)) 
                return "Cấu hình & Phần cứng";
            if (code.StartsWith("VIEW_REPORT") || code.StartsWith("VIEW_LOG") || code.StartsWith("VIEW_AUDIT_")) 
                return "Báo cáo & Nhật ký";
            if (code.Equals("OPEN_BARRIER", StringComparison.OrdinalIgnoreCase) || code.Equals("FORCE_EXIT", StringComparison.OrdinalIgnoreCase) || code.Equals("MANUAL_OVERRIDE", StringComparison.OrdinalIgnoreCase) || code.Equals("HANDLE_LOST_TICKET", StringComparison.OrdinalIgnoreCase) || code.Equals("MANUAL_PLATE_EDIT", StringComparison.OrdinalIgnoreCase)) 
                return "Vận hành Làn xe";
            if (code.Equals("VIEW_CAMERA", StringComparison.OrdinalIgnoreCase) || code.Equals("VIEW_OCCUPANCY", StringComparison.OrdinalIgnoreCase) || code.Equals("VIEW_DASHBOARD", StringComparison.OrdinalIgnoreCase)) 
                return "Giám sát & Dashboard";
            if (code.Equals("PAYMENT_PROCESS", StringComparison.OrdinalIgnoreCase) || code.Equals("REFUND_PAYMENT", StringComparison.OrdinalIgnoreCase) || code.Equals("EXPORT_FINANCIAL_REPORT", StringComparison.OrdinalIgnoreCase) || code.Equals("VIEW_REVENUE", StringComparison.OrdinalIgnoreCase)) 
                return "Tài chính & Soát vé";
            if (code.Equals("SIMULATE_RECOVERY", StringComparison.OrdinalIgnoreCase)) 
                return "Vận hành QA Panel";
            return "Khác";
        }
    }
}
