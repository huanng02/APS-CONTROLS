using System;
using System.Collections.Generic;
using QuanLyGiuXe.Models;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    public class LoaiVeService
    {
        private readonly LoaiVeRepository _repo = new();

        public async System.Threading.Tasks.Task<List<LoaiVe>> GetAllAsync()
        {
            return await _repo.GetAllAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(string ten, string trangThai, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(ten)) throw new ArgumentException("Tên loại vé không được rỗng", nameof(ten));
            var lv = new LoaiVe { TenLoai = ten, TrangThai = trangThai ?? string.Empty, Detail = detail };
            await _repo.InsertAsync(lv);
        }

        public async System.Threading.Tasks.Task UpdateAsync(int id, string ten, string trangThai, string detail = null)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            if (string.IsNullOrWhiteSpace(ten)) throw new ArgumentException("Tên loại vé không được rỗng", nameof(ten));
            var lv = new LoaiVe { Id = id, TenLoai = ten, TrangThai = trangThai ?? string.Empty, Detail = detail };
            await _repo.UpdateAsync(lv);
        }

        public async System.Threading.Tasks.Task DeleteAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            await _repo.DeleteAsync(id);
        }

        // Legacy synchronous wrappers for UI compatibility
        public List<LoaiVe> GetAll() => Task.Run(() => GetAllAsync()).GetAwaiter().GetResult();
        public void Add(string ten, string trangThai, string detail = null) => Task.Run(() => AddAsync(ten, trangThai, detail)).GetAwaiter().GetResult();
        public void Update(int id, string ten, string trangThai, string detail = null) => Task.Run(() => UpdateAsync(id, ten, trangThai, detail)).GetAwaiter().GetResult();
        public void Delete(int id) => Task.Run(() => DeleteAsync(id)).GetAwaiter().GetResult();
    }
}
