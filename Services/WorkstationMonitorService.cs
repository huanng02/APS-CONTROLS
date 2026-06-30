using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class WorkstationMonitorService
    {
        private static readonly Lazy<WorkstationMonitorService> _instance =
            new(() => new WorkstationMonitorService());

        public static WorkstationMonitorService Instance => _instance.Value;

        private readonly DatabaseService _db = new();
        private System.Threading.Timer? _monitorTimer;
        private readonly object _lock = new();

        public string CurrentWorkstationId { get; private set; } = string.Empty;
        public string Hostname { get; private set; } = string.Empty;
        public string IpAddress { get; private set; } = string.Empty;

        // Tracks the lanes currently active (assigned to us)
        private List<int> _activeLaneIds = new();
        public List<int> GetActiveLaneIds()
        {
            lock (_lock)
            {
                return _activeLaneIds.ToList();
            }
        }

        // Event raised when the set of lanes managed by this workstation changes
        public event Action? OnActiveLanesChanged;

        private WorkstationMonitorService()
        {
            InitializeIdentity();
        }

        private void InitializeIdentity()
        {
            var cfg = AppConfig.Load();
            CurrentWorkstationId = cfg.WorkstationId;
            Hostname = Environment.MachineName;

            var localIps = C3200Service.GetLocalIpAddresses();
            IpAddress = localIps.FirstOrDefault() ?? "127.0.0.1";
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_monitorTimer != null) return;
                _monitorTimer = new System.Threading.Timer(async _ => await TickAsync(), null, 0, 3000);
                LoggingService.Instance.LogInfo("FAILOVER", "MonitorService", $"WorkstationMonitorService started. WorkstationId={CurrentWorkstationId}");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _monitorTimer?.Dispose();
                _monitorTimer = null;
            }
        }

        private async Task TickAsync()
        {
            try
            {
                // 1. Send Heartbeat
                await SendHeartbeatAsync();

                // 2. Mark Offline Workstations
                await DetectOfflineWorkstationsAsync();

                // 3. Auto-populate LaneOwnership table
                await AutoResolveLaneOwnershipAsync();

                // 4. Check for WAITING_RECLAIM lanes we currently control and release if idle
                await HandleWaitingReclaimsAsync();

                // 5. Check and initiate WAITING_RECLAIM for our primary lanes that are taken over
                await InitiateReclaimsForPrimaryLanesAsync();

                // 6. Take over any offline active workstation's lanes
                await PerformTakeoversAsync();

                // 7. Refresh active lanes list
                await RefreshActiveLanesAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("FAILOVER_TICK_ERROR", "MonitorService", "Error during failover monitor tick", ex);
            }
        }

        private async Task SendHeartbeatAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                
                // Register/update heartbeat
                string sql = @"
                    IF EXISTS (SELECT 1 FROM Workstations WHERE WorkstationId = @id)
                        UPDATE Workstations 
                        SET Hostname = @host, IpAddress = @ip, LastHeartbeatUtc = GETUTCDATE(), Status = 'Online'
                        WHERE WorkstationId = @id
                    ELSE
                        INSERT INTO Workstations (WorkstationId, Hostname, IpAddress, LastHeartbeatUtc, Status)
                        VALUES (@id, @host, @ip, GETUTCDATE(), 'Online')";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", CurrentWorkstationId);
                    cmd.Parameters.AddWithValue("@host", Hostname);
                    cmd.Parameters.AddWithValue("@ip", IpAddress);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task DetectOfflineWorkstationsAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                
                // Mark workstations as offline if no heartbeat in 15 seconds
                string sql = @"
                    UPDATE Workstations
                    SET Status = 'Offline'
                    WHERE Status = 'Online' 
                      AND DATEDIFF(second, LastHeartbeatUtc, GETUTCDATE()) > 15
                      AND WorkstationId != @myId"; // Safe-guard: never mark ourselves offline

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@myId", CurrentWorkstationId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task AutoResolveLaneOwnershipAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Query lanes with barriers whose controller PcIp maps to a registered workstation
                string sql = @"
                    SELECT DISTINCT b.LaneId, w.WorkstationId
                    FROM dbo.Barriers b
                    JOIN dbo.C3Controllers c ON b.ControllerId = c.Id
                    JOIN dbo.Workstations w ON c.PcIp = w.IpAddress
                    WHERE b.IsActive = 1 AND c.IsActive = 1";

                var mappings = new List<(int LaneId, string WorkstationId)>();
                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        mappings.Add((reader.GetInt32(0), reader.GetString(1)));
                    }
                }

                // Insert missing entries into LaneOwnership
                foreach (var mapping in mappings)
                {
                    string insertSql = @"
                        IF NOT EXISTS (SELECT 1 FROM LaneOwnership WHERE LaneId = @laneId)
                        BEGIN
                            INSERT INTO LaneOwnership (LaneId, PrimaryWorkstationId, ActiveWorkstationId, OwnershipStatus, LastTakeoverUtc, LastChangedUtc)
                            VALUES (@laneId, @workstationId, @workstationId, 'NORMAL', NULL, GETUTCDATE())
                        END";

                    using (var cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@laneId", mapping.LaneId);
                        cmd.Parameters.AddWithValue("@workstationId", mapping.WorkstationId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private async Task PerformTakeoversAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Find lanes whose active workstation is offline in the DB
                string sql = @"
                    SELECT lo.LaneId, lo.ActiveWorkstationId
                    FROM LaneOwnership lo
                    JOIN Workstations w ON lo.ActiveWorkstationId = w.WorkstationId
                    WHERE w.Status = 'Offline' AND lo.ActiveWorkstationId != @myId";

                var targetLanes = new List<(int LaneId, string OfflineId)>();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@myId", CurrentWorkstationId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            targetLanes.Add((reader.GetInt32(0), reader.GetString(1)));
                        }
                    }
                }

                // Atomically claim ownership
                foreach (var lane in targetLanes)
                {
                    string claimSql = @"
                        UPDATE LaneOwnership
                        SET ActiveWorkstationId = @myId,
                            OwnershipStatus = 'TAKEN_OVER',
                            LastTakeoverUtc = GETUTCDATE(),
                            LastChangedUtc = GETUTCDATE()
                        WHERE LaneId = @laneId AND ActiveWorkstationId = @offlineId";

                    int affected = 0;
                    using (var cmd = new SqlCommand(claimSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@myId", CurrentWorkstationId);
                        cmd.Parameters.AddWithValue("@laneId", lane.LaneId);
                        cmd.Parameters.AddWithValue("@offlineId", lane.OfflineId);
                        affected = await cmd.ExecuteNonQueryAsync();
                    }

                    if (affected > 0)
                    {
                        // Successfully claimed! Log the takeover event
                        LoggingService.Instance.LogAudit(
                            "LANE_TAKEOVER",
                            "LaneOwnership",
                            lane.LaneId.ToString(),
                            null,
                            new { LaneId = lane.LaneId, OldWorkstation = lane.OfflineId, NewWorkstation = CurrentWorkstationId },
                            "WorkstationMonitorService",
                            details: $"Workstation {CurrentWorkstationId} took over Lane {lane.LaneId} because Workstation {lane.OfflineId} is offline"
                        );

                        // Save dynamic device event
                        string eventSql = @"
                            INSERT INTO DeviceEvents (DeviceType, DeviceName, EventType, Severity, Description)
                            VALUES ('Lane', @laneName, 'TAKEOVER', 'Warning', @desc)";

                        using (var cmd = new SqlCommand(eventSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@laneName", $"Làn ID {lane.LaneId}");
                            cmd.Parameters.AddWithValue("@desc", $"Workstation {Hostname} ({IpAddress}) nhận quyền điều khiển làn từ máy trạm bị lỗi.");
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        private async Task InitiateReclaimsForPrimaryLanesAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // If we are online, check if any of our primary lanes are controlled by someone else, and status is TAKEN_OVER
                // Update status to WAITING_RECLAIM
                string sql = @"
                    UPDATE LaneOwnership
                    SET OwnershipStatus = 'WAITING_RECLAIM',
                        LastChangedUtc = GETUTCDATE()
                    WHERE PrimaryWorkstationId = @myId
                      AND ActiveWorkstationId != @myId
                      AND OwnershipStatus = 'TAKEN_OVER'";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@myId", CurrentWorkstationId);
                    int affected = await cmd.ExecuteNonQueryAsync();
                    if (affected > 0)
                    {
                        LoggingService.Instance.LogInfo("FAILOVER", "Reclaim", $"{affected} primary lanes marked as WAITING_RECLAIM.");
                    }
                }
            }
        }

        private async Task HandleWaitingReclaimsAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Find lanes we currently control (ActiveWorkstationId = @myId) that are in WAITING_RECLAIM status
                string sql = @"
                    SELECT LaneId, PrimaryWorkstationId
                    FROM LaneOwnership
                    WHERE ActiveWorkstationId = @myId
                      AND OwnershipStatus = 'WAITING_RECLAIM'";

                var reclaimCandidates = new List<(int LaneId, string PrimaryId)>();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@myId", CurrentWorkstationId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            reclaimCandidates.Add((reader.GetInt32(0), reader.GetString(1)));
                        }
                    }
                }

                // For each candidate, check if the lane is currently idle (no active swipe or locks)
                foreach (var candidate in reclaimCandidates)
                {
                    var laneState = LaneRuntimeManager.Instance.GetLaneState(candidate.LaneId);
                    
                    // Idle conditions: 
                    // 1. Not locked by LaneRuntimeManager.
                    // 2. No transaction currently active (check if there is a pending vehicle state)
                    bool isIdle = laneState == null || (!laneState.IsLocked && !laneState.HasVehicleInside);

                    if (isIdle)
                    {
                        // Release control back to primary workstation
                        string releaseSql = @"
                            UPDATE LaneOwnership
                            SET ActiveWorkstationId = PrimaryWorkstationId,
                                OwnershipStatus = 'NORMAL',
                                LastChangedUtc = GETUTCDATE()
                            WHERE LaneId = @laneId
                              AND ActiveWorkstationId = @myId
                              AND OwnershipStatus = 'WAITING_RECLAIM'";

                        int affected = 0;
                        using (var cmd = new SqlCommand(releaseSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@laneId", candidate.LaneId);
                            cmd.Parameters.AddWithValue("@myId", CurrentWorkstationId);
                            affected = await cmd.ExecuteNonQueryAsync();
                        }

                        if (affected > 0)
                        {
                            LoggingService.Instance.LogAudit(
                                "LANE_RECLAIMED",
                                "LaneOwnership",
                                candidate.LaneId.ToString(),
                                null,
                                new { LaneId = candidate.LaneId, OldWorkstation = CurrentWorkstationId, NewWorkstation = candidate.PrimaryId },
                                "WorkstationMonitorService",
                                details: $"Workstation {candidate.PrimaryId} reclaimed ownership of Lane {candidate.LaneId}. Failover ended."
                            );

                            // Save device event
                            string eventSql = @"
                                INSERT INTO DeviceEvents (DeviceType, DeviceName, EventType, Severity, Description)
                                VALUES ('Lane', @laneName, 'RECLAIM', 'Info', @desc)";

                            using (var cmd = new SqlCommand(eventSql, conn))
                            {
                                cmd.Parameters.AddWithValue("@laneName", $"Làn ID {candidate.LaneId}");
                                cmd.Parameters.AddWithValue("@desc", $"Hoàn tất chuyển giao quyền kiểm soát. Trả quyền quản lý làn về máy trạm gốc.");
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
        }

        private async Task RefreshActiveLanesAsync()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Get all lane IDs where we are the ActiveWorkstationId
                string sql = @"
                    SELECT LaneId
                    FROM LaneOwnership
                    WHERE ActiveWorkstationId = @myId";

                var newLanes = new List<int>();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@myId", CurrentWorkstationId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            newLanes.Add(reader.GetInt32(0));
                        }
                    }
                }

                // Check if the set of active lanes changed
                bool changed = false;
                lock (_lock)
                {
                    var currentSet = new HashSet<int>(_activeLaneIds);
                    var newSet = new HashSet<int>(newLanes);

                    if (!currentSet.SetEquals(newSet))
                    {
                        _activeLaneIds = newLanes;
                        changed = true;
                    }
                }

                if (changed)
                {
                    LoggingService.Instance.LogInfo("FAILOVER", "ActiveLanes", $"Active lanes updated: [{string.Join(", ", newLanes)}]");
                    
                    // Dispatch event asynchronously to avoid blocking tick loop
                    _ = Task.Run(() => OnActiveLanesChanged?.Invoke());
                }
            }
        }
    }
}
