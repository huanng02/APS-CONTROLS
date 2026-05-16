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

            // Rule 1: monthly ticket (detect by GiaThang or name or flag)
            var dbSvc = new DatabaseService();
            var loaiVe = dbSvc.GetLoaiVe().FirstOrDefault(x => x.Id == loaiVeId);
            bool isMonthly = (loaiVe != null && loaiVe.CoTheGiaHan) || 
                             (bg.GiaThang.HasValue && bg.GiaThang.Value > 0);

            if (isMonthly) return 0m;

            var khungGioList = dbSvc.GetKhungGio(); // all slot definitions
            var bangGiaKhungList = dbSvc.GetBangGiaKhungGioByBangGiaId(bg.Id);

            // Use the advanced calculator
            var (defs, ps) = QuanLyGiuXe.ViewModels.TimeSlotCalculator.MapFromDb(khungGioList, bangGiaKhungList);
            var result = QuanLyGiuXe.ViewModels.TimeSlotCalculator.Calculate(timeIn, timeOut, defs, ps);

            return result.FinalPrice;
        }

    }
}
