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

            // Trích xuất cấu hình từ Database
            var daySlot = slots.FirstOrDefault(s => !s.Khung.QuaDem);
            var nightSlot = slots.FirstOrDefault(s => s.Khung.QuaDem);

            decimal dayFee = daySlot?.Price ?? 0m;
            decimal nightFee = nightSlot?.Price ?? 0m;

            // Lấy mốc thời gian từ db, mặc định 6h-22h nếu không tồn tại
            TimeSpan dayStart = daySlot != null ? daySlot.Khung.GioBatDau : new TimeSpan(6, 0, 0);
            TimeSpan dayEnd = daySlot != null ? daySlot.Khung.GioKetThuc : new TimeSpan(22, 0, 0);

            // RULE 4: Qua ngày -> luôn = giá ngày hôm sau + giá đêm hôm trước
            if (timeIn.Date != timeOut.Date)
            {
                return Math.Round(dayFee + nightFee, 2);
            }

            // --- XỬ LÝ TRONG CÙNG 1 NGÀY ---
            TimeSpan startTime = timeIn.TimeOfDay;
            TimeSpan endTime = timeOut.TimeOfDay;

            // Kiểm tra xem có dính khung giờ ban ngày không?
            bool hasDay = startTime < dayEnd && endTime > dayStart;

            // Kiểm tra xem có dính khung giờ ban đêm không?
            bool hasNight = startTime < dayStart || endTime > dayEnd;

            // RULE 1: Chỉ trong ban ngày
            if (hasDay && !hasNight)
            {
                return Math.Round(dayFee, 2);
            }

            // RULE 2 & RULE 3: Chỉ có đêm HOẶC có cả ngày và đêm
            return Math.Round(nightFee, 2);
        }

    }
}
