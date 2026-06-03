using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QuanLyGiuXe.Services
{
    public class ReaderLaneMapping
    {
        public int ReaderNo { get; set; } // 1,2,3,4

        // DB Lane Id thật
        public int LaneId { get; set; }

        // IN / OUT
        public string Direction { get; set; }

        public bool IsEnabled { get; set; } = true;
    }

    public class ReaderLaneMappingService
    {
        private static readonly string FilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reader_mappings.json");

        private List<ReaderLaneMapping> _mappings = new();

        public static ReaderLaneMappingService Instance { get; } = new();

        private ReaderLaneMappingService()
        {
            Load();
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    _mappings = new List<ReaderLaneMapping>();
                    Save();
                    return;
                }

                string json = File.ReadAllText(FilePath);

                _mappings =
                    JsonSerializer.Deserialize<List<ReaderLaneMapping>>(json)
                    ?? new List<ReaderLaneMapping>();

                // migrate old data
                bool needMigration =
                    _mappings.Any() &&
                    json.Contains("LaneIndex");

                if (needMigration)
                {
                    MigrateOldMappings();
                }
            }
            catch
            {
                _mappings = new List<ReaderLaneMapping>();
            }
        }

        private void MigrateOldMappings()
        {
            try
            {
                var migrated = new List<ReaderLaneMapping>();

                foreach (var old in _mappings)
                {
                    migrated.Add(new ReaderLaneMapping
                    {
                        ReaderNo = old.ReaderNo,

                        // tạm migrate LaneIndex -> LaneId
                        LaneId = old.LaneId,

                        Direction = old.Direction,
                        IsEnabled = old.IsEnabled
                    });
                }

                _mappings = migrated;

                Save();
            }
            catch
            {
                _mappings = new List<ReaderLaneMapping>();
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(
                    _mappings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(FilePath, json);
            }
            catch
            {
            }
        }

        public List<ReaderLaneMapping> GetAll()
        {
            return _mappings;
        }

        public ReaderLaneMapping GetMappingByReader(int readerNo)
        {
            return _mappings.FirstOrDefault(x => x.ReaderNo == readerNo);
        }

        public List<ReaderLaneMapping> GetMappingsByLane(int laneId)
        {
            return _mappings
                .Where(x => x.LaneId == laneId)
                .ToList();
        }

        public void RemoveMappingsByLane(int laneId)
        {
            _mappings.RemoveAll(x => x.LaneId == laneId);
            Save();
        }

        public ReaderLaneMapping GetReaderByLaneAndDirection(int laneId, string direction)
        {
            return _mappings.FirstOrDefault(x =>
                x.LaneId == laneId &&
                x.Direction == direction &&
                x.IsEnabled);
        }

        public void UpdateMapping(
            int readerNo,
            int laneId,
            string direction,
            bool enabled = true)
        {
            var existing = _mappings
                .FirstOrDefault(x => x.ReaderNo == readerNo);

            string GetLaneName(int lId)
            {
                try
                {
                    var l = ParkingTopologyService.Instance.GetLanes()?.FirstOrDefault(x => x.Id == lId);
                    return l != null ? l.LaneName : $"Làn ID {lId}";
                }
                catch { return $"Làn ID {lId}"; }
            }

            string oldValStr = existing != null 
                ? $"{GetLaneName(existing.LaneId)} ({existing.Direction}, {(existing.IsEnabled ? "Bật" : "Tắt")})" 
                : "Chưa gán";
            string newValStr = $"{GetLaneName(laneId)} ({direction}, {(enabled ? "Bật" : "Tắt")})";

            bool isChanged = existing == null || 
                             existing.LaneId != laneId || 
                             existing.Direction != direction || 
                             existing.IsEnabled != enabled;

            if (existing == null)
            {
                _mappings.Add(new ReaderLaneMapping
                {
                    ReaderNo = readerNo,
                    LaneId = laneId,
                    Direction = direction,
                    IsEnabled = enabled
                });
            }
            else
            {
                existing.LaneId = laneId;
                existing.Direction = direction;
                existing.IsEnabled = enabled;
            }

            Save();
            LoggingService.Instance.LogInfo("CONFIG_CHANGE", "ReaderMapping", $"Cập nhật đầu đọc: Reader {readerNo} -> Làn {laneId} ({direction}, enabled={enabled})");

            if (isChanged)
            {
                _ = Task.Run(async () => {
                    await ConfigurationAuditService.Instance.RecordChangeAsync(
                        "Reader Mapping",
                        $"Đầu đọc {readerNo}",
                        "Lane Mapping",
                        oldValStr,
                        newValStr
                    );
                });
            }
        }

        public void UpdateMappings(List<ReaderLaneMapping> mappings)
        {
            var oldMappings = new List<ReaderLaneMapping>(_mappings);
            _mappings = mappings ?? new List<ReaderLaneMapping>();

            Save();

            _ = Task.Run(async () =>
            {
                try
                {
                    string GetLaneName(int lId)
                    {
                        var l = ParkingTopologyService.Instance.GetLanes()?.FirstOrDefault(x => x.Id == lId);
                        return l != null ? l.LaneName : $"Làn ID {lId}";
                    }

                    for (int r = 1; r <= 4; r++)
                    {
                        var oldM = oldMappings.FirstOrDefault(x => x.ReaderNo == r);
                        var newM = _mappings.FirstOrDefault(x => x.ReaderNo == r);
                        if (newM != null)
                        {
                            bool isChanged = oldM == null ||
                                             oldM.LaneId != newM.LaneId ||
                                             oldM.Direction != newM.Direction ||
                                             oldM.IsEnabled != newM.IsEnabled;
                            if (isChanged)
                            {
                                string oldValStr = oldM != null 
                                    ? $"{GetLaneName(oldM.LaneId)} ({oldM.Direction}, {(oldM.IsEnabled ? "Bật" : "Tắt")})" 
                                    : "Chưa gán";
                                string newValStr = $"{GetLaneName(newM.LaneId)} ({newM.Direction}, {(newM.IsEnabled ? "Bật" : "Tắt")})";

                                await ConfigurationAuditService.Instance.RecordChangeAsync(
                                    "Reader Mapping",
                                    $"Đầu đọc {r}",
                                    "Lane Mapping",
                                    oldValStr,
                                    newValStr
                                );
                            }
                        }
                    }
                }
                catch { }
            });
        }

        public void RemoveReader(int readerNo)
        {
            var existing = _mappings
                .FirstOrDefault(x => x.ReaderNo == readerNo);

            if (existing != null)
            {
                string oldValStr = $"{existing.LaneId} ({existing.Direction})";
                _mappings.Remove(existing);
                Save();
                LoggingService.Instance.LogInfo("CONFIG_CHANGE", "ReaderMapping", $"Xóa đầu đọc: Reader {readerNo}");

                _ = Task.Run(async () => {
                    await ConfigurationAuditService.Instance.RecordChangeAsync(
                        "Reader Mapping",
                        $"Đầu đọc {readerNo}",
                        "Lane Mapping",
                        oldValStr,
                        "Chưa gán"
                    );
                });
            }
        }

        public void Clear()
        {
            _mappings.Clear();
            Save();
            LoggingService.Instance.LogInfo("CONFIG_CHANGE", "ReaderMapping", "Xóa toàn bộ cấu hình ánh xạ đầu đọc");

            _ = Task.Run(async () => {
                await ConfigurationAuditService.Instance.RecordChangeAsync(
                    "Reader Mapping",
                    "Tất cả đầu đọc",
                    "Clear Mappings",
                    "Có cấu hình",
                    "Chưa gán"
                );
            });
        }
    }
}