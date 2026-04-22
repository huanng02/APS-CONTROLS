using System;
using System.Collections.Generic;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    // Service specifically for admin updates to BangGia - restricts operations to update only
    public class BangGiaAdminService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<BangGia> GetAll()
        {
            return _db.LayBangGia();
        }

        public void UpdateBangGia(BangGia model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.Id <= 0) throw new ArgumentException("Invalid Id", nameof(model.Id));
            if (!model.GiaBanNgay.HasValue || model.GiaBanNgay.Value <= 0) throw new ArgumentException("Giá ban ngày phải > 0");
            if (!model.GiaQuaDem.HasValue || model.GiaQuaDem.Value < 0) throw new ArgumentException("GiaQuaDem must be >= 0");

            _db.UpdateBangGia(model.Id, model.GiaBanNgay.Value, model.GiaQuaDem.Value);
        }
    }
}
