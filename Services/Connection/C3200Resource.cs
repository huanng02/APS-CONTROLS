using System.Linq;
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
            // Nếu máy này không phải owner → báo "healthy" để không trigger reconnect
            if (!await IsOwnerAsync())
                return true;

            // The resource is healthy ONLY if it is pingable AND the SDK is actually connected
            bool isPingable = await ConnectionMonitorService.Instance.CheckC3Async(token);
            return isPingable && C3200Service.Instance.IsConnected;
        }

        public async Task<bool> ReconnectAsync(CancellationToken token)
        {
            // Nếu máy này không phải owner → không reconnect
            if (!await IsOwnerAsync())
                return true; // trả true để AutoReconnect không coi đây là lỗi

            // If the controller is pingable, try to connect the SDK if it's currently disconnected
            bool isPingable = await ConnectionMonitorService.Instance.CheckC3Async(token);
            if (isPingable)
            {
                if (!C3200Service.Instance.IsConnected)
                {
                    return await C3200Service.Instance.ConnectAsync();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra xem máy hiện tại có phải owner của controller đang được cấu hình không.
        /// Đọc PcIp từ database theo IpAddress trong config.json.
        /// </summary>
        private static async Task<bool> IsOwnerAsync()
        {
            try
            {
                var cfg = AppConfig.Load();
                var controllerIp = cfg.ZKTeco?.IpAddress;
                if (string.IsNullOrWhiteSpace(controllerIp))
                    return true; // chưa cấu hình → không giới hạn

                var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var matched = allControllers
                    .FirstOrDefault(c => c.IsActive &&
                        c.IpAddress.Trim().Equals(controllerIp.Trim(), System.StringComparison.OrdinalIgnoreCase));

                if (matched == null || string.IsNullOrWhiteSpace(matched.PcIp))
                    return true; // không có cấu hình PcIp → không giới hạn

                return C3200Service.IsOwnerOfController(matched.PcIp);
            }
            catch
            {
                return true; // fail-safe: nếu lỗi vẫn cho phép
            }
        }
    }
}
