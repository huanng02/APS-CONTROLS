using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class SqlConnectionService
    {
        public static async Task<SqlConnection> GetConnectionAsync(ConnectionConfig config, int timeout = 5, CancellationToken cancellationToken = default)
        {
            string connStr = config.BuildConnectionString(timeout);
            var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken);
            return conn;
        }

        public static async Task<bool> TestConnectionAsync(ConnectionConfig config, int timeout = 5, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = await GetConnectionAsync(config, timeout, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
