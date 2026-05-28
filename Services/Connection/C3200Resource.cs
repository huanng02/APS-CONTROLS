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
            // The resource is healthy ONLY if it is pingable AND the SDK is actually connected
            bool isPingable = await ConnectionMonitorService.Instance.CheckC3Async(token);
            return isPingable && C3200Service.Instance.IsConnected;
        }

        public async Task<bool> ReconnectAsync(CancellationToken token)
        {
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
    }
}
