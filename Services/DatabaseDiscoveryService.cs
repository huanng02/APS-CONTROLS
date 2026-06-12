using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class DatabaseDiscoveryService
    {
        public static async Task<List<string>> DiscoverDatabasesAsync(ConnectionConfig config, CancellationToken cancellationToken = default)
        {
            // Tải danh sách bằng cách kết nối tạm tới master database
            var masterConfig = new ConnectionConfig
            {
                ServerIP = config.ServerIP,
                Port = config.Port,
                Username = config.Username,
                Password = config.Password,
                Database = "master"
            };

            var dbs = new List<string>();
            try
            {
                using var conn = await SqlConnectionService.GetConnectionAsync(masterConfig, timeout: 5, cancellationToken);
                using var cmd = new SqlCommand(@"SELECT name FROM sys.databases WHERE HAS_DBACCESS(name) = 1 ORDER BY name;", conn);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    dbs.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DB_DISCOVERY", "Discover", "Lỗi khám phá danh sách database", ex);
                throw;
            }
            return dbs;
        }
    }
}
