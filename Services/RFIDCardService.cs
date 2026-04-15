using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class RFIDCardService
    {
        private readonly DatabaseService db = new DatabaseService();

        public List<RFIDCard> GetAll()
        {
            return db.GetRFIDCards();
        }

        public void Add(string uid, string bienSo, int loaiVeId, int loaiXeId, string trangThai)
        {
            if (string.IsNullOrWhiteSpace(uid)) throw new ArgumentException("UID không được rỗng", nameof(uid));
            if (db.IsRFIDUidExists(uid)) throw new InvalidOperationException("UID đã tồn tại");
            var ngayTao = DateTime.Now;
            db.InsertRFIDCard(uid, bienSo ?? string.Empty, loaiVeId, loaiXeId, trangThai ?? string.Empty, ngayTao);
        }

        public void Update(int id, string uid, string bienSo, int loaiVeId, int loaiXeId, string trangThai)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            if (string.IsNullOrWhiteSpace(uid)) throw new ArgumentException("UID không được rỗng", nameof(uid));
            var all = db.GetRFIDCards();
            var existing = all.Find(x => x.Id == id);
            if (existing == null) throw new InvalidOperationException("Không tìm thấy thẻ");
            if (!string.Equals(existing.UID, uid, StringComparison.OrdinalIgnoreCase) && db.IsRFIDUidExists(uid))
                throw new InvalidOperationException("UID đã tồn tại");
            db.UpdateRFIDCard(id, uid, bienSo ?? string.Empty, loaiVeId, loaiXeId, trangThai ?? string.Empty);
        }

        public void Delete(int id)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            db.DeleteRFIDCard(id);
        }
    }
}
