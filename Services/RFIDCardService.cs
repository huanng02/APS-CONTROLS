using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class RFIDCardService
    {
        private readonly DatabaseService db = new DatabaseService();
        // Load all RFID cards from DB and map to UI model (includes display names for LoaiVe/LoaiXe)
        public System.Collections.Generic.List<RFIDCards> GetAll()
        {
            var rows = db.GetRFIDCards(); // returns List<RFIDCard> (DB model)
            var list = new System.Collections.Generic.List<RFIDCards>();
            if (rows == null) return list;

            // build lookup maps for display names
            var loaiVeMap = new System.Collections.Generic.Dictionary<int, string>();
            var loaiXeMap = new System.Collections.Generic.Dictionary<int, string>();
            try
            {
                var lvs = new LoaiVeService().GetAll();
                foreach (var lv in lvs) loaiVeMap[lv.Id] = lv.TenLoai ?? string.Empty;
            }
            catch { }
            try
            {
                var lxs = new LoaiXeService().GetAll();
                foreach (var lx in lxs) loaiXeMap[lx.Id] = lx.TenLoai ?? string.Empty;
            }
            catch { }

            foreach (var r in rows)
            {
                list.Add(new RFIDCards
                {
                    Id = r.Id,
                    CardUID = r.UID,
                    BienSo = r.BienSo,
                    LoaiVeId = r.LoaiVeId == 0 ? (int?)null : r.LoaiVeId,
                    LoaiXeId = r.LoaiXeId == 0 ? (int?)null : r.LoaiXeId,
                    NgayDangKy = r.NgayTao == DateTime.MinValue ? (DateTime?)null : r.NgayTao,
                    NgayHetHan = r.NgayHetHan,
                    TrangThai = r.TrangThai ?? string.Empty,
                    LoaiXe = r.LoaiXeId != 0 && loaiXeMap.TryGetValue(r.LoaiXeId, out var lxName) ? lxName : string.Empty,
                    LoaiVe = r.LoaiVeId != 0 && loaiVeMap.TryGetValue(r.LoaiVeId, out var lvName) ? lvName : string.Empty
                });
            }
            return list;
        }

        // Get by LoaiVe filter (includes display names)
        public System.Collections.Generic.List<RFIDCards> GetByLoaiVe(int loaiVeId)
        {
            if (loaiVeId <= 0) return GetAll();

            var rows = db.GetRFIDCards();
            var filtered = rows?.FindAll(x => x.LoaiVeId == loaiVeId) ?? new System.Collections.Generic.List<RFIDCard>();
            var list = new System.Collections.Generic.List<RFIDCards>();

            // build lookup maps for display names
            var loaiVeMap = new System.Collections.Generic.Dictionary<int, string>();
            var loaiXeMap = new System.Collections.Generic.Dictionary<int, string>();
            try
            {
                var lvs = new LoaiVeService().GetAll();
                foreach (var lv in lvs) loaiVeMap[lv.Id] = lv.TenLoai ?? string.Empty;
            }
            catch { }
            try
            {
                var lxs = new LoaiXeService().GetAll();
                foreach (var lx in lxs) loaiXeMap[lx.Id] = lx.TenLoai ?? string.Empty;
            }
            catch { }

            foreach (var r in filtered)
            {
                list.Add(new RFIDCards
                {
                    Id = r.Id,
                    CardUID = r.UID,
                    BienSo = r.BienSo,
                    LoaiVeId = r.LoaiVeId == 0 ? (int?)null : r.LoaiVeId,
                    LoaiXeId = r.LoaiXeId == 0 ? (int?)null : r.LoaiXeId,
                    NgayDangKy = r.NgayTao == DateTime.MinValue ? (DateTime?)null : r.NgayTao,
                    NgayHetHan = r.NgayHetHan,
                    TrangThai = r.TrangThai ?? string.Empty,
                    LoaiXe = r.LoaiXeId != 0 && loaiXeMap.TryGetValue(r.LoaiXeId, out var lxName) ? lxName : string.Empty,
                    LoaiVe = r.LoaiVeId != 0 && loaiVeMap.TryGetValue(r.LoaiVeId, out var lvName) ? lvName : string.Empty
                });
            }
            return list;
        }

        public void Add(RFIDCards model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.CardUID)) throw new ArgumentException("CardUID không được rỗng", nameof(model.CardUID));
            if (db.IsRFIDUidExists(model.CardUID)) throw new InvalidOperationException("CardUID đã tồn tại");

            // Determine NgayHetHan logic if provided by model (caller should set NgayHetHan when needed)
            var ngayDangKy = model.NgayDangKy ?? DateTime.Now;
            var ngayHetHan = model.NgayHetHan;

            db.InsertRFIDCard(model.CardUID, model.BienSo ?? string.Empty, model.LoaiVeId ?? 0, model.LoaiXeId ?? 0, model.TrangThai ?? string.Empty, ngayDangKy, ngayHetHan);
        }

        public void Update(RFIDCards model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.Id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(model.Id));
            // CardUID is readonly on edit, so we don't allow changing it here; but still ensure it's not empty
            if (string.IsNullOrWhiteSpace(model.CardUID)) throw new ArgumentException("CardUID không được rỗng", nameof(model.CardUID));

            // existing record check
            var all = db.GetRFIDCards();
            var existing = all?.Find(x => x.Id == model.Id);
            if (existing == null) throw new InvalidOperationException("Không tìm thấy thẻ");

            // if CardUID changed (shouldn't), prevent duplicate
            if (!string.Equals(existing.UID, model.CardUID, StringComparison.OrdinalIgnoreCase) && db.IsRFIDUidExists(model.CardUID))
                throw new InvalidOperationException("CardUID đã tồn tại");

            db.UpdateRFIDCard(model.Id, model.CardUID, model.BienSo ?? string.Empty, model.LoaiVeId ?? 0, model.LoaiXeId ?? 0, model.TrangThai ?? string.Empty, model.NgayDangKy, model.NgayHetHan);
        }

        public void Delete(int id)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            db.DeleteRFIDCard(id);
        }

        // Get by id - map DatabaseService RFIDCard to project RFIDCards model
        public RFIDCards? GetById(int id)
        {
            if (id <= 0) return null;
            var list = db.GetRFIDCards(); // returns List<RFIDCard>
            var found = list?.FirstOrDefault(x => x.Id == id);
            if (found == null) return null;
            return new RFIDCards
            {
                Id = found.Id,
                CardUID = found.UID,
                BienSo = found.BienSo,
                LoaiVeId = found.LoaiVeId == 0 ? (int?)null : found.LoaiVeId,
                LoaiXeId = found.LoaiXeId == 0 ? (int?)null : found.LoaiXeId,
                TrangThai = found.TrangThai,
                NgayDangKy = found.NgayTao == DateTime.MinValue ? (DateTime?)null : found.NgayTao,
                NgayHetHan = found.NgayHetHan
            };
        }
    }
}
