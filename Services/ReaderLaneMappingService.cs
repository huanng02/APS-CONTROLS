using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QuanLyGiuXe.Services
{
    public class ReaderLaneMapping
    {
        public int ReaderNo { get; set; }    // 1, 2, 3, 4
        public int LaneIndex { get; set; }   // 1, 2...
        public string Direction { get; set; } // "IN", "OUT"
        public bool IsEnabled { get; set; } = true;
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
                    
                    // Migrate old data if necessary (if ReaderNo is missing/0)
                    if (_mappings.Any() && _mappings[0].ReaderNo == 0)
                    {
                        CreateDefaultMappings();
                    }
                }
                catch { CreateDefaultMappings(); }
            }
            else
            {
                CreateDefaultMappings();
            }
        }

        private void CreateDefaultMappings()
        {
            // Default mapping:
            // Reader 1 (Door 1) -> Lane 1 IN
            // Reader 2 (Door 1) -> Lane 1 OUT
            // Reader 3 (Door 2) -> Lane 2 IN
            // Reader 4 (Door 2) -> Lane 2 OUT
            _mappings = new List<ReaderLaneMapping>
            {
                new() { ReaderNo = 1, LaneIndex = 1, Direction = "IN", IsEnabled = true },
                new() { ReaderNo = 2, LaneIndex = 1, Direction = "OUT", IsEnabled = true },
                new() { ReaderNo = 3, LaneIndex = 2, Direction = "IN", IsEnabled = true },
                new() { ReaderNo = 4, LaneIndex = 2, Direction = "OUT", IsEnabled = true }
            };
            Save();
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

        public ReaderLaneMapping GetMappingByReader(int readerNo)
        {
            return _mappings.FirstOrDefault(m => m.ReaderNo == readerNo);
        }

        public void UpdateMappings(List<ReaderLaneMapping> newMappings)
        {
            _mappings = new List<ReaderLaneMapping>(newMappings);
            Save();
        }
    }
}
