using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QuanLyGiuXe.Services
{
    public class ReaderLaneMapping
    {
        public int ReaderIndex { get; set; } // 1-2 (Group A or B)
        public int Door { get; set; }        // 1 or 2
        public int LaneIndex { get; set; }   // 1 (Left/In), 2 (Right/Out)
    }

    public class ReaderLaneMappingService
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reader_mappings.json");
        private List<ReaderLaneMapping> _mappings;

        public static ReaderLaneMappingService Instance { get; } = new();

        private ReaderLaneMappingService()
        {
            Load();
        }

        public void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    _mappings = JsonSerializer.Deserialize<List<ReaderLaneMapping>>(json) ?? new List<ReaderLaneMapping>();
                }
                catch { _mappings = new List<ReaderLaneMapping>(); }
            }
            else
            {
                // Default mapping: Group A (Door 1) -> Lane 1, Group B (Door 2) -> Lane 2
                _mappings = new List<ReaderLaneMapping>
                {
                    new() { ReaderIndex = 1, Door = 1, LaneIndex = 1 },
                    new() { ReaderIndex = 2, Door = 1, LaneIndex = 1 },
                    new() { ReaderIndex = 1, Door = 2, LaneIndex = 2 },
                    new() { ReaderIndex = 2, Door = 2, LaneIndex = 2 }
                };
                Save();
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_mappings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public List<ReaderLaneMapping> GetAll() => _mappings;

        public int MapReaderToLane(int readerIndex, int door)
        {
            var mapping = _mappings.FirstOrDefault(m => m.ReaderIndex == readerIndex && m.Door == door);
            return mapping?.LaneIndex ?? 0;
        }

        public void UpdateMapping(int door, List<int> readers, int laneIndex)
        {
            // Remove existing mappings for these readers
            _mappings.RemoveAll(m => m.Door == door && readers.Contains(m.ReaderIndex));
            
            // Remove other readers from this lane if it's a exclusive assignment (optional)
            // _mappings.RemoveAll(m => m.LaneIndex == laneIndex);

            foreach (var r in readers)
            {
                _mappings.Add(new ReaderLaneMapping { Door = door, ReaderIndex = r, LaneIndex = laneIndex });
            }
            Save();
        }
    }
}
