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

        public Task<List<UserListItem>> SearchUsersAsync(int currentUserId,
                                                         string search, int? roleId,
                                                         string? status) =>
            _repo.SearchUsersAsync(currentUserId, search, roleId, status);

        public async Task<(bool Success, string Message)> CreateUserAsync(
            UserUpsertModel model, int actorUserId)
        {
            if (string.IsNullOrWhiteSpace(model.Ten) ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password))
                return (false, "Tên, Username, Password không được để trống.");

            if (model.Password.Trim().Length < 6)
                return (false, "Password phải có ít nhất 6 ký tự.");

            if (await _repo.UsernameExistsAsync(model.Username).ConfigureAwait(false))
                return (false, "Username đã tồn tại.");

            // Hash password before saving (do NOT double-hash)
            string hashed = BCrypt.Net.BCrypt.HashPassword(model.Password);
            model.Password = hashed;

            int newId = await _repo.CreateUserAsync(model).ConfigureAwait(false);
            try
            {
                LoggingService.Instance.LogSecurity(
                    "CREATE_USER",
                    "Auth",
                    $"{{\"Username\":\"{model.Username}\",\"UserId\":{newId}}}",
                    userId: actorUserId.ToString());
            }
            catch { }

            return (true, "Thêm user thành công.");
        }

        public async Task<(bool Success, string Message)> UpdateUserAsync(
            int id, string ten, int roleId, string trangThai, int actorUserId)
        {
            if (id <= 0)
                return (false, "User không hợp lệ.");
            if (string.IsNullOrWhiteSpace(ten))
                return (false, "Tên không được để trống.");

            var previous = await _repo.GetUserByIdAsync(id).ConfigureAwait(false);
            var oldValues = previous == null ? null : new { Ten = previous.Ten, Username = previous.Username, RoleId = previous.RoleId, TrangThai = previous.TrangThai };
            var newValues = new { Ten = ten, Username = previous?.Username ?? string.Empty, RoleId = roleId, TrangThai = trangThai };

            await _repo.UpdateUserAsync(id, ten, roleId, trangThai)
                .ConfigureAwait(false);

            try
            {
                bool roleChanged = previous != null && previous.RoleId != roleId;
                bool otherChanged = previous == null || previous.Ten != ten || previous.TrangThai != trangThai;

                if (roleChanged && !otherChanged)
                {
                    LoggingService.Instance.LogCrud(
                        "CHANGE_ROLE",
                        "NhanVien",
                        id.ToString(),
                        oldValues: new { RoleId = previous.RoleId },
                        newValues: new { RoleId = roleId },
                        source: "Auth",
                        userId: actorUserId.ToString());
                }
                else
                {
                    LoggingService.Instance.LogCrud(
                        "UPDATE_USER",
                        "NhanVien",
                        id.ToString(),
                        oldValues: oldValues,
                        newValues: newValues,
                        source: "Auth",
                        userId: actorUserId.ToString());
                }
            }
            catch { }

            return (true, "Cập nhật user thành công.");
        }

        public async Task<(bool Success, string Message)> DisableUserAsync(
            int id, int actorUserId)
        {
            if (id <= 0)
                return (false, "User không hợp lệ.");
            var previous = await _repo.GetUserByIdAsync(id).ConfigureAwait(false);
            var oldValues = previous == null ? null : new { Ten = previous.Ten, Username = previous.Username, TrangThai = previous.TrangThai };

            await _repo.DisableUserAsync(id).ConfigureAwait(false);

            try
            {
                var newValues = new { TrangThai = "Disabled" };
                LoggingService.Instance.LogCrud(
                    "DISABLE_USER",
                    "NhanVien",
                    id.ToString(),
                    oldValues: oldValues,
                    newValues: newValues,
                    source: "Auth",
                    userId: actorUserId.ToString());
            }
            catch { }

            return (true, "Đã disable user.");
        }

        public async Task<(bool Success, string Message)> EnableUserAsync(int id, int actorUserId)
        {
            if (id <= 0) return (false, "User không hợp lệ.");
            var previous = await _repo.GetUserByIdAsync(id).ConfigureAwait(false);
            var oldValues = previous == null ? null : new { Ten = previous.Ten, Username = previous.Username, TrangThai = previous.TrangThai };

            await _repo.EnableUserAsync(id).ConfigureAwait(false);

            try
            {
                var newValues = new { TrangThai = "Active" };
                LoggingService.Instance.LogCrud(
                    "ENABLE_USER",
                    "NhanVien",
                    id.ToString(),
                    oldValues: oldValues,
                    newValues: newValues,
                    source: "Auth",
                    userId: actorUserId.ToString());
            }
            catch { }

            return (true, "Đã enable user.");
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(int id, int actorUserId)
        {
            if (id <= 0) return (false, "User không hợp lệ.");

            var previous = await _repo.GetUserByIdAsync(id).ConfigureAwait(false);
            var oldValues = previous == null ? null : new { Ten = previous.Ten, Username = previous.Username, TrangThai = previous.TrangThai };

            await _repo.DeleteUserAsync(id).ConfigureAwait(false);

            try
            {
                LoggingService.Instance.LogCrud(
                    "DELETE_USER",
                    "NhanVien",
                    id.ToString(),
                    oldValues: oldValues,
                    newValues: null,
                    source: "Auth",
                    userId: actorUserId.ToString());
            }
            catch { }

            return (true, "Đã xóa user.");
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(
            int id, int actorUserId)
        {
            if (id <= 0)
                return (false, "User không hợp lệ.");
            const string defaultPassword = "123456";

            // Hash default password before saving
            string hashed = BCrypt.Net.BCrypt.HashPassword(defaultPassword);

            await _repo.ResetPasswordAsync(id, hashed).ConfigureAwait(false);
            try
            {
                var target = await _repo.GetUserByIdAsync(id).ConfigureAwait(false);
                var oldValues = target == null ? null : new { Ten = target.Ten, Username = target.Username, TrangThai = target.TrangThai };
                var newValues = new { /* password changed - do not include password */ PasswordChanged = true };
                LoggingService.Instance.LogCrud(
                    "RESET_PASSWORD",
                    "NhanVien",
                    id.ToString(),
                    oldValues: oldValues,
                    newValues: newValues,
                    source: "Auth",
                    userId: actorUserId.ToString());
            }
            catch { /* best-effort logging */ }

            return (true, $"Đã reset password mặc định: {defaultPassword}");
        }
        public async Task<(bool Success, string Message)>
        UpdateCurrentUserProfileAsync(int id, string ten, string username)
        {
            try
            {
                if (id <= 0)
                    return (false, "User không hợp lệ.");

                if (string.IsNullOrWhiteSpace(ten))
                    return (false, "Tên không được để trống.");

                if (string.IsNullOrWhiteSpace(username))
                    return (false, "Username không được để trống.");

                bool usernameExists = await _repo.UsernameExistsAsync(username, id);

                if (usernameExists)
                    return (false, "Username đã tồn tại.");

                // =========================
                // OLD VALUES
                // =========================

                string oldTen = CurrentUser.Ten ?? "";
                string oldUsername = CurrentUser.Username ?? "";

                // =========================
                // UPDATE DB
                // =========================

                await _repo.UpdateCurrentUserProfileAsync(id, ten, username);

                // =========================
                // BUILD CHANGE LOG
                // =========================

                var changes = new List<string>();

                if (oldTen != ten)
                {
                    changes.Add($"Ten: '{oldTen}' -> '{ten}'");
                }

                if (oldUsername != username)
                {
                    changes.Add($"Username: '{oldUsername}' -> '{username}'");
                }

                string detail =
                    changes.Count > 0 ? string.Join(" | ", changes) : "No changes";

                // =========================
                // LOG
                // =========================

                LoggingService.Instance.LogInfo(
                    "UPDATE_PROFILE", nameof(UserManagementService),
                    $"Profile updated | {detail}", id.ToString());

                return (true, "Cập nhật thông tin thành công.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(
                    "UPDATE_PROFILE_ERROR", nameof(UserManagementService),
                    $"Update profile failed | UserId={id}", ex, id.ToString());

                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            int id, string oldPassword, string newPassword,
            string confirmPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(oldPassword))
                    return (false, "Vui lòng nhập mật khẩu cũ.");

                if (string.IsNullOrWhiteSpace(newPassword))
                    return (false, "Vui lòng nhập mật khẩu mới.");

                if (newPassword.Length < 6)
                    return (false, "Mật khẩu mới phải >= 6 ký tự.");

                if (newPassword != confirmPassword)
                    return (false, "Xác nhận mật khẩu không khớp.");

                var user = await _repo.GetUserByIdAsync(id);

                if (user == null)
                    return (false, "Không tìm thấy user.");

                // verify old password using BCrypt when possible
                var stored = user.Password ?? string.Empty;

                bool oldMatches;
                if (stored.StartsWith("$2"))
                {
                    try
                    {
                        oldMatches = BCrypt.Net.BCrypt.Verify(oldPassword, stored);
                    }
                    catch
                    {
                        oldMatches = false;
                    }
                }
                else
                {
                    // legacy plaintext - fixed time compare
                    var a = System.Text.Encoding.UTF8.GetBytes(oldPassword ?? string.Empty);
                    var b = System.Text.Encoding.UTF8.GetBytes(stored);
                    oldMatches = (a.Length == b.Length) && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
                }

                if (!oldMatches)
                {
                    try
                    {
                        LoggingService.Instance.LogSecurity(
                            "CHANGE_PASSWORD",
                            "Auth",
                            $"{{\"PasswordChanged\":false,\"Reason\":\"OldPasswordMismatch\",\"Username\":\"{user.Username}\"}}",
                            userId: id.ToString());
                    }
                    catch { }

                    return (false, "Mật khẩu cũ không đúng.");
                }

                // Hash new password before saving
                string hashedNew = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await _repo.ChangePasswordAsync(id, hashedNew);

                try
                {
                    // success audit: do NOT log the actual password
                    LoggingService.Instance.LogSecurity(
                        "CHANGE_PASSWORD",
                        "Auth",
                        "{\"PasswordChanged\":true}",
                        userId: id.ToString());
                }
                catch { }

                return (true, "Đổi mật khẩu thành công.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(
                    "CHANGE_PASSWORD_ERROR", nameof(UserManagementService),
                    $"Lỗi đổi mật khẩu user Id={id}", ex, id.ToString());

                return (false, ex.Message);
            }
        }
    }
}
