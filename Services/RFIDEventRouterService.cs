using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public enum RFIDContextType
    {
        VehicleAccess,     // Xe vào/ra bãi xe (Mặc định)
        CardEnrollment,    // Đăng ký/thêm thẻ mới
        AdminOperation     // Test thiết bị, override, debug
    }

    public class RFIDEvent
    {
        public string CardUID { get; set; } = string.Empty;
        public int ReaderNo { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string TerminalId { get; set; } = string.Empty;
        public int DoorNumber { get; set; }
    }

    public interface IRFIDEventHandler
    {
        RFIDContextType SupportedContext { get; }
        Task HandleAsync(RFIDEvent rfidEvent);
    }

    public class TerminalContext
    {
        public string TerminalId { get; set; } = string.Empty;
        public RFIDContextType CurrentContext { get; set; } = RFIDContextType.VehicleAccess;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public Action<string>? CardScannedCallback { get; set; }
    }

    public sealed class RFIDEventRouterService
    {
        private static readonly Lazy<RFIDEventRouterService> _instance =
            new Lazy<RFIDEventRouterService>(() => new RFIDEventRouterService());

        public static RFIDEventRouterService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, TerminalContext> _terminalSessions = new();
        private readonly Dictionary<RFIDContextType, IRFIDEventHandler> _handlers = new();
        private readonly ConcurrentDictionary<string, DateTime> _cooldownCache = new();
        private readonly object _lock = new object();
        private string _localTerminalId = string.Empty;

        private RFIDEventRouterService()
        {
            // Định danh máy trạm hiện tại
            _localTerminalId = Environment.MachineName;
            
            // Đặt Session mặc định cho máy trạm hiện tại
            SetTerminalContext(_localTerminalId, RFIDContextType.VehicleAccess);
        }

        public void RegisterHandler(IRFIDEventHandler handler)
        {
            lock (_lock)
            {
                _handlers[handler.SupportedContext] = handler;
            }
        }

        public void SetTerminalContext(string terminalId, RFIDContextType context, Action<string>? onCardScannedCallback = null)
        {
            var ctx = _terminalSessions.GetOrAdd(terminalId, id => new TerminalContext { TerminalId = id });
            ctx.CurrentContext = context;
            ctx.CardScannedCallback = onCardScannedCallback;
            ctx.LastUpdated = DateTime.Now;
            
            System.Diagnostics.Debug.WriteLine($"🌐 [RFID Router] Terminal '{terminalId}' switched context to '{context}'");
            try
            {
                LoggingService.Instance.LogInfo("RFID_CONTEXT_CHANGE", "RFIDEventRouter", $"Terminal {terminalId} context set to {context}");
            }
            catch { }
        }

        public void ResetTerminalToDefault(string terminalId)
        {
            SetTerminalContext(terminalId, RFIDContextType.VehicleAccess);
        }

        public async Task RouteEventAsync(string rawUid, int readerNo, int doorNumber = 1)
        {
            string cleanUid = ChuanHoaUID(rawUid);
            if (string.IsNullOrEmpty(cleanUid)) return;

            // 1. Cooldown chống quét trùng lặp
            string cooldownKey = $"{cleanUid}_{readerNo}";
            DateTime now = DateTime.Now;
            if (_cooldownCache.TryGetValue(cooldownKey, out var lastScanTime))
            {
                if ((now - lastScanTime).TotalMilliseconds < 1500) // Cooldown 1.5s
                {
                    System.Diagnostics.Debug.WriteLine($"⏳ [RFID Router] Cooldown ignore for Card {cleanUid} at Reader {readerNo}");
                    return;
                }
            }
            _cooldownCache[cooldownKey] = now;

            var rfidEvent = new RFIDEvent
            {
                CardUID = cleanUid,
                ReaderNo = readerNo,
                Timestamp = now,
                TerminalId = _localTerminalId,
                DoorNumber = doorNumber
            };

            // 2. Xác định Ngữ cảnh hoạt động
            var session = _terminalSessions.GetOrAdd(_localTerminalId, id =>
                new TerminalContext { TerminalId = id, CurrentContext = RFIDContextType.VehicleAccess });

            System.Diagnostics.Debug.WriteLine($"🔀 [RFID Router] Routing Card {cleanUid} (Reader {readerNo}) with Context: {session.CurrentContext}");

            // 3. Phân phối luồng xử lý
            IRFIDEventHandler? handler = null;
            lock (_lock)
            {
                _handlers.TryGetValue(session.CurrentContext, out handler);
            }

            if (handler != null)
            {
                try
                {
                    // Chạy xử lý nghiệp vụ
                    await handler.HandleAsync(rfidEvent);
                    
                    // Thực thi callback tùy chỉnh nếu có
                    if (session.CardScannedCallback != null)
                    {
                        try
                        {
                            session.CardScannedCallback.Invoke(cleanUid);
                        }
                        catch (Exception cbEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ [RFID Router] Callback exception: {cbEx.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ [RFID Router] Handler error: {ex.Message}");
                    try
                    {
                        LoggingService.Instance.LogError("RFID_ROUTING_ERROR", "RFIDEventRouter", $"Handler failed for {cleanUid}", ex);
                    }
                    catch { }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [RFID Router] No handler for context {session.CurrentContext}");
            }
        }

        public static string ChuanHoaUID(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return string.Empty;
            return new string(uid
                   .Where(c => char.IsLetterOrDigit(c))
                   .ToArray())
                   .ToUpper();
        }
    }

    // ── VEHICLE ACCESS HANDLER ──────────────────────────────────────────────────
    public class VehicleAccessHandler : IRFIDEventHandler
    {
        public RFIDContextType SupportedContext => RFIDContextType.VehicleAccess;
        public event Func<int, string, Task>? OnVehicleAccessTriggered;

        public async Task HandleAsync(RFIDEvent rfidEvent)
        {
            System.Diagnostics.Debug.WriteLine($"🚗 [VehicleAccessHandler] Triggering parking flow for card: {rfidEvent.CardUID}");
            
            try
            {
                var topo = EventBus.Instance.ResolveTopologyForReader(rfidEvent.ReaderNo);
                bool isEntry = rfidEvent.ReaderNo % 2 == 1; // Odd = Entry, Even = Exit
                
                EventBus.Instance.Publish(new RealtimeEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = isEntry ? "VEHICLE_ENTRY" : "VEHICLE_EXIT",
                    Severity = RealtimeEventSeverity.Success,
                    Source = "VehicleAccessHandler",
                    Message = $"{(isEntry ? "Xe vào bãi" : "Xe ra khỏi bãi")}. Thẻ UID: {rfidEvent.CardUID} quẹt tại làn kiểm soát.",
                    Site = topo.Site,
                    Zone = topo.Zone,
                    Gate = topo.Gate,
                    Lane = topo.Lane
                });
            }
            catch { }

            if (OnVehicleAccessTriggered != null)
            {
                // Gọi callback bất đồng bộ để thực hiện quy trình nghiệp vụ bãi xe hiện tại
                await OnVehicleAccessTriggered.Invoke(rfidEvent.ReaderNo, rfidEvent.CardUID);
            }
        }
    }

    // ── CARD ENROLLMENT HANDLER ─────────────────────────────────────────────────
    public class CardEnrollmentHandler : IRFIDEventHandler
    {
        public RFIDContextType SupportedContext => RFIDContextType.CardEnrollment;
        public static event Action<string>? OnCardEnrolled;

        public Task HandleAsync(RFIDEvent rfidEvent)
        {
            System.Diagnostics.Debug.WriteLine($"💳 [CardEnrollmentHandler] Enrolling card: {rfidEvent.CardUID}");
            
            try
            {
                EventBus.Instance.Publish(new RealtimeEvent
                {
                    Timestamp = DateTime.Now,
                    EventType = "CARD_REGISTERED",
                    Severity = RealtimeEventSeverity.Success,
                    Source = "CardEnrollmentHandler",
                    Message = $"Tiến trình đăng ký thẻ mới được kích hoạt. Đọc thẻ UID: {rfidEvent.CardUID}",
                    Site = "HQ Office",
                    Zone = "Khu đăng ký",
                    Gate = "Bàn làm việc",
                    Lane = "Đăng ký thẻ"
                });
            }
            catch { }

            OnCardEnrolled?.Invoke(rfidEvent.CardUID);
            return Task.CompletedTask;
        }
    }

    // ── ADMIN OVERRIDE HANDLER ──────────────────────────────────────────────────
    public class AdminOverrideHandler : IRFIDEventHandler
    {
        public RFIDContextType SupportedContext => RFIDContextType.AdminOperation;

        public Task HandleAsync(RFIDEvent rfidEvent)
        {
            System.Diagnostics.Debug.WriteLine($"🛠️ [AdminOverrideHandler] Diagnostic scan of card: {rfidEvent.CardUID}");
            
            try
            {
                LoggingService.Instance.LogAudit(
                    "ADMIN_OVERRIDE_SCAN",
                    "RFIDDevice",
                    rfidEvent.CardUID,
                    null, null,
                    "AdminOverrideHandler",
                    null, null,
                    $"Card {rfidEvent.CardUID} scanned in admin operation mode on Reader {rfidEvent.ReaderNo}"
                );
            }
            catch { }
            
            return Task.CompletedTask;
        }
    }
}
