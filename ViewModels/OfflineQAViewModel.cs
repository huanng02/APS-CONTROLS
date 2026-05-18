using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.ViewModels
{
    public class OfflineQAViewModel : BaseViewModel
    {
        private ConnectivityStateService _connService = ConnectivityStateService.Instance;
        private OfflineCacheService _cacheService = OfflineCacheService.Instance;
        private OfflineQueueService _queueService = OfflineQueueService.Instance;
        private DispatcherTimer _refreshTimer;

        public ConnectivityStateService Conn => _connService;

        private ObservableCollection<PendingTransaction> _transactions = new();
        public ObservableCollection<PendingTransaction> Transactions
        {
            get => _transactions;
            set { _transactions = value; OnPropertyChanged(nameof(Transactions)); }
        }

        private ObservableCollection<AuditLogModel> _auditLogs = new();
        public ObservableCollection<AuditLogModel> AuditLogs
        {
            get => _auditLogs;
            set { _auditLogs = value; OnPropertyChanged(nameof(AuditLogs)); }
        }

        private string _testLog = string.Empty;
        public string TestLog
        {
            get => _testLog;
            set { _testLog = value; OnPropertyChanged(nameof(TestLog)); }
        }

        // Telemetry properties
        private int _telemetryLiveVehicleCount;
        public int TelemetryLiveVehicleCount
        {
            get => _telemetryLiveVehicleCount;
            set { _telemetryLiveVehicleCount = value; OnPropertyChanged(nameof(TelemetryLiveVehicleCount)); }
        }

        private int _telemetryActiveSessions;
        public int TelemetryActiveSessions
        {
            get => _telemetryActiveSessions;
            set { _telemetryActiveSessions = value; OnPropertyChanged(nameof(TelemetryActiveSessions)); }
        }

        private int _telemetrySuspiciousSessions;
        public int TelemetrySuspiciousSessions
        {
            get => _telemetrySuspiciousSessions;
            set { _telemetrySuspiciousSessions = value; OnPropertyChanged(nameof(TelemetrySuspiciousSessions)); }
        }

        private int _telemetryPendingOfflineSync;
        public int TelemetryPendingOfflineSync
        {
            get => _telemetryPendingOfflineSync;
            set { _telemetryPendingOfflineSync = value; OnPropertyChanged(nameof(TelemetryPendingOfflineSync)); }
        }

        public ICommand ForceOfflineCommand { get; }
        public ICommand ForceOnlineCommand { get; }
        public ICommand TestReadCacheCommand { get; }
        public ICommand TestWriteQueueCommand { get; }
        public ICommand RefreshQueueCommand { get; }
        public ICommand StressTestCommand { get; }

        // Simulation and Resilience Commands
        public ICommand SimulateEntryCrashCommand { get; }
        public ICommand SimulateDuplicateActiveCommand { get; }
        public ICommand ForceAutoRepairCommand { get; }

        public OfflineQAViewModel()
        {
            ForceOfflineCommand = new RelayCommand(_ => { _connService.IsSimulatingOffline = true; AddLog("Forced Offline Mode"); _ = RefreshAllAsync(); });
            ForceOnlineCommand = new RelayCommand(_ => { _connService.IsSimulatingOffline = false; AddLog("Restored Online Mode"); _ = RefreshAllAsync(); });
            
            TestReadCacheCommand = new RelayCommand(async _ => await RunReadTest());
            TestWriteQueueCommand = new RelayCommand(async _ => await RunWriteTest());
            RefreshQueueCommand = new RelayCommand(async _ => await RefreshAllAsync());
            StressTestCommand = new RelayCommand(async _ => await RunStressTest());

            SimulateEntryCrashCommand = new RelayCommand(async _ => await RunSimulateEntryCrash());
            SimulateDuplicateActiveCommand = new RelayCommand(async _ => await RunSimulateDuplicateActive());
            ForceAutoRepairCommand = new RelayCommand(async _ => await RunForceAutoRepair());

            _ = RefreshAllAsync();

            // Set up a dispatcher timer to auto-refresh every 2 seconds
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += async (s, e) => await RefreshAllAsync();
            _refreshTimer.Start();
        }

        private void AddLog(string msg)
        {
            TestLog = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + TestLog;
            if (TestLog.Length > 5000) TestLog = TestLog.Substring(0, 5000);
        }

        private async Task RefreshAllAsync()
        {
            try
            {
                // Refresh Queue
                var items = await _queueService.GetPendingAsync();
                Transactions = new ObservableCollection<PendingTransaction>(items);

                // Refresh Audits
                var audits = await SessionAuditService.Instance.GetAuditLogsAsync();
                AuditLogs = new ObservableCollection<AuditLogModel>(audits);

                // Refresh Telemetry
                TelemetryLiveVehicleCount = await GarageStateService.Instance.GetCurrentVehicleCount();
                TelemetryActiveSessions = TelemetryLiveVehicleCount;
                TelemetryPendingOfflineSync = await GarageStateService.Instance.GetPendingSyncCount();
                TelemetrySuspiciousSessions = await GarageStateService.Instance.GetSuspiciousSessionsCount();
            }
            catch (Exception ex)
            {
                AddLog($"Refresh error: {ex.Message}");
            }
        }

        private async Task RunSimulateEntryCrash()
        {
            int mockCardId = 999;
            string plate = "30A-99999";
            
            AddLog("Simulating Entry Crash for Card 999...");
            
            // Write strictly to SQLite ONLY (simulating app crash right after swiping before SQL Sync runs)
            await _cacheService.SaveActiveSessionLocalAsync(mockCardId, plate, DateTime.Now, "mock_image_path");
            
            AddLog("Saved local SQLite transaction. (Card 999 successfully locked inside Lot locally).");
            AddLog("Simulation completed. Recovery Engine will verify on startup or next auto-repair cycle.");
            
            await RefreshAllAsync();
        }

        private async Task RunSimulateDuplicateActive()
        {
            int cardId = 888;
            AddLog("Simulating Duplicate Active Sessions for Card 888...");

            // Insert session A locally
            await _cacheService.SaveActiveSessionLocalAsync(cardId, "DUP-888-A", DateTime.Now.AddMinutes(-20), "");

            // Force insert session B directly into SQLite bypassing the single-active filter
            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
            using (var conn = new SqliteConnection($"Data Source={dbPath};Default Timeout=5;"))
            {
                await conn.OpenAsync();
                string sql = "INSERT INTO LocalXeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe, IsSynced) VALUES (@cardId, 'DUP-888-B', @time, '', 0)";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", cardId);
                    cmd.Parameters.AddWithValue("@time", DateTime.Now);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Log raw insertion to audit trail
            await SessionAuditService.Instance.LogAuditAsync(
                cardId.ToString(),
                "CORRUPTION",
                "Active",
                "DuplicateActive",
                "Simulated direct insertion of duplicate active session for test card"
            );

            AddLog("Corrupted state inserted successfully: Card 888 now has 2 parallel active sessions in local SQLite.");
            
            await RefreshAllAsync();
        }

        private async Task RunForceAutoRepair()
        {
            AddLog("Triggering manual Force Auto-Repair...");
            int repaired = await SessionRepairService.Instance.RepairAllInconsistenciesAsync();
            AddLog($"Auto-Repair execution complete. Total issues self-healed: {repaired}");
            await RefreshAllAsync();
        }

        private async Task RunReadTest()
        {
            AddLog("Running Read Test (Lookup LoaiXe)...");
            var result = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<System.Collections.Generic.List<LoaiXe>>(
                "LOOKUP_LOAIXE",
                async conn => {
                    await Task.Delay(500);
                    return new System.Collections.Generic.List<LoaiXe> { new LoaiXe { Id = 1, TenLoai = "SQL_SERVER_DATA" } };
                }
            );

            if (result != null && result.Any())
            {
                AddLog($"Read Success: {result.First().TenLoai}");
            }
            else
            {
                AddLog("Read Failed: No data found in SQL or Cache.");
            }
        }

        private async Task RunWriteTest()
        {
            AddLog("Running Write Test...");
            bool success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "TEST_WRITE",
                new { Data = "Test Payload", Time = DateTime.Now },
                async conn => {
                    if (_connService.IsSimulatingOffline) throw new Exception("SQL Offline");
                    await Task.Delay(500);
                }
            );

            if (success) AddLog("Write Success: Directly to SQL.");
            else AddLog("Write Fail: Queued to SQLite.");

            await RefreshAllAsync();
        }

        private async Task RunStressTest()
        {
            AddLog("Starting Stress Test (1000 items)...");
            var tasks = Enumerable.Range(1, 1000).Select(i => 
                _queueService.EnqueueAsync("STRESS_TEST", new { Index = i, Msg = "Massive stress test" })
            );
            await Task.WhenAll(tasks);
            AddLog("Stress Test Finished.");
            await RefreshAllAsync();
        }
    }
}
