using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyGiuXe.DebugTools.Simulations
{
    public class SyncSimulationService : INotifyPropertyChanged
    {
        private static readonly Lazy<SyncSimulationService> _lazy = new Lazy<SyncSimulationService>(() => new SyncSimulationService());
        public static SyncSimulationService Instance => _lazy.Value;

        private int _pendingQueueCount = 0;
        private double _syncProgress = 0;
        private string _syncSpeed = "0 kb/s";
        private int _failedSyncCount = 0;
        private int _retryQueue = 0;
        private DateTime _lastSyncTime = DateTime.Now;
        private int _currentBatchSize = 0;
        private string _syncDuration = "0ms";

        public event PropertyChangedEventHandler PropertyChanged;

        private SyncSimulationService() { }

        public int PendingQueueCount { get => _pendingQueueCount; set { _pendingQueueCount = value; OnPropertyChanged(); } }
        public double SyncProgress { get => _syncProgress; set { _syncProgress = value; OnPropertyChanged(); } }
        public string SyncSpeed { get => _syncSpeed; set { _syncSpeed = value; OnPropertyChanged(); } }
        public int FailedSyncCount { get => _failedSyncCount; set { _failedSyncCount = value; OnPropertyChanged(); } }
        public int RetryQueue { get => _retryQueue; set { _retryQueue = value; OnPropertyChanged(); } }
        public DateTime LastSyncTime { get => _lastSyncTime; set { _lastSyncTime = value; OnPropertyChanged(); } }
        public int CurrentBatchSize { get => _currentBatchSize; set { _currentBatchSize = value; OnPropertyChanged(); } }
        public string SyncDuration { get => _syncDuration; set { _syncDuration = value; OnPropertyChanged(); } }

        public void StartStressSync()
        {
            Task.Run(async () =>
            {
                PendingQueueCount = 10000;
                while (PendingQueueCount > 0)
                {
                    CurrentBatchSize = new Random().Next(10, 50);
                    SyncProgress = (10000 - PendingQueueCount) / 100.0;
                    SyncSpeed = new Random().Next(100, 500) + " kb/s";
                    PendingQueueCount -= CurrentBatchSize;
                    SyncDuration = new Random().Next(50, 200) + "ms";
                    LastSyncTime = DateTime.Now;
                    
                    if (new Random().Next(0, 100) < 5) 
                    {
                        FailedSyncCount++;
                        RetryQueue++;
                    }

                    await Task.Delay(500);
                }
                SyncProgress = 100;
                PendingQueueCount = 0;
                SyncSpeed = "0 kb/s";
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
