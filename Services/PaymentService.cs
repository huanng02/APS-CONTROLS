using System;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class PaymentService
    {
        private readonly DatabaseService _db = new DatabaseService();
        private readonly BangGiaRepository _bgRepo = new BangGiaRepository();

        /// <summary>
        /// Calculate parking fee according to business rules using BangGia as source of pricing.
        /// </summary>
        public decimal CalculateFee(int? loaiVeId, int? loaiXeId, DateTime timeIn, DateTime timeOut)
        {
            if (timeOut < timeIn) timeOut = timeIn;

            // Determine LoaiVe name
            string tenLoaiVe = string.Empty;
            if (loaiVeId.HasValue && loaiVeId.Value > 0)
            {
                var lv = _db.GetLoaiVe().FirstOrDefault(x => x.Id == loaiVeId.Value);
                tenLoaiVe = lv?.TenLoai ?? string.Empty;
            }

            if (!loaiXeId.HasValue || loaiXeId.Value <= 0 || !loaiVeId.HasValue)
                return 0m;

            var bg = _bgRepo.GetByLoaiXeAndLoaiVe(loaiXeId.Value, loaiVeId.Value);
            if (bg == null)
                return 0m;

            var name = (tenLoaiVe ?? string.Empty).ToLowerInvariant();
            var span = timeOut - timeIn;
            var hours = (int)Math.Ceiling(span.TotalHours <= 0 ? 1 : span.TotalHours);
            bool isOvernight = timeIn.Date != timeOut.Date;

            if (name.Contains("thang") || name.Contains("tháng"))
            {
                return bg.GiaThang ?? 0m;
            }

            if (name.Contains("vang") || name.Contains("vanglai") || name.Contains("vang lai"))
            {
                if (isOvernight && bg.GiaQuaDem.HasValue)
                    return bg.GiaQuaDem.Value;
                return (bg.GiaTheoGio ?? 0m) * hours;
            }

            // fallback
            if (isOvernight && bg.GiaQuaDem.HasValue) return bg.GiaQuaDem.Value;
            return (bg.GiaTheoGio ?? 0m) * hours;
        }
    }
}
