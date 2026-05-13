using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services.Connection
{
    public class CameraResource : IConnectionResource
    {
        private readonly string _camKey;
        private readonly CameraService _cameraService;

        public string ResourceId => $"Camera_{_camKey}";
        public ResourceType Type => ResourceType.Camera;

        public CameraResource(string camKey, CameraService cameraService)
        {
            _camKey = camKey;
            _cameraService = cameraService;
        }

        public Task<bool> CheckHealthAsync(CancellationToken token)
        {
            return Task.FromResult(_cameraService.IsConnected(_camKey));
        }

        public async Task<bool> ReconnectAsync(CancellationToken token)
        {
            string url = _cameraService.GetUrl(_camKey);
            if (string.IsNullOrEmpty(url)) return false;

            // Restart camera stream
            _cameraService.StartIpCamera(_camKey, url);
            
            // Đợi một chút để xem có lên không
            await Task.Delay(2000, token);
            return _cameraService.IsConnected(_camKey);
        }
    }
}
