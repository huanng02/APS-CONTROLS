using System;
using System.IO;
using System.Text.Json;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Models
{
    public class DbConnectionConfig
    {
        public string ServerIP { get; set; } = "192.168.2.13";
        public string Port { get; set; } = "1433";
        public string Database { get; set; } = "BaiXe";
        public string Username { get; set; } = "sa";
        public string Password { get; set; } = "";
        
        public string BuildConnectionString(int timeout = 15)
        {
            return $"Server={ServerIP},{Port};Database={Database};User Id={Username};Password={Password};TrustServerCertificate=True;Connect Timeout={timeout};";
        }

        public static void SaveToFile(DbConnectionConfig config, string filePath = "dbconfig.json")
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(filePath, json);
                LoggingService.Instance.LogInfo("DbConnectionConfig", "Save", "Lưu cấu hình DB thành công vào " + filePath);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DbConnectionConfig", "Save", "Lỗi lưu cấu hình DB", ex);
            }
        }

        public static DbConnectionConfig LoadFromFile(string filePath = "dbconfig.json")
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<DbConnectionConfig>(json) ?? new DbConnectionConfig();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DbConnectionConfig", "Load", "Lỗi đọc cấu hình DB, dùng mặc định", ex);
            }
            return new DbConnectionConfig();
        }
    }
}
