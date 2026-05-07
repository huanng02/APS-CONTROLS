using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public sealed class UserManagementService
    {
        private readonly UserManagementRepository _repo = new();

        public Task<List<RoleOption>> GetRolesAsync() => _repo.GetRolesAsync();

        public Task<List<UserListItem>> SearchUsersAsync(int currentUserId, string search, int? roleId, string? status) =>
            _repo.SearchUsersAsync(currentUserId, search, roleId, status);

        public async Task<(bool Success, string Message)> CreateUserAsync(UserUpsertModel model, int actorUserId)
        {
            if (string.IsNullOrWhiteSpace(model.Ten) || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                return (false, "Tên, Username, Password không được để trống.");

            if (model.Password.Trim().Length < 6)
                return (false, "Password phải có ít nhất 6 ký tự.");

            if (await _repo.UsernameExistsAsync(model.Username).ConfigureAwait(false))
                return (false, "Username đã tồn tại.");

            int newId = await _repo.CreateUserAsync(model).ConfigureAwait(false);
            LoggingService.Instance.LogInfo(
                "CREATE_USER",
                nameof(UserManagementService),
                $"Created user Id={newId}, Username={model.Username}, RoleId={model.RoleId}, Status={model.TrangThai}",
                actorUserId.ToString());

            return (true, "Thêm user thành công.");
        }

        public async Task<(bool Success, string Message)> UpdateUserAsync(int id, string ten, int roleId, string trangThai, int actorUserId)
        {
            if (id <= 0) return (false, "User không hợp lệ.");
            if (string.IsNullOrWhiteSpace(ten)) return (false, "Tên không được để trống.");

            await _repo.UpdateUserAsync(id, ten, roleId, trangThai).ConfigureAwait(false);
            LoggingService.Instance.LogInfo(
                "UPDATE_USER",
                nameof(UserManagementService),
                $"Updated user Id={id}, Ten={ten}, RoleId={roleId}, Status={trangThai}",
                actorUserId.ToString());
            return (true, "Cập nhật user thành công.");
        }

        public async Task<(bool Success, string Message)> DisableUserAsync(int id, int actorUserId)
        {
            if (id <= 0) return (false, "User không hợp lệ.");
            await _repo.DisableUserAsync(id).ConfigureAwait(false);

            LoggingService.Instance.LogInfo(
                "DISABLE_USER",
                nameof(UserManagementService),
                $"Disabled user Id={id}",
                actorUserId.ToString());
            return (true, "Đã disable user.");
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(int id, int actorUserId)
        {
            if (id <= 0) return (false, "User không hợp lệ.");
            const string defaultPassword = "123456";

            await _repo.ResetPasswordAsync(id, defaultPassword).ConfigureAwait(false);
            LoggingService.Instance.LogInfo(
                "RESET_PASSWORD",
                nameof(UserManagementService),
                $"Reset password user Id={id} -> default",
                actorUserId.ToString());

            return (true, $"Đã reset password mặc định: {defaultPassword}");
        }
    }
}
