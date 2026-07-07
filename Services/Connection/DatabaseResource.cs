using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services.Connection
{
    public class DatabaseResource : IConnectionResource
    {
        public string ResourceId => "Database";
        public ResourceType Type => ResourceType.Database;

        public async Task<bool> CheckHealthAsync(CancellationToken token)
        {
            // Tận dụng logic check db sẵn có
            return await ConnectionMonitorService.Instance.CheckDatabaseAsync(token);
        }

        public async Task<bool> ReconnectAsync(CancellationToken token)
        {
            // Với SQL, Reconnect thường là thử mở lại connection hoặc kiểm tra config
            // Ở đây đơn giản là chạy lại CheckHealth vì ConnectionManager tự quản lý conn string
            return await CheckHealthAsync(token);
        }
    }
}
