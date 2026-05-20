using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services.Connection
{
    public class C3200Resource : IConnectionResource
    {
        public string ResourceId => "C3200";
        public ResourceType Type => ResourceType.C3200Controller;

        public async Task<bool> CheckHealthAsync(CancellationToken token)
        {
            // Tận dụng logic ping sẵn có
            return await ConnectionMonitorService.Instance.CheckC3Async(token);
        }

        public async Task<bool> ReconnectAsync(CancellationToken token)
        {
            // Với C3, reconnect có thể bao gồm việc khởi tạo lại SDK nếu cần
            // Ở đây bước đầu ta dùng check health (Ping)
            bool isPingable = await CheckHealthAsync(token);
            if (isPingable)
            {
                // Thử connect SDK nếu cần (giả định Service tự quản lý session)
                return true;
            }
            return false;
        }
    }
}
