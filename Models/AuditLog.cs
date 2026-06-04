using System;

namespace QuanLyGiuXe.Models
{
    public sealed class AuditLog
    {
        public long AuditLogId { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? OldValue { get; set; } // Stored as JSON string
        public string? NewValue { get; set; } // Stored as JSON string
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
