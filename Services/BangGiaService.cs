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

        public void UpdateGia(int id, decimal giaTheoGio, decimal giaQuaDem)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            if (giaTheoGio <= 0) throw new ArgumentException("GiaTheoGio phải > 0", nameof(giaTheoGio));
            if (giaQuaDem < 0) throw new ArgumentException("GiaQuaDem phải >= 0", nameof(giaQuaDem));

            _db.UpdateBangGia(id, giaTheoGio, giaQuaDem);
        }
    }
}
