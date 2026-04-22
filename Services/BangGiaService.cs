using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class BangGiaService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<BangGia> GetAll()
        {
            return _db.LayBangGia();
        }

        public void UpdateGia(int id, decimal giaBanNgay, decimal giaQuaDem)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            if (giaBanNgay <= 0) throw new ArgumentException("Giá ban ngày phải > 0", nameof(giaBanNgay));
            if (giaQuaDem < 0) throw new ArgumentException("GiaQuaDem phải >= 0", nameof(giaQuaDem));

            _db.UpdateBangGia(id, giaBanNgay, giaQuaDem);
        }
    }
}
