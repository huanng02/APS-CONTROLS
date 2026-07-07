using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services.Connection
{
    public class CameraDiagnosticsService
    {
        private static readonly Lazy<CameraDiagnosticsService> _instance =
            new(() => new CameraDiagnosticsService());

        public static CameraDiagnosticsService Instance => _instance.Value;

        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true)]
        private static extern int SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int maximumWorkingSetSize);

        private CancellationTokenSource? _cts;
        private bool _isRunning;

        private CameraDiagnosticsService() { }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            // Trim memory immediately on startup
            TrimMemory();
            
            Task.Run(() => DiagnosticsLoopAsync(_cts.Token));
            try { LoggingService.Instance.LogInfo("DIAGNOSTICS", "CameraDiagnostics", "Diagnostics service started"); } catch { }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts?.Cancel();
            _cts?.Dispose();
            try { LoggingService.Instance.LogInfo("DIAGNOSTICS", "CameraDiagnostics", "Diagnostics service stopped"); } catch { }
        }

        public void TrimMemory()
        {
            try
            {
                // Force collection of all generations and Large Object Heap (LOH)
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                
                // Trim physical memory pages back to OS
                using (var currentProcess = Process.GetCurrentProcess())
                {
                    SetProcessWorkingSetSize(currentProcess.Handle, -1, -1);
                }
            }
            catch { }
        }

        private async Task DiagnosticsLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000, token); // Run every 10 seconds

                    // Perform memory collection and working set trim
                    TrimMemory();

                    // 1. Gather memory metrics
                    var currentProcess = Process.GetCurrentProcess();
                    long workingSetMb = currentProcess.WorkingSet64 / (1024 * 1024);
                    long privateMemoryMb = currentProcess.PrivateMemorySize64 / (1024 * 1024);
                    long gcMemoryMb = GC.GetTotalMemory(false) / (1024 * 1024);

                    // 2. Gather active camera connection metrics
                    var connections = CameraConnectionManager.Instance.GetConnections();
                    int activeConnectionsCount = connections.Count;

                    string connDetails = "";
                    foreach (var kvp in connections)
                    {
                        var conn = kvp.Value;
                        string consumers = string.Join(", ", conn.Consumers);
                        connDetails += $"\n- URL: {kvp.Key} (Connected: {conn.IsConnected}, Consumers: [{consumers}], Reconnects: {conn.ReconnectCount}, FPS: {conn.CurrentFps:F1})";
                    }

                    string logMsg = $"[MEM] WorkingSet: {workingSetMb}MB, PrivateMemory: {privateMemoryMb}MB, GCMemory: {gcMemoryMb}MB. [CAM] Active Streams: {activeConnectionsCount}.Details: {connDetails}";
                    try { LoggingService.Instance.LogInfo("DIAGNOSTICS", "CameraDiagnostics", logMsg); } catch { }

                    // 3. Warning conditions
                    if (workingSetMb > 600)
                    {
                        try 
                        { 
                            LoggingService.Instance.LogWarning("HIGH_MEM_WARN", "CameraDiagnostics", 
                                $"Process Working Set memory usage is high: {workingSetMb}MB (exceeds threshold 600MB)."); 
                        } 
                        catch { }
                    }

                    // Check for excessive reconnect counts
                    foreach (var kvp in connections)
                    {
                        if (kvp.Value.ReconnectCount > 5)
                        {
                            try
                            {
                                LoggingService.Instance.LogWarning("RECONNECT_LOOP_WARN", "CameraDiagnostics",
                                    $"Camera stream {kvp.Key} has experienced excessive reconnects ({kvp.Value.ReconnectCount}).");
                            }
                            catch { }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    try { LoggingService.Instance.LogError("DIAGNOSTICS_ERROR", "CameraDiagnostics", "Error in diagnostics loop", ex); } catch { }
                }
            }
        }
    }
}
