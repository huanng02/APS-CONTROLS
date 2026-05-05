using System;

namespace QuanLyGiuXe.Models
{
    /// <summary>
    /// Lightweight DTO for a button/RT log row.
    /// Kept as a mutable POCO to work with data adapters and ObservableCollection.
    /// </summary>
    public sealed class ButtonPressLog
    {
        /// <summary>Primary key.</summary>
        public int Id { get; set; }

        /// <summary>Event timestamp (local time).</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Door number (1/2) if available.</summary>
        public byte? Door { get; set; }

        /// <summary>Device event type (if any).</summary>
        public int? EventType { get; set; }

        /// <summary>Card UID read by the device (if any).</summary>
        public string? CardNo { get; set; }

        /// <summary>Action label (e.g. "BUTTON_PRESS", "MANUAL_OPEN").</summary>
        public string? Action { get; set; }

        /// <summary>Barrier open result: 1 = opened, 0 = failed, null = unknown/not recorded.</summary>
        public byte? BarrierResult { get; set; }

        /// <summary>Path to cropped plate image.</summary>
        public string? PlateImagePath { get; set; }

        /// <summary>Path to full image.</summary>
        public string? FullImagePath { get; set; }

        /// <summary>Raw event string returned by device.</summary>
        public string? RawData { get; set; }

        /// <summary>Additional notes (e.g. sdk return, diagnostics).</summary>
        public string? Notes { get; set; }

        /// <summary>Optional In/Out state reported by device.</summary>
        public int? InOutState { get; set; }

        /// <summary>PIN entered on device, if provided.</summary>
        public int? Pin { get; set; }

        /// <summary>Operator name (for manual actions).</summary>
        public string? Operator { get; set; }

        /// <summary>Source IP or device identifier.</summary>
        public string? SourceIp { get; set; }

        /// <summary>Short debug representation.</summary>
        public override string ToString()
        {
            return $"[{Id}] {Timestamp:g} door={Door?.ToString() ?? "-"} action={Action ?? "-"} barrier={BarrierResult?.ToString() ?? "-"}";
        }

        // --- UI-only helper properties (do not affect DB) ---
        /// <summary>Human-friendly door label for display only.</summary>
        public string DoorText => Door switch
        {
            1 => "Cổng VÀO (1)",
            2 => "Cổng RA (2)",
            null => "-",
            _ => $"Cổng {Door}"
        };

        /// <summary>Human-friendly event description for display only.</summary>
        public string EventTypeText
        {
            get
            {
                if (!EventType.HasValue) return "-";
                return EventType.Value switch
                {
                    202 => "Nhấn nút / Button press",
                    16 => "Swipe / Card read",
                    7 => "Invalid PIN / Access denied",
                    8 => "Access granted",
                    200 => "Enroll / User added",
                    _ => $"Event {EventType.Value}"
                };
            }
        }

        /// <summary>Human-friendly barrier state for display only.</summary>
        public string BarrierText
        {
            get
            {
                if (!BarrierResult.HasValue) return "Unknown";
                return BarrierResult.Value switch
                {
                    1 => "Đã mở (Opened)",
                    0 => "Không mở (Failed)",
                    _ => BarrierResult.Value.ToString()
                };
            }
        }
    }
}
