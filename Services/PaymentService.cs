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

            // basic validation
            if (!loaiXeId.HasValue || loaiXeId.Value <= 0 || !loaiVeId.HasValue || loaiVeId.Value <= 0)
                return 0m;

            var bg = _bgRepo.GetByLoaiXeAndLoaiVe(loaiXeId.Value, loaiVeId.Value);
            if (bg == null)
                return 0m;

            // Monthly ticket
            if (bg.GiaThang.HasValue && bg.GiaThang.Value > 0)
                return bg.GiaThang.Value;

            // Pricing zones
            // DAY: 06:00 -> 19:59 (we treat end as 20:00 exclusive)
            // NIGHT: 20:00 -> 05:59 (spans midnight)

            var total = timeOut - timeIn;

            // If duration > 30 minutes -> AUTO NIGHT price
            if (total > TimeSpan.FromMinutes(30))
            {
                return bg.GiaQuaDem ?? 0m;
            }

            // duration <= 30 minutes -> split into day/night portions across covered dates
            long dayTicks = 0;
            var firstDate = timeIn.Date;
            var lastDate = timeOut.Date;
            for (var d = firstDate; d <= lastDate; d = d.AddDays(1))
            {
                var dayStart = d + new TimeSpan(6, 0, 0);
                var dayEnd = d + new TimeSpan(20, 0, 0); // exclusive

                var overlapStart = timeIn > dayStart ? timeIn : dayStart;
                var overlapEnd = timeOut < dayEnd ? timeOut : dayEnd;
                if (overlapEnd > overlapStart)
                {
                    dayTicks += (overlapEnd - overlapStart).Ticks;
                }
            }

            var daySpan = TimeSpan.FromTicks(dayTicks);
            var nightSpan = total - daySpan;

            // Compare durations: if day>night => DAY price, if night>=day => NIGHT price (night wins tie)
            if (daySpan > nightSpan)
                return bg.GiaBanNgay ?? 0m;
            return bg.GiaQuaDem ?? 0m;
        }
    }
}
