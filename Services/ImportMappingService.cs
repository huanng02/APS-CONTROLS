using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class ImportMappingService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public Dictionary<string, int> LoadLoaiXeMap()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var list = _db.GetLoaiXe().Where(x => (x.TrangThai ?? string.Empty).Equals("Active", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var item in list)
            {
                if (item == null) continue;
                var key = ImportNormalization.Normalize(item.TenLoai ?? string.Empty);
                if (string.IsNullOrEmpty(key)) continue;
                if (!dict.ContainsKey(key)) dict[key] = item.Id;
            }
            return dict;
        }

        public Dictionary<string, int> LoadLoaiVeMap()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var list = _db.GetLoaiVe().Where(x => (x.TrangThai ?? string.Empty).Equals("Active", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var item in list)
            {
                if (item == null) continue;
                var key = ImportNormalization.Normalize(item.TenLoai ?? string.Empty);
                if (string.IsNullOrEmpty(key)) continue;
                if (!dict.ContainsKey(key)) dict[key] = item.Id;
            }
            return dict;
        }

        // Map with exact normalized match first; if not found, try fuzzy (Levenshtein distance <=2) then contains
        public int? MapLoaiXe(string rawText, Dictionary<string, int> map)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return null;

            // If user provided a numeric id in Excel, try parse and validate existence
            if (int.TryParse(rawText.Trim(), out var numericId))
            {
                if (map.Values.Contains(numericId)) return numericId;
            }

            var n = ImportNormalization.Normalize(rawText);
            if (map.TryGetValue(n, out var id)) return id;

            // fuzzy: try levenshtein <=2
            var best = map.Keys.Select(k => new { Key = k, Dist = LevenshteinDistance(k, n) }).OrderBy(x => x.Dist).FirstOrDefault();
            if (best != null && best.Dist <= 2) return map[best.Key];

            // contains match
            var contains = map.Keys.FirstOrDefault(k => k.Contains(n) || n.Contains(k));
            if (contains != null) return map[contains];

            return null;
        }

        public int? MapLoaiVe(string rawText, Dictionary<string, int> map)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return null;

            if (int.TryParse(rawText.Trim(), out var numericId))
            {
                if (map.Values.Contains(numericId)) return numericId;
            }

            var n = ImportNormalization.Normalize(rawText);
            if (map.TryGetValue(n, out var id)) return id;

            var best = map.Keys.Select(k => new { Key = k, Dist = LevenshteinDistance(k, n) }).OrderBy(x => x.Dist).FirstOrDefault();
            if (best != null && best.Dist <= 2) return map[best.Key];

            var contains = map.Keys.FirstOrDefault(k => k.Contains(n) || n.Contains(k));
            if (contains != null) return map[contains];

            return null;
        }

        private int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[a.Length, b.Length];
        }
    }
}
