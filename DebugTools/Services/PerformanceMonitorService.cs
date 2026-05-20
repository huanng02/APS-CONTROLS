using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyGiuXe.DebugTools.Services
{
    public class PerformanceMonitorService : INotifyPropertyChanged
    {
        private static readonly Lazy<PerformanceMonitorService> _lazy = new Lazy<PerformanceMonitorService>(() => new PerformanceMonitorService());
        public static PerformanceMonitorService Instance => _lazy.Value;

        private double _cpuUsage;
        private double _ramUsageMb;
        private int _activeThreads;
        private Process _currentProcess;

        public event PropertyChangedEventHandler PropertyChanged;

        private PerformanceMonitorService()
        {
            _currentProcess = Process.GetCurrentProcess();
            StartMonitoring();
        }

        public double CpuUsage { get => _cpuUsage; set { _cpuUsage = value; OnPropertyChanged(); } }
        public double RamUsageMb { get => _ramUsageMb; set { _ramUsageMb = value; OnPropertyChanged(); } }
        public int ActiveThreads { get => _activeThreads; set { _activeThreads = value; OnPropertyChanged(); } }

        private void StartMonitoring()
        {
            Task.Run(async () =>
            {
                var lastTime = DateTime.Now;
                var lastCpuTime = _currentProcess.TotalProcessorTime;

                while (true)
                {
                    await Task.Delay(1000);
                    
                    var currentTime = DateTime.Now;
                    var currentCpuTime = _currentProcess.TotalProcessorTime;
                    
                    var cpuUsedMs = (currentCpuTime - lastCpuTime).TotalMilliseconds;
                    var totalMsPassed = (currentTime - lastTime).TotalMilliseconds;
                    var cpuUsagePercent = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;

                    CpuUsage = Math.Round(cpuUsagePercent, 2);
                    RamUsageMb = Math.Round(_currentProcess.WorkingSet64 / 1024.0 / 1024.0, 2);
                    ActiveThreads = _currentProcess.Threads.Count;

                    lastTime = currentTime;
                    lastCpuTime = currentCpuTime;
                }
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
