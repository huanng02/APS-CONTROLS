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
        public decimal CalculateFee(int loaiXeId, int loaiVeId, DateTime timeIn, DateTime timeOut)
        {
            // basic validation
            if (loaiXeId <= 0 || loaiVeId <= 0) return 0m;
            if (timeOut <= timeIn) return 0m;

            var bg = _bgRepo.GetByLoaiXeAndLoaiVe(loaiXeId, loaiVeId);
            if (bg == null) return 0m;

            // Rule 1: monthly ticket
            if (bg.GiaThang.HasValue && bg.GiaThang.Value > 0) return bg.GiaThang.Value;

            var dbSvc = new DatabaseService();
            var khungGioList = dbSvc.GetKhungGio(); // all slot definitions
            var bangGiaKhungList = dbSvc.GetBangGiaKhungGioByBangGiaId(bg.Id);

            // join khung + price (price defaults to 0 if missing)
            var slots = (from k in khungGioList
                         let p = bangGiaKhungList.FirstOrDefault(x => x.KhungGioId == k.Id)
                         select new { Khung = k, Price = p != null ? p.GiaTien : 0m }).ToList();

            if (!slots.Any()) return 0m;

            decimal total = 0m;
            TimeSpan businessStart = TimeSpan.FromHours(6); // 06:00 boundary

            DateTime cur = timeIn;
            while (cur < timeOut)
            {
                // next 06:00 after cur
                var nextBoundary = cur.Date + businessStart;
                if (nextBoundary <= cur) nextBoundary = nextBoundary.AddDays(1);
                var segmentEnd = timeOut < nextBoundary ? timeOut : nextBoundary;

                bool anyNight = false;
                decimal nightPrice = 0m;
                decimal dayPrice = 0m;

                foreach (var s in slots)
                {
                    var kh = s.Khung;
                    // iterate possible occurrence dates that could overlap the segment
                    for (var d = cur.Date.AddDays(-1); d <= segmentEnd.Date; d = d.AddDays(1))
                    {
                        DateTime occStart = d + kh.GioBatDau;
                        DateTime occEnd = kh.QuaDem ? d.AddDays(1) + kh.GioKetThuc : d + kh.GioKetThuc;

                        var ovStart = occStart > cur ? occStart : cur;
                        var ovEnd = occEnd < segmentEnd ? occEnd : segmentEnd;
                        if (ovEnd > ovStart)
                        {
                            if (kh.QuaDem)
                            {
                                anyNight = true;
                                if (s.Price > nightPrice) nightPrice = s.Price;
                            }
                            else
                            {
                                if (s.Price > dayPrice) dayPrice = s.Price;
                            }
                            break;
                        }
                    }
                }

                if (anyNight)
                    total += nightPrice;
                else
                    total += dayPrice;

                cur = segmentEnd;
            }

            return Math.Round(total, 2);
        }

    }
}
