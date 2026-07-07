    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;


namespace QuanLyGiuXe.Models
{
    public class NhanVien
    {
        public int Id { get; set; }

        public string Ten { get; set; } = "";

        public string Username { get; set; } = "";

        public string Password { get; set; } = "";

        public string TrangThai { get; set; } = "";

        public int RoleId { get; set; }

        public string? Role { get; set; }

        // =========================
        // Database Fields
        // =========================

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLogin { get; set; }
    }
}
