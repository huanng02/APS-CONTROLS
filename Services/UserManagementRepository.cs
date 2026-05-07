using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public sealed class UserManagementRepository
    {
        private readonly DatabaseService _db = new();

        public async Task<List<RoleOption>> GetRolesAsync()
        {
            var list = new List<RoleOption>();
            const string sql =
                @"
    SELECT Id, Name, TrangThai
    FROM Roles
    ORDER BY Name;";

            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync().ConfigureAwait(false);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new RoleOption
                {
                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    TrangThai = reader["TrangThai"]?.ToString() ?? string.Empty
                });
            }

            return list;
        }

        public async Task<List<UserListItem>> SearchUsersAsync(int currentUserId,
                                                               string search,
                                                               int? roleId,
                                                               string? status)
        {
            var list = new List<UserListItem>();
            var sql = new StringBuilder(
                @"
    SELECT
        nv.Id,
        nv.Ten,
        nv.Username,
        nv.RoleId,
        r.Name AS RoleName,
        nv.TrangThai,
        nv.CreatedAt,
        nv.LastLogin
    FROM NhanVien nv
    LEFT JOIN Roles r ON nv.RoleId = r.Id
    WHERE nv.Id <> @CurrentUserId");

            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(string.Empty, conn);
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

            if (!string.IsNullOrWhiteSpace(status) &&
                !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                sql.Append(" AND nv.TrangThai = @Status");
                cmd.Parameters.AddWithValue("@Status", status);
            }

            sql.Append(" ORDER BY nv.Id DESC;");
            cmd.CommandText = sql.ToString();

            await conn.OpenAsync().ConfigureAwait(false);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(new UserListItem
                {
                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                    Ten = reader["Ten"]?.ToString() ?? string.Empty,
                    Username = reader["Username"]?.ToString() ?? string.Empty,
                    RoleId = reader["RoleId"] != DBNull.Value
                               ? Convert.ToInt32(reader["RoleId"])
                               : 0,
                    RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
                    TrangThai = reader["TrangThai"]?.ToString() ?? string.Empty,
                    CreatedAt = reader["CreatedAt"] != DBNull.Value
                                  ? Convert.ToDateTime(reader["CreatedAt"])
                                  : DateTime.MinValue,
                    LastLogin = reader["LastLogin"] == DBNull.Value
                                  ? null
                                  : Convert.ToDateTime(reader["LastLogin"])
                });
            }

            return list;
        }

        public async Task<bool> UsernameExistsAsync(string username,
                                                    int? excludeUserId = null)
        {
            const string sql =
                @"
    SELECT COUNT(1)
    FROM NhanVien
    WHERE Username = @Username
      AND (@ExcludeUserId IS NULL OR Id <> @ExcludeUserId);";

            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Username", username.Trim());
            cmd.Parameters.AddWithValue("@ExcludeUserId", excludeUserId.HasValue
                                                              ? excludeUserId.Value
                                                              : DBNull.Value);

            await conn.OpenAsync().ConfigureAwait(false);
            var count =
                Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
            return count > 0;
        }

        public async Task<int> CreateUserAsync(UserUpsertModel model)
        {
            const string sql =
                @"
    INSERT INTO NhanVien (Ten, Username, [Password], TrangThai, RoleId, CreatedAt)
    VALUES (@Ten, @Username, @Password, @TrangThai, @RoleId, SYSUTCDATETIME());
    SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Ten", model.Ten.Trim());
            cmd.Parameters.AddWithValue("@Username", model.Username.Trim());
            cmd.Parameters.AddWithValue("@Password", model.Password);
            cmd.Parameters.AddWithValue("@TrangThai", model.TrangThai.Trim());
            cmd.Parameters.AddWithValue("@RoleId", model.RoleId);

            await conn.OpenAsync().ConfigureAwait(false);
            return Convert.ToInt32(
                await cmd.ExecuteScalarAsync().ConfigureAwait(false));
        }

        public async Task UpdateUserAsync(int id, string ten, int roleId,
                                          string trangThai)
        {
            const string sql =
                @"
    UPDATE NhanVien
    SET Ten = @Ten,
        RoleId = @RoleId,
        TrangThai = @TrangThai
    WHERE Id = @Id;";

            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Ten", ten.Trim());
            cmd.Parameters.AddWithValue("@RoleId", roleId);
            cmd.Parameters.AddWithValue("@TrangThai", trangThai.Trim());

            await conn.OpenAsync().ConfigureAwait(false);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task DisableUserAsync(int id)
        {
            const string sql =
                "UPDATE NhanVien SET TrangThai = 'Disabled' WHERE Id = @Id;";
            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync().ConfigureAwait(false);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task ResetPasswordAsync(int id, string newPassword)
        {
            const string sql =
                "UPDATE NhanVien SET [Password] = @Password WHERE Id = @Id;";
            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Password", newPassword);
            await conn.OpenAsync().ConfigureAwait(false);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task UpdateCurrentUserProfileAsync(int id, string ten,
                                                        string username)
        {
            const string sql =
                @"
    UPDATE NhanVien
    SET
        Ten = @Ten,
        Username = @Username
    WHERE Id = @Id;";

            using var conn = new SqlConnection(_db.GetConnectionString());

            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Ten", ten.Trim());
            cmd.Parameters.AddWithValue("@Username", username.Trim());

            await conn.OpenAsync().ConfigureAwait(false);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<bool> VerifyCurrentPasswordAsync(int userId,
                                                           string oldPassword)
        {
            const string sql =
                @"
    SELECT COUNT(1)
    FROM NhanVien
    WHERE Id = @Id
    AND [Password] = @Password;";

            using var conn = new SqlConnection(_db.GetConnectionString());

            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Id", userId);
            cmd.Parameters.AddWithValue("@Password", oldPassword);

            await conn.OpenAsync().ConfigureAwait(false);

            int count =
                Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));

            return count > 0;
        }
        public async Task ChangePasswordAsync(int userId, string newPassword)
        {
            const string sql =
                @"
    UPDATE NhanVien
    SET [Password] = @Password
    WHERE Id = @Id;";

            using var conn = new SqlConnection(_db.GetConnectionString());

            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Id", userId);
            cmd.Parameters.AddWithValue("@Password", newPassword);

            await conn.OpenAsync().ConfigureAwait(false);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        public async Task<NhanVien?> GetUserByIdAsync(int id)
        {
            const string sql =
                @"
SELECT TOP 1
    Id,
    Ten,
    Username,
    [Password],
    TrangThai,
    RoleId,
    CreatedAt,
    LastLogin
FROM NhanVien
WHERE Id = @Id;";

            using var conn = new SqlConnection(_db.GetConnectionString());
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync().ConfigureAwait(false);

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                return new NhanVien
                {
                    Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,

                    Ten = reader["Ten"]?.ToString() ?? "",

                    Username = reader["Username"]?.ToString() ?? "",

                    Password = reader["Password"]?.ToString() ?? "",

                    TrangThai = reader["TrangThai"]?.ToString() ?? "",

                    RoleId = reader["RoleId"] != DBNull.Value
                               ? Convert.ToInt32(reader["RoleId"])
                               : 0,

                    CreatedAt = reader["CreatedAt"] != DBNull.Value
                                  ? Convert.ToDateTime(reader["CreatedAt"])
                                  : DateTime.MinValue,

                    LastLogin = reader["LastLogin"] == DBNull.Value
                                  ? null
                                  : Convert.ToDateTime(reader["LastLogin"])
                };
            }

            return null;
        }
    }
}
