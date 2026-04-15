using System;
using System.Linq;
using System.Collections.Generic;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class PaymentService
    {
        private readonly DatabaseService _db = new DatabaseService();

        /// <summary>
        /// Calculate parking fee according to business rules.
        /// loaiVeId: when present & represents a monthly ticket (GiaTien>0) => use LoaiVe.GiaTien
        /// Otherwise use BangGia for loaiXeId
        /// </summary>
        /// <param name="loaiVeId">LoaiVe id from card (nullable)</param>
        /// <param name="loaiXeId">LoaiXe id for vehicle (nullable)</param>
        /// <param name="timeIn">Entrance time</param>
        /// <param name="timeOut">Exit time</param>
        /// <returns>decimal fee</returns>
        public decimal CalculateFee(int? loaiVeId, int? loaiXeId, DateTime timeIn, DateTime timeOut)
        {
            if (timeOut < timeIn) timeOut = timeIn;

            // 1) Monthly ticket check
            if (loaiVeId.HasValue)
            {
                var loaiVe = _db.GetLoaiVe().FirstOrDefault(x => x.Id == loaiVeId.Value);
                if (loaiVe != null)
                {
                    // If GiaTien > 0 treat as monthly/prepaid ticket
                    if (loaiVe.GiaTien > 0)
                    {
                        return loaiVe.GiaTien;
                    }
                }
            }

            // 2) Transient pricing via BangGia (by loaiXeId)
            if (!loaiXeId.HasValue)
                return 0m; // no price information

            var bg = _db.LayBangGia().FirstOrDefault(b => b.LoaiXeId.HasValue && b.LoaiXeId.Value == loaiXeId.Value);
            if (bg == null)
                return 0m;

            decimal giaTheoGio = Convert.ToDecimal(bg.GiaTheoGio ?? 0d);
            decimal giaQuaDem = Convert.ToDecimal(bg.GiaQuaDem ?? 0d);

            var span = timeOut - timeIn;
            var hours = Math.Ceiling(span.TotalHours);
            if (hours <= 0) hours = 1;

            decimal fee = (decimal)hours * giaTheoGio;

            // If crosses midnight (different date) add GiaQuaDem once
            if (timeIn.Date != timeOut.Date)
            {
                fee += giaQuaDem;
            }

            return fee;
        }
    }
}
