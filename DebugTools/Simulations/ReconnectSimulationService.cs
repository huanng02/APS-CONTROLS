#if DEBUG
using System;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Services.Connection;

namespace QuanLyGiuXe.DebugTools.Simulations
{
    public class ReconnectSimulationService
    {
        public void SimulateSqlDisconnect()
        {
            // Stop the monitor and simulate a failure
            ConnectionMonitorService.Instance.Stop();
            LoggingService.Instance.LogWarning("QA_SIM", "SQL", "Simulating SQL Disconnect");
        }

        public async Task SimulateSqlReconnect()
        {
            ConnectionMonitorService.Instance.Start();
            await ConnectionMonitorService.Instance.ForceCheckAsync();
        }

        public async Task SimulateNetworkFailureAsync()
        {
            // Simulate a network drop by stopping all monitors and logging failures
            ConnectionMonitorService.Instance.Stop();
            // In a real scenario, we might want to flag a 'NetworkDown' state in a mock
            await Task.Delay(500);
            LoggingService.Instance.LogError("QA_SIM", "Network", "Simulated Global Network Failure");
        }
    }
}
#endif
