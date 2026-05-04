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

        public void UpdateGia(int id, decimal giaThang)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            if (giaThang <= 0) throw new ArgumentException("Giá tháng phải > 0", nameof(giaThang));

            _db.UpdateBangGia(id, giaThang, "Active");
        }
    }
}
