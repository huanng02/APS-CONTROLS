using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class OfflineQueueService
    {
        private static readonly Lazy<OfflineQueueService> _lazy = new(() => new OfflineQueueService());
        public static OfflineQueueService Instance => _lazy.Value;

        private OfflineQueueService() { }

        public async Task EnqueueAsync(string type, object payload, string? fingerprint = null)
        {
            var tx = new PendingTransaction
            {
                TransactionType = type,
                PayloadJson = JsonConvert.SerializeObject(payload),
                SyncStatus = SyncStatus.Pending,
                CreatedUtc = DateTime.UtcNow,
                Fingerprint = fingerprint
            };

            int id = await OfflineCacheService.Instance.EnqueueTransactionAsync(tx);
            if (id > 0)
                LoggingService.Instance.LogInfo("QUEUE_ADD", "OfflineQueue", $"Added transaction {id} of type {type}");
        }

        public async Task<List<PendingTransaction>> GetPendingAsync()
        {
            return await OfflineCacheService.Instance.GetPendingTransactionsAsync();
        }

        public async Task MarkCompletedAsync(int id)
        {
            await OfflineCacheService.Instance.UpdateTransactionStatusAsync(id, SyncStatus.Completed);
            LoggingService.Instance.LogInfo("QUEUE_SYNC", "OfflineQueue", $"Transaction {id} synced successfully");
        }

        public async Task MarkFailedAsync(int id, string error)
        {
            await OfflineCacheService.Instance.UpdateTransactionStatusAsync(id, SyncStatus.Failed, error);
            LoggingService.Instance.LogError("QUEUE_FAIL", "OfflineQueue", $"Transaction {id} failed: {error}", null);
        }
    }
}
