using System;
using System.Collections.Concurrent;

namespace QuanLyGiuXe.Services
{
    public class LaneRuntimeState
    {
        public int LaneId { get; set; }
        
        // "IN", "OUT", "DISABLED", "MAINTENANCE"
        public string CurrentDirection { get; set; } = "IN"; 
        
        // True nếu đang có xe xử lý (chưa qua vòng từ)
        public bool IsLocked { get; set; }
        
        public DateTime LastSwitchTime { get; set; } = DateTime.Now;
        public bool HasVehicleInside { get; set; }
        
        // ID thẻ đang chờ xử lý để đối chiếu
        public string LockedByCardUid { get; set; } = string.Empty;
        
        // Thời gian lock, dùng cho Timeout Recovery
        public DateTime LockedAt { get; set; } 
    }

    public class LaneRuntimeManager
    {
        public static LaneRuntimeManager Instance { get; } = new();

        private readonly ConcurrentDictionary<int, LaneRuntimeState> _laneStates = new();

        private LaneRuntimeManager()
        {
            // Initialize default lanes
            _laneStates.TryAdd(1, new LaneRuntimeState { LaneId = 1, CurrentDirection = "IN" });
            _laneStates.TryAdd(2, new LaneRuntimeState { LaneId = 2, CurrentDirection = "OUT" });

            // Start background recovery task to prevent permanent locks (stuck state)
            System.Threading.Tasks.Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(1000);
                        CheckTimeoutAndUnlockLanes(10); // Auto-unlock if locked for more than 10 seconds
                    }
                    catch
                    {
                        // Prevent thread crash
                    }
                }
            });
        }

        public LaneRuntimeState GetLaneState(int laneId)
        {
            if (!_laneStates.TryGetValue(laneId, out var state))
            {
                state = new LaneRuntimeState { LaneId = laneId };
                _laneStates.TryAdd(laneId, state);
            }
            return state;
        }

        public event Action<int>? OnLaneDirectionChanged;

        public void SetLaneDirection(int laneId, string direction)
        {
            var state = GetLaneState(laneId);
            state.CurrentDirection = direction;
            state.LastSwitchTime = DateTime.Now;
            OnLaneDirectionChanged?.Invoke(laneId);
        }

        public bool LockLane(int laneId, string cardUid)
        {
            var state = GetLaneState(laneId);
            if (state.IsLocked) return false; // Already locked

            state.IsLocked = true;
            state.LockedByCardUid = cardUid;
            state.LockedAt = DateTime.Now;
            return true;
        }

        public void UnlockLane(int laneId)
        {
            var state = GetLaneState(laneId);
            state.IsLocked = false;
            state.LockedByCardUid = string.Empty;
        }

        public void CheckTimeoutAndUnlockLanes(int timeoutSeconds = 30)
        {
            foreach (var kvp in _laneStates)
            {
                var state = kvp.Value;
                if (state.IsLocked && (DateTime.Now - state.LockedAt).TotalSeconds > timeoutSeconds)
                {
                    state.IsLocked = false;
                    state.LockedByCardUid = string.Empty;
                    LoggingService.Instance.LogWarning("LaneRuntime", "Timeout", $"Lane {state.LaneId} auto-unlocked due to timeout.");
                }
            }
        }
    }
}
