using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
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

        public ConnectivityStateService Conn => _connService;

        private ObservableCollection<PendingTransaction> _transactions = new();
        public ObservableCollection<PendingTransaction> Transactions
        {
            get => _transactions;
            set { _transactions = value; OnPropertyChanged(nameof(Transactions)); }
        }

        private string _testLog = string.Empty;
        public string TestLog
        {
            get => _testLog;
            set { _testLog = value; OnPropertyChanged(nameof(TestLog)); }
        }

        public ICommand ForceOfflineCommand { get; }
        public ICommand ForceOnlineCommand { get; }
        public ICommand TestReadCacheCommand { get; }
        public ICommand TestWriteQueueCommand { get; }
        public ICommand RefreshQueueCommand { get; }
        public ICommand StressTestCommand { get; }
        public ICommand TestOfflineCardValidationCommand { get; }

        public OfflineQAViewModel()
        {
            ForceOfflineCommand = new RelayCommand(_ => { _connService.IsSimulatingOffline = true; AddLog("Forced Offline Mode"); });
            ForceOnlineCommand = new RelayCommand(_ => { _connService.IsSimulatingOffline = false; AddLog("Restored Online Mode"); });
            
            TestReadCacheCommand = new RelayCommand(async _ => await RunReadTest());
            TestWriteQueueCommand = new RelayCommand(async _ => await RunWriteTest());
            RefreshQueueCommand = new RelayCommand(async _ => await RefreshQueue());
            StressTestCommand = new RelayCommand(async _ => await RunStressTest());
            TestOfflineCardValidationCommand = new RelayCommand(async _ => await RunOfflineCardValidationTest());

            _ = RefreshQueue();
        }

        private void AddLog(string msg)
        {
            TestLog = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + TestLog;
            if (TestLog.Length > 5000) TestLog = TestLog.Substring(0, 5000);
        }

        private async Task RefreshQueue()
        {
            var items = await _queueService.GetPendingAsync();
            Transactions = new ObservableCollection<PendingTransaction>(items);
        }

        private async Task RunReadTest()
        {
            AddLog("Running Read Test (Lookup LoaiXe)...");
            var result = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<System.Collections.Generic.List<LoaiXe>>(
                "LOOKUP_LOAIXE",
                async conn => {
                    // Actual SQL query simulation
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
                    // Simulate SQL write failure if offline
                    if (_connService.IsSimulatingOffline) throw new Exception("SQL Offline");
                    await Task.Delay(500);
                }
            );

            if (success) AddLog("Write Success: Directly to SQL.");
            else AddLog("Write Fail: Queued to SQLite.");

            await RefreshQueue();
        }

        private async Task RunStressTest()
        {
            AddLog("Starting Stress Test (1000 items)...");
            var tasks = Enumerable.Range(1, 1000).Select(i => 
                _queueService.EnqueueAsync("STRESS_TEST", new { Index = i, Msg = "Massive stress test" })
            );
            await Task.WhenAll(tasks);
            AddLog("Stress Test Finished.");
            await RefreshQueue();
        }

        private async Task RunOfflineCardValidationTest()
        {
            AddLog("--- OFFLINE CARD VALIDATION TEST ---");
            bool wasSimulating = _connService.IsSimulatingOffline;
            
            try
            {
                // Force online to get a card
                _connService.IsSimulatingOffline = false;
                var cards = await new DatabaseService().GetRFIDCardsAsync();
                if (cards == null || cards.Count == 0)
                {
                    AddLog("No cards in DB to test.");
                    return;
                }

                var testCard = cards.First();
                AddLog($"Testing with Card UID: {testCard.UID} (Online mode)");
                
                // Simulate MainViewModel logic online
                var vm = new MainViewModel(); // just for testing logic if needed, or we just call the cache directly
                var onlineCard = new DatabaseService().GetRFIDCardByUid(testCard.UID);
                AddLog(onlineCard != null ? "✅ Online: Card found" : "❌ Online: Card not found");

                // Switch to offline
                _connService.IsSimulatingOffline = true;
                AddLog($"Switched to Offline Mode. Testing Card UID: {testCard.UID}");
                
                var offlineCard = await _cacheService.GetCardFromCacheAsync(testCard.UID);
                if (offlineCard != null)
                {
                    AddLog("✅ Offline: Card found in Cache");
                    AddLog($"Status: {(offlineCard.TrangThai == "Khóa" ? "Blacklisted" : "Active")}");
                    AddLog($"Expires: {(offlineCard.NgayHetHan.HasValue ? offlineCard.NgayHetHan.Value.ToString() : "Never")}");
                }
                else
                {
                    AddLog("❌ Offline: Card NOT found in Cache");
                }

                // Test unknown card
                string fakeUid = "UNKNOWN_12345";
                AddLog($"Testing Unknown Card UID: {fakeUid}");
                var unknownCard = await _cacheService.GetCardFromCacheAsync(fakeUid);
                AddLog(unknownCard == null ? "✅ Offline: Unknown card correctly rejected" : "❌ Offline: Unknown card was found!");
            }
            finally
            {
                // Restore state
                _connService.IsSimulatingOffline = wasSimulating;
            }
        }
    }
}
