using System;

namespace QuanLyGiuXe.Models
{
    public class ConnectionConfig
    {
        public string ServerIP { get; set; } = "localhost";
        public string Port { get; set; } = "1433";
        public string Database { get; set; } = "";
        public string Username { get; set; } = "sa";
        public string Password { get; set; } = ""; // Mật khẩu có thể đã được mã hóa hoặc thô tùy ngữ cảnh
        public bool RememberConnection { get; set; } = true;

        public string SecondaryServerIP { get; set; } = "";
        public string SecondaryPort { get; set; } = "1433";
        public string SecondaryDatabase { get; set; } = "BaiXe";
        public string SecondaryUsername { get; set; } = "sa";
        public string SecondaryPassword { get; set; } = "";

        public string BuildConnectionString(int timeout = 5)
        {
            return $"Server={ServerIP},{Port};Database={Database};User Id={Username};Password={Password};TrustServerCertificate=True;Connect Timeout={timeout};";
        }
    }
}
