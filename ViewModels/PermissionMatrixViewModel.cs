using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public sealed class PermissionMatrixViewModel
    {
        private readonly PermissionMatrixService _service = PermissionMatrixService.Instance;

        // ── Properties for View Data Binding/Retrieval ────────────────────
        public List<RoleOption> Roles { get; private set; } = new();
        public List<PermissionMatrixItem> Permissions { get; private set; } = new();
        public HashSet<(int RoleId, int PermissionId)> OriginalRolePermissions { get; private set; } = new();
        public Dictionary<(int RoleId, int PermissionId), PermissionChange> PendingChanges { get; private set; } = new();

        // ── Load Initial States ───────────────────────────────────────────
        public async Task LoadDataAsync()
        {
            Roles = await _service.GetRolesAsync();
            Permissions = await _service.GetPermissionsAsync();
            OriginalRolePermissions = await _service.GetRolePermissionsAsync();
            PendingChanges.Clear();
        }

        // ── Handle Toggles and Track Changes ──────────────────────────────
        public void TogglePermission(int roleId, string roleName, int permId, string permCode, bool newValue)
        {
            var key = (roleId, permId);
            bool originalValue = OriginalRolePermissions.Contains(key);

            if (newValue != originalValue)
            {
                // Record the change
                PendingChanges[key] = new PermissionChange
                {
                    RoleId = roleId,
                    RoleName = roleName,
                    PermissionId = permId,
                    PermissionCode = permCode,
                    OriginalValue = originalValue,
                    NewValue = newValue
                };
            }
            else
            {
                // Reverted back to original database state, remove from track log
                PendingChanges.Remove(key);
            }
        }

        // ── Query/Retrieval API Methods ───────────────────────────────────
        public List<PermissionChange> GetPendingChanges()
        {
            return PendingChanges.Values.ToList();
        }

        public bool HasChange(int roleId, int permId)
        {
            return PendingChanges.ContainsKey((roleId, permId));
        }

        public int GetChangeCount()
        {
            return PendingChanges.Count;
        }

        public bool GetCurrentValue(int roleId, int permId)
        {
            var key = (roleId, permId);
            if (PendingChanges.TryGetValue(key, out var change))
            {
                return change.NewValue;
            }
            return OriginalRolePermissions.Contains(key);
        }

        public async Task SaveChangesAsync()
        {
            var changes = GetPendingChanges();
            if (!changes.Any()) return;

            // Save database changes
            await _service.SavePermissionChangesAsync(changes);

            // Re-fetch original role permissions from the database/cache
            OriginalRolePermissions = await _service.GetRolePermissionsAsync();

            // Clear pending tracking changes since they are now persisted
            PendingChanges.Clear();
        }
    }
}
