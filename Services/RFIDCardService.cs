using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class RFIDCardService
    {
        private readonly DatabaseService db = new DatabaseService();

        public async System.Threading.Tasks.Task<System.Collections.Generic.List<RFIDCards>> GetAllAsync()
        {
            var rows = await db.GetRFIDCardsAsync();
            var list = new System.Collections.Generic.List<RFIDCards>();
            if (rows == null) return list;

            var loaiVeMap = new System.Collections.Generic.Dictionary<int, string>();
            var loaiXeMap = new System.Collections.Generic.Dictionary<int, string>();

            try
            {
                var lvs = await new LoaiVeService().GetAllAsync();
                foreach (var lv in lvs) loaiVeMap[lv.Id] = lv.TenLoai ?? string.Empty;
            }
            catch { }

            try
            {
                var lxs = await new LoaiXeService().GetAllAsync();
                foreach (var lx in lxs) loaiXeMap[lx.Id] = lx.TenLoai ?? string.Empty;
            }
            catch { }

            foreach (var r in rows)
            {
                list.Add(new RFIDCards
                {
                    Id = r.Id,
                    CardUID = r.UID,
                    CardName = r.CardName,
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

        public async System.Threading.Tasks.Task<System.Collections.Generic.List<RFIDCards>> GetByLoaiVeAsync(int loaiVeId)
        {
            if (loaiVeId <= 0) return await GetAllAsync();

            var rows = await db.GetRFIDCardsAsync();
            var filtered = rows?.FindAll(x => x.LoaiVeId == loaiVeId) ?? new System.Collections.Generic.List<RFIDCard>();
            var list = new System.Collections.Generic.List<RFIDCards>();

            var loaiVeMap = new System.Collections.Generic.Dictionary<int, string>();
            var loaiXeMap = new System.Collections.Generic.Dictionary<int, string>();

            try
            {
                var lvs = await new LoaiVeService().GetAllAsync();
                foreach (var lv in lvs) loaiVeMap[lv.Id] = lv.TenLoai ?? string.Empty;
            }
            catch { }

            try
            {
                var lxs = await new LoaiXeService().GetAllAsync();
                foreach (var lx in lxs) loaiXeMap[lx.Id] = lx.TenLoai ?? string.Empty;
            }
            catch { }

            foreach (var r in filtered)
            {
                list.Add(new RFIDCards
                {
                    Id = r.Id,
                    CardUID = r.UID,
                    CardName = r.CardName,
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

        public async System.Threading.Tasks.Task AddAsync(RFIDCards model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.CardUID)) throw new ArgumentException("CardUID không được rỗng");

            var ngayDangKy = model.NgayDangKy ?? DateTime.Now;
            var ngayHetHan = model.NgayHetHan;

            await db.InsertRFIDCardAsync(model.CardUID, model.BienSo ?? string.Empty, model.CardName ?? string.Empty, model.LoaiVeId ?? 0, model.LoaiXeId ?? 0, model.TrangThai ?? string.Empty, ngayDangKy, ngayHetHan);
        }

        public async System.Threading.Tasks.Task UpdateAsync(RFIDCards model)
        {
            if (model == null || model.Id <= 0) throw new ArgumentException("Model không hợp lệ");

            await db.UpdateRFIDCardAsync(model.Id, model.CardUID ?? string.Empty, model.BienSo ?? string.Empty, model.CardName ?? string.Empty, model.LoaiVeId ?? 0, model.LoaiXeId ?? 0, model.TrangThai ?? string.Empty, model.NgayDangKy, model.NgayHetHan);
        }

        public async System.Threading.Tasks.Task DeleteAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ");
            await db.DeleteRFIDCardAsync(id);
        }

        public async System.Threading.Tasks.Task<RFIDCards?> GetByIdAsync(int id)
        {
            if (id <= 0) return null;
            var list = await db.GetRFIDCardsAsync();
            var found = list?.FirstOrDefault(x => x.Id == id);
            if (found == null) return null;

            return new RFIDCards
            {
                Id = found.Id,
                CardUID = found.UID,
                CardName = found.CardName,
                BienSo = found.BienSo,
                LoaiVeId = found.LoaiVeId == 0 ? (int?)null : found.LoaiVeId,
                LoaiXeId = found.LoaiXeId == 0 ? (int?)null : found.LoaiXeId,
                TrangThai = found.TrangThai,
                NgayDangKy = found.NgayTao == DateTime.MinValue ? (DateTime?)null : found.NgayTao,
                NgayHetHan = found.NgayHetHan
            };
        }

        public async System.Threading.Tasks.Task GiaHanAsync(int id, int soThang)
        {
            if (id <= 0 || soThang <= 0) throw new ArgumentException("Tham số không hợp lệ");
            await db.GiaHanRFIDCardAsync(id, soThang);

            // Optimistic offline cache update
            try
            {
                var cachedCards = await QuanLyGiuXe.Services.OfflineCache.OfflineCacheService.Instance.GetCacheAsync<List<RFIDCard>>("LIST_RFID_CARDS");
                if (cachedCards != null)
                {
                    var card = cachedCards.FirstOrDefault(c => c.Id == id);
                    if (card != null)
                    {
                        var now = DateTime.Now;
                        if (!card.NgayHetHan.HasValue || card.NgayHetHan.Value < now)
                            card.NgayHetHan = now.AddMonths(soThang);
                        else
                            card.NgayHetHan = card.NgayHetHan.Value.AddMonths(soThang);
                        
                        card.TrangThai = "Active";
                        await QuanLyGiuXe.Services.OfflineCache.OfflineCacheService.Instance.SaveCacheAsync("LIST_RFID_CARDS", cachedCards);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "OptimisticUpdate", "Failed to update local cache for GiaHan", ex);
            }
        }

        // Legacy synchronous wrappers for UI compatibility
        public List<RFIDCards> GetAll() => Task.Run(() => GetAllAsync()).GetAwaiter().GetResult();
        public List<RFIDCards> GetByLoaiVe(int loaiVeId) => Task.Run(() => GetByLoaiVeAsync(loaiVeId)).GetAwaiter().GetResult();
        public RFIDCards? GetById(int id) => Task.Run(() => GetByIdAsync(id)).GetAwaiter().GetResult();
        public void Add(RFIDCards model) => Task.Run(() => AddAsync(model)).GetAwaiter().GetResult();
        public void Update(RFIDCards model) => Task.Run(() => UpdateAsync(model)).GetAwaiter().GetResult();
        public void Delete(int id) => Task.Run(() => DeleteAsync(id)).GetAwaiter().GetResult();
        public void GiaHan(int id, int soThang) => Task.Run(() => GiaHanAsync(id, soThang)).GetAwaiter().GetResult();
    }
}
