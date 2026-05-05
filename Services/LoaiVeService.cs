using System;
using System.Collections.Generic;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class LoaiVeService
    {
        private readonly LoaiVeRepository _repo = new();

        public System.Collections.Generic.List<LoaiVe> GetAll()
        {
            return _repo.GetAll();
        }

        public void Add(string ten, string trangThai, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(ten)) throw new ArgumentException("Tên loại vé không được rỗng", nameof(ten));
            var lv = new LoaiVe { TenLoai = ten, TrangThai = trangThai ?? string.Empty, Detail = detail };
            _repo.Insert(lv);
        }

        public void Update(int id, string ten, string trangThai, string detail = null)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            if (string.IsNullOrWhiteSpace(ten)) throw new ArgumentException("Tên loại vé không được rỗng", nameof(ten));
            var lv = new LoaiVe { Id = id, TenLoai = ten, TrangThai = trangThai ?? string.Empty, Detail = detail };
            _repo.Update(lv);
        }

        public void Delete(int id)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            _repo.Delete(id);
        }
    }
}
