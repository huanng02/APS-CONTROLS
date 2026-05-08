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
            try
            {
                LoggingService.Instance.LogCrud("CREATE_VEHICLE_TYPE", "LoaiXe", ten, null, new { TenLoai = ten, TrangThai = trangThai }, source: "LoaiXeService");
            }
            catch { }
        }

        public void Update(int id, string ten, string trangThai)
        {
            _db.UpdateLoaiXe(id, ten, trangThai);
            try
            {
                LoggingService.Instance.LogCrud("UPDATE_VEHICLE_TYPE", "LoaiXe", id.ToString(), null, new { Id = id, TenLoai = ten, TrangThai = trangThai }, source: "LoaiXeService");
            }
            catch { }
        }

        public void Delete(int id)
        {
            string name = string.Empty;
            try
            {
                var all = _db.GetLoaiXe();
                name = all.FirstOrDefault(x => x.Id == id)?.TenLoai ?? id.ToString();
            }
            catch { }

            _db.DeleteLoaiXe(id);

            try
            {
                LoggingService.Instance.LogCrud("DELETE_VEHICLE_TYPE", "LoaiXe", id.ToString(), null, null, source: "LoaiXeService", details: $"Deleted vehicle type: {name}");
            }
            catch { }
        }
    }
}
