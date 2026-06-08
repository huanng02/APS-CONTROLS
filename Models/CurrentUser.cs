using System;

namespace QuanLyGiuXe.Models
{
    public static class CurrentUser
    {
        public static int Id { get; set; }
        public static string? Username { get; set; }
        public static string? Role { get; set; }
        public static string? Ten { get; set; }

        public static void Clear()
        {
            Id = 0;
            Username = null;
            Role = null;
            Ten = null;
        }

        public static bool IsAuthenticated => !string.IsNullOrEmpty(Username);
    }
}
