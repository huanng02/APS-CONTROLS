using System;
using System.Collections.Generic;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class LoaiVeService
    {
        private readonly DatabaseService db = new();

        public List<LoaiVe> GetAll()
        {
            return db.GetLoaiVe();
        }

        public void Add(string ten, decimal giaTien, string trangThai)
        {
            if (string.IsNullOrWhiteSpace(ten)) throw new ArgumentException("Tên loại vé không được rỗng", nameof(ten));
            if (giaTien < 0) throw new ArgumentException("Giá tiền phải >= 0", nameof(giaTien));
            db.InsertLoaiVe(ten, giaTien, trangThai ?? string.Empty);
        }

        public void Update(int id, string ten, decimal giaTien, string trangThai)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            if (string.IsNullOrWhiteSpace(ten)) throw new ArgumentException("Tên loại vé không được rỗng", nameof(ten));
            if (giaTien < 0) throw new ArgumentException("Giá tiền phải >= 0", nameof(giaTien));
            db.UpdateLoaiVe(id, ten, giaTien, trangThai ?? string.Empty);
        }

        public void Delete(int id)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            db.DeleteLoaiVe(id);
        }
    }
}
