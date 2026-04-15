using System;
using System.Collections.Generic;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class LoaiTheService
    {
        private readonly DatabaseService db = new();

        public List<LoaiThe> GetAll()
        {
            return db.GetLoaiThe();
        }

        public void Add(string ten, decimal giaTien, string trangThai)
        {
            if (string.IsNullOrWhiteSpace(ten))
                throw new ArgumentException("Tên loại thẻ không được rỗng", nameof(ten));

            if (giaTien < 0)
                throw new ArgumentException("Giá tiền phải lớn hơn hoặc bằng 0", nameof(giaTien));

            db.InsertLoaiThe(ten, giaTien, trangThai ?? string.Empty);
        }

        public void Update(int id, string ten, decimal giaTien, string trangThai)
        {
            if (id <= 0)
                throw new ArgumentException("ID không hợp lệ", nameof(id));

            if (string.IsNullOrWhiteSpace(ten))
                throw new ArgumentException("Tên loại thẻ không được rỗng", nameof(ten));

            if (giaTien < 0)
                throw new ArgumentException("Giá tiền phải lớn hơn hoặc bằng 0", nameof(giaTien));

            db.UpdateLoaiThe(id, ten, giaTien, trangThai ?? string.Empty);
        }

        public void Delete(int id)
        {
            if (id <= 0)
                throw new ArgumentException("ID không hợp lệ", nameof(id));

            db.DeleteLoaiThe(id);
        }
    }
}
