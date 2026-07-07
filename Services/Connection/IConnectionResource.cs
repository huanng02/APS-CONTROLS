using System.Threading;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services.Connection
{
    public interface IConnectionResource
    {
        string ResourceId { get; }
        ResourceType Type { get; }
        Task<bool> CheckHealthAsync(CancellationToken token);
        Task<bool> ReconnectAsync(CancellationToken token);
    }
}
