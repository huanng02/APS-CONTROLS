using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class PermissionService
    {
        private static readonly PermissionService _instance = new();
        public static PermissionService Instance => _instance;

        private readonly DatabaseService _db = new();

        public async Task<HashSet<string>> GetPermissionsForUserAsync(int userId)
        {
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return permissions;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    
                    // Load permissions via RolePermissions and UserRoles (or legacy RoleId link)
                    string query = @"
                        SELECT DISTINCT p.Code 
                        FROM dbo.Permissions p
                        INNER JOIN dbo.RolePermissions rp ON p.Id = rp.PermissionId
                        INNER JOIN dbo.Roles r ON rp.RoleId = r.Id
                        WHERE r.Id IN (
                            SELECT RoleId FROM dbo.NhanVien WHERE Id = @UserId
                            UNION
                            SELECT RoleId FROM dbo.UserRoles WHERE UserId = @UserId
                        )";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                permissions.Add(reader["Code"].ToString() ?? string.Empty);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("PermissionService", "GetPermissionsForUserAsync", $"Error fetching permissions for user {userId}", ex);
            }

            return permissions;
        }

        public async Task<(HashSet<int> LaneIds, HashSet<int> SiteIds)> GetAssignedLanesAndSitesAsync(int userId)
        {
            var laneIds = new HashSet<int>();
            var siteIds = new HashSet<int>();
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return (laneIds, siteIds);

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // Fetch lanes
                    using (var cmd = new SqlCommand("SELECT LaneId FROM dbo.UserLanePermissions WHERE UserId = @UserId", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync()) laneIds.Add(Convert.ToInt32(reader["LaneId"]));
                        }
                    }

                    // Fetch sites
                    using (var cmd = new SqlCommand("SELECT SiteId FROM dbo.UserSitePermissions WHERE UserId = @UserId", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync()) siteIds.Add(Convert.ToInt32(reader["SiteId"]));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("PermissionService", "GetAssignedLanesAndSitesAsync", $"Error fetching assignments for user {userId}", ex);
            }

            return (laneIds, siteIds);
        }

        public async Task RefreshCurrentUserPermissionsAsync()
        {
            if (!CurrentUserContext.Instance.IsAuthenticated) return;

            int userId = CurrentUserContext.Instance.Id;
            var permissions = await GetPermissionsForUserAsync(userId);
            CurrentUserContext.Instance.UpdatePermissions(permissions);
        }

        public bool CheckPermission(string permissionCode)
        {
            if (!CurrentUserContext.Instance.IsAuthenticated) return false;
            
            // SuperAdmin and Admin bypass all permission checks
            string role = CurrentUserContext.Instance.Role;
            if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return CurrentUserContext.Instance.Permissions.Contains(permissionCode);
        }

        public async Task<bool> CheckPermissionAsync(int userId, string permissionCode)
        {
            var userPerms = await GetPermissionsForUserAsync(userId);
            return userPerms.Contains(permissionCode);
        }

        public bool HasLaneAccess(int userId, int laneId)
        {
            if (!CurrentUserContext.Instance.IsAuthenticated) return false;

            string role = CurrentUserContext.Instance.Role;
            if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var assignedLanes = CurrentUserContext.Instance.AssignedLaneIds;
            if (assignedLanes.Count == 0)
            {
                // Fallback: If no lane is specifically assigned, they have access to all lanes
                return true;
            }

            return assignedLanes.Contains(laneId);
        }

        public bool HasSiteAccess(int userId, int siteId)
        {
            if (!CurrentUserContext.Instance.IsAuthenticated) return false;

            string role = CurrentUserContext.Instance.Role;
            if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var assignedSites = CurrentUserContext.Instance.AssignedSiteIds;
            if (assignedSites.Count == 0)
            {
                return true;
            }

            return assignedSites.Contains(siteId);
        }
    }
}
