using System.Collections.Generic;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services
{
    public class LoaiXeService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<LoaiXe> GetAll()
        {
            return _db.GetLoaiXe();
        }

        public void Add(string ten, string trangThai)
        {
            _db.InsertLoaiXe(ten, trangThai);
        }

        public void Update(int id, string ten, string trangThai)
        {
            _db.UpdateLoaiXe(id, ten, trangThai);
        }

        public void Delete(int id)
        {
            _db.DeleteLoaiXe(id);
        }
    }
}
