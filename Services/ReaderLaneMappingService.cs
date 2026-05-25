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
        }

        public void UpdateMappings(List<ReaderLaneMapping> mappings)
        {
            _mappings = mappings ?? new List<ReaderLaneMapping>();

            Save();
        }

        public void RemoveReader(int readerNo)
        {
            var existing = _mappings
                .FirstOrDefault(x => x.ReaderNo == readerNo);

            if (existing != null)
            {
                _mappings.Remove(existing);
                Save();
            }
        }

        public void Clear()
        {
            _mappings.Clear();
            Save();
        }
    }
}