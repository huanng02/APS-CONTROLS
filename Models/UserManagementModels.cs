using System;

namespace QuanLyGiuXe.Models
{
    public sealed class UserListItem
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string TrangThai { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public sealed class RoleOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TrangThai { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public sealed class UserUpsertModel
    {
        public string Ten { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string TrangThai { get; set; } = "Active";
    }
}
