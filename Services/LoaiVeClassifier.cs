using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public enum LoaiVeKind { Unknown = 0, Monthly = 1, Daily = 2, Other = 3 }

    public interface ILoaiVeClassifier
    {
        LoaiVeKind Classify(LoaiVe loaiVe);
    }

    // Default classifier: uses TenLoai heuristics but centralized so it can be replaced later.
    public class DefaultLoaiVeClassifier : ILoaiVeClassifier
    {
        private readonly HashSet<string> _monthlyKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "tháng", "thang", "month", "monthly"
        };

        private readonly HashSet<string> _dailyKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "lượt", "vat lai", "vãng lai", "day", "daily"
        };

        public LoaiVeKind Classify(LoaiVe loaiVe)
        {
            if (loaiVe == null) return LoaiVeKind.Unknown;
            var name = (loaiVe.TenLoai ?? string.Empty).ToLowerInvariant();
            // check monthly keywords
            if (_monthlyKeywords.Any(k => name.Contains(k))) return LoaiVeKind.Monthly;
            if (_dailyKeywords.Any(k => name.Contains(k))) return LoaiVeKind.Daily;
            return LoaiVeKind.Other;
        }
    }
}
