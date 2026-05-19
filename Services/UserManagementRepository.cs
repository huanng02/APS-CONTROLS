using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public sealed class UserManagementRepository
    {
        private readonly DatabaseService _db = new();

        public async Task<List<RoleOption>> GetRolesAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<RoleOption>>(
                "LOOKUP_ROLES",
                async conn =>
                {
                    var list = new List<RoleOption>();
                    using (var cmd = new SqlCommand( @"SELECT Id, Name, TrangThai FROM Roles ORDER BY Name;", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new RoleOption
                            {
                                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                TrangThai = reader["TrangThai"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<RoleOption>();
        }

        public async Task<List<UserListItem>> SearchUsersAsync(int currentUserId, string search, int? roleId, string? status)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<UserListItem>>(
                $"USERS_SEARCH_{currentUserId}_{search}_{roleId}_{status}",
                async conn =>
                {
                    var list = new List<UserListItem>();
                    var sql = new StringBuilder(@"
                        SELECT nv.Id, nv.Ten, nv.Username, nv.RoleId, r.Name AS RoleName, nv.TrangThai, nv.CreatedAt, nv.LastLogin
                        FROM NhanVien nv
                        LEFT JOIN Roles r ON nv.RoleId = r.Id
                        WHERE nv.Id <> @CurrentUserId");

                    using (var cmd = new SqlCommand(string.Empty, conn))
                    {
                        cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId);

                        if (!string.IsNullOrWhiteSpace(search))
                        {
                            sql.Append(" AND (nv.Ten LIKE @Search OR nv.Username LIKE @Search)");
                            cmd.Parameters.AddWithValue("@Search", $"%{search.Trim()}%");
                        }

                        if (roleId.HasValue && roleId.Value > 0)
                        {
                            sql.Append(" AND nv.RoleId = @RoleId");
                            cmd.Parameters.AddWithValue("@RoleId", roleId.Value);
                        }

                        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
                        {
                            sql.Append(" AND nv.TrangThai = @Status");
                            cmd.Parameters.AddWithValue("@Status", status);
                        }

                        sql.Append(" ORDER BY nv.Id DESC;");
                        cmd.CommandText = sql.ToString();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new UserListItem
                                {
                                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                                    Ten = reader["Ten"]?.ToString() ?? string.Empty,
                                    Username = reader["Username"]?.ToString() ?? string.Empty,
                                    RoleId = reader["RoleId"] != DBNull.Value ? Convert.ToInt32(reader["RoleId"]) : 0,
                                    RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
                                    TrangThai = reader["TrangThai"]?.ToString() ?? string.Empty,
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                    LastLogin = reader["LastLogin"] == DBNull.Value ? null : Convert.ToDateTime(reader["LastLogin"])
                                });
                            }
                        }
                    }
                    return list;
                }
            ) ?? new List<UserListItem>();
        }

        public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<bool>(
                $"USER_EXISTS_{username}",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT COUNT(1) FROM NhanVien WHERE Username = @Username AND (@ExcludeUserId IS NULL OR Id <> @ExcludeUserId);", conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username.Trim());
                        cmd.Parameters.AddWithValue("@ExcludeUserId", (object?)excludeUserId ?? DBNull.Value);
                        var result = await cmd.ExecuteScalarAsync();
                        return result != null && Convert.ToInt32(result) > 0;
                    }
                }
            );
        }

        public async Task<int> CreateUserAsync(UserUpsertModel model)
        {
            int newId = 0;
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "CREATE_USER",
                model,
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"
                        INSERT INTO NhanVien (Ten, Username, [Password], TrangThai, RoleId, CreatedAt)
                        VALUES (@Ten, @Username, @Password, @TrangThai, @RoleId, SYSUTCDATETIME());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                    {
                        cmd.Parameters.AddWithValue("@Ten", model.Ten.Trim());
                        cmd.Parameters.AddWithValue("@Username", model.Username.Trim());
                        cmd.Parameters.AddWithValue("@Password", model.Password);
                        cmd.Parameters.AddWithValue("@TrangThai", model.TrangThai.Trim());
                        cmd.Parameters.AddWithValue("@RoleId", model.RoleId);
                        newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }
                }
            );
            return newId;
        }

        public async Task UpdateUserAsync(int id, string ten, int roleId, string trangThai)
        {
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_USER",
                new { Id = id, Ten = ten, RoleId = roleId, TrangThai = trangThai },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE NhanVien SET Ten = @Ten, RoleId = @RoleId, TrangThai = @TrangThai WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Ten", ten.Trim());
                        cmd.Parameters.AddWithValue("@RoleId", roleId);
                        cmd.Parameters.AddWithValue("@TrangThai", trangThai.Trim());
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public async Task DisableUserAsync(int id)
        {
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DISABLE_USER",
                new { Id = id },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE NhanVien SET TrangThai = 'Disabled' WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public async Task ResetPasswordAsync(int id, string newPassword)
        {
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "RESET_PASSWORD",
                new { Id = id },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE NhanVien SET [Password] = @Password WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Password", newPassword);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public async Task UpdateCurrentUserProfileAsync(int id, string ten, string username)
        {
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_PROFILE",
                new { Id = id, Ten = ten, Username = username },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE NhanVien SET Ten = @Ten, Username = @Username WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@Ten", ten.Trim());
                        cmd.Parameters.AddWithValue("@Username", username.Trim());
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public async Task ChangePasswordAsync(int userId, string newPassword)
        {
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "CHANGE_PASSWORD",
                new { Id = userId },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE NhanVien SET [Password] = @Password WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", userId);
                        cmd.Parameters.AddWithValue("@Password", newPassword);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public async Task<NhanVien?> GetUserByIdAsync(int id)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<NhanVien>(
                $"USER_BY_ID_{id}",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT Id, Ten, Username, [Password], TrangThai, RoleId, CreatedAt, LastLogin FROM NhanVien WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new NhanVien
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Ten = reader["Ten"]?.ToString() ?? "",
                                    Username = reader["Username"]?.ToString() ?? "",
                                    Password = reader["Password"]?.ToString() ?? "",
                                    TrangThai = reader["TrangThai"]?.ToString() ?? "",
                                    RoleId = reader["RoleId"] != DBNull.Value ? Convert.ToInt32(reader["RoleId"]) : 0,
                                    CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue,
                                    LastLogin = reader["LastLogin"] == DBNull.Value ? null : Convert.ToDateTime(reader["LastLogin"])
                                };
                            }
                        }
                    }
                    return null;
                }
            );
        }

        public async Task EnableUserAsync(int id)
        {
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "ENABLE_USER",
                new { Id = id },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE NhanVien SET TrangThai = 'Active' WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public async Task DeleteUserAsync(int id)
        {
            await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_USER",
                new { Id = id },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"DELETE FROM NhanVien WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }
    }
}
