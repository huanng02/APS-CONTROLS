using System;

namespace QuanLyGiuXe.Models
{
    public enum SyncStatus
    {
        Pending,
        Syncing,
        Completed,
        Failed
    }

    public class PendingTransaction
    {
        public int Id { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public string? PayloadJson { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime? LastRetryUtc { get; set; }
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? TransactionType { get; set; } // e.g., "INSERT_XE_VAO", "UPDATE_XE_RA"
        public string? Fingerprint { get; set; } // Idempotency key to prevent duplicates
        
        // Helper for UI
        public string StatusColor => SyncStatus switch
        {
            SyncStatus.Pending => "#3498DB",
            SyncStatus.Syncing => "#F1C40F",
            SyncStatus.Completed => "#2ECC71",
            SyncStatus.Failed => "#E74C3C",
            _ => "#95A5A6"
        };
    }
}
