using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.ViewModels
{
    public enum PriceUnit
    {
        PerHour,
        PerMinute,
        PerSecond,
        PerSession
    }

    public class KhungGioDef
    {
        public int Id { get; set; }
        public string TenKhungGio { get; set; } = string.Empty;
        public TimeSpan GioBatDau { get; set; }
        public TimeSpan GioKetThuc { get; set; }
        public bool QuaDem { get; set; }
    }

    public class KhungPrice
    {
        public int KhungGioId { get; set; }
        public decimal GiaTien { get; set; }
        public PriceUnit Unit { get; set; } = PriceUnit.PerHour;
    }

    public class Segment
    {
        public int KhungGioId { get; set; }
        public string TenKhungGio { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public long DurationSeconds => (long)(End - Start).TotalSeconds;
    }

    public class CalculationResult
    {
        public TimeSpan TotalDuration { get; set; }
        public bool IsCaseA { get; set; }
        public int? DominantKhungId { get; set; }
        public string? DominantKhungName { get; set; }
        public List<Segment> Segments { get; set; } = new List<Segment>();
        public Dictionary<int, long> DurationByKhungSeconds { get; set; } = new Dictionary<int, long>();
        public decimal FinalPrice { get; set; }
    }

    public static class TimeSlotCalculator
    {
        private static DateTime NormalizeCheckOut(DateTime checkIn, DateTime checkOut)
        {
            if (checkOut <= checkIn)
                return checkOut.AddDays(1);

            return checkOut;
        }

        public static CalculationResult Calculate(
            DateTime checkIn,
            DateTime checkOut,
            IEnumerable<KhungGioDef> khungDefs,
            IEnumerable<KhungPrice> prices,
            PriceUnit defaultUnit = PriceUnit.PerHour)
        {
            if (khungDefs == null) throw new ArgumentNullException(nameof(khungDefs));
            if (prices == null) throw new ArgumentNullException(nameof(prices));

            checkOut = NormalizeCheckOut(checkIn, checkOut);

            var result = new CalculationResult();

            var totalSeconds = (long)(checkOut - checkIn).TotalSeconds;
            result.TotalDuration = TimeSpan.FromSeconds(totalSeconds);

            if (totalSeconds <= 0)
            {
                result.FinalPrice = 0m;
                return result;
            }

            var khungs = khungDefs.ToList();
            var priceMap = prices.ToDictionary(p => p.KhungGioId, p => p);

            var startDate = checkIn.Date.AddDays(-1);
            var endDate = checkOut.Date.AddDays(1);

            var intervals = new List<(int id, string name, DateTime start, DateTime end)>();

            for (var dt = startDate; dt <= endDate; dt = dt.AddDays(1))
            {
                foreach (var k in khungs)
                {
                    var s = dt + k.GioBatDau;
                    var e = dt + k.GioKetThuc;

                    if (k.QuaDem || k.GioKetThuc < k.GioBatDau)
                        e = e.AddDays(1);

                    if (e <= s) continue;

                    intervals.Add((k.Id, k.TenKhungGio, s, e));
                }
            }

            var segments = new List<Segment>();

            foreach (var iv in intervals)
            {
                var segStart = iv.start < checkIn ? checkIn : iv.start;
                var segEnd = iv.end > checkOut ? checkOut : iv.end;

                if (segEnd <= segStart) continue;

                segments.Add(new Segment
                {
                    KhungGioId = iv.id,
                    TenKhungGio = iv.name,
                    Start = segStart,
                    End = segEnd
                });
            }

            var durations = new Dictionary<int, long>();

            foreach (var s in segments)
            {
                if (!durations.ContainsKey(s.KhungGioId))
                    durations[s.KhungGioId] = 0;

                durations[s.KhungGioId] += s.DurationSeconds;
            }

            result.DurationByKhungSeconds = durations;

            // =========================
            // CASE A: < 1 giờ
            // =========================
            if (totalSeconds < 3600)
            {
                result.IsCaseA = true;

                var maxSec = durations.Any() ? durations.Values.Max() : 0;

                var candidates = durations
                    .Where(x => x.Value == maxSec)
                    .Select(x => x.Key)
                    .ToList();

                int chosen = -1;

                if (candidates.Count == 1)
                {
                    chosen = candidates[0];
                }
                else
                {
                    DateTime latest = DateTime.MinValue;

                    foreach (var id in candidates)
                    {
                        var lastEnd = segments
                            .Where(x => x.KhungGioId == id)
                            .Max(x => x.End);

                        if (lastEnd >= latest)
                        {
                            latest = lastEnd;
                            chosen = id;
                        }
                    }
                }

                result.DominantKhungId = chosen;
                result.DominantKhungName =
                    khungs.FirstOrDefault(x => x.Id == chosen)?.TenKhungGio ?? "";

                if (chosen != -1 && priceMap.TryGetValue(chosen, out var p))
                    result.FinalPrice = Math.Round(p.GiaTien, 2);

                result.Segments = segments.OrderBy(x => x.Start).ToList();
                return result;
            }

            // =========================
            // CASE B: >= 1 giờ
            // =========================
            result.IsCaseA = false;

            var uniqueKhungs = segments
                .Select(x => x.KhungGioId)
                .Distinct()
                .ToList();

            decimal totalPrice = 0m;

            foreach (var id in uniqueKhungs)
            {
                if (priceMap.TryGetValue(id, out var p))
                    totalPrice += p.GiaTien;
            }

            result.FinalPrice = Math.Round(totalPrice, 2);
            result.Segments = segments.OrderBy(x => x.Start).ToList();

            return result;
        }

        public static (IEnumerable<KhungGioDef>, IEnumerable<KhungPrice>) MapFromDb(
            IEnumerable<KhungGio> khungs,
            IEnumerable<BangGiaKhungGio> prices,
            PriceUnit defaultUnit = PriceUnit.PerHour)
        {
            var defs = khungs.Select(k => new KhungGioDef
            {
                Id = k.Id,
                TenKhungGio = k.TenKhungGio,
                GioBatDau = k.GioBatDau,
                GioKetThuc = k.GioKetThuc,
                QuaDem = k.QuaDem
            });

            var ps = prices.Select(p => new KhungPrice
            {
                KhungGioId = p.KhungGioId,
                GiaTien = p.GiaTien,
                Unit = defaultUnit
            });

            return (defs, ps);
        }
    }
}