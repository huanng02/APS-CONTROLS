using System.Collections.Generic;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    public class LoaiXeService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public async System.Threading.Tasks.Task<List<LoaiXe>> GetAllAsync()
        {
            return await _db.GetLoaiXeAsync();
        }

        public async System.Threading.Tasks.Task AddAsync(string ten, string trangThai)
        {
            await _db.InsertLoaiXeAsync(ten, trangThai);
            LoggingService.Instance.LogCrud("CREATE_LOAIXE", "LoaiXe", details: $"Thêm loại xe: {ten} ({trangThai})", source: "LoaiXeService");
        }

        public async System.Threading.Tasks.Task UpdateAsync(int id, string ten, string trangThai)
        {
            await _db.UpdateLoaiXeAsync(id, ten, trangThai);
            LoggingService.Instance.LogCrud("UPDATE_LOAIXE", "LoaiXe", id.ToString(), details: $"Cập nhật loại xe ID: {id} thành: {ten} ({trangThai})", source: "LoaiXeService");
        }

        public async System.Threading.Tasks.Task DeleteAsync(int id)
        {
            await _db.DeleteLoaiXeAsync(id);
            LoggingService.Instance.LogCrud("DELETE_LOAIXE", "LoaiXe", id.ToString(), details: $"Xóa loại xe ID: {id}", source: "LoaiXeService");
        }

        // Legacy synchronous wrappers for UI compatibility
        public List<LoaiXe> GetAll() => Task.Run(() => GetAllAsync()).GetAwaiter().GetResult();
        public void Add(string ten, string trangThai) => Task.Run(() => AddAsync(ten, trangThai)).GetAwaiter().GetResult();
        public void Update(int id, string ten, string trangThai) => Task.Run(() => UpdateAsync(id, ten, trangThai)).GetAwaiter().GetResult();
        public void Delete(int id) => Task.Run(() => DeleteAsync(id)).GetAwaiter().GetResult();
    }
}
