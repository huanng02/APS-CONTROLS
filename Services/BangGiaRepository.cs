using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public class BangGiaRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        private static List<BangGia>? _cachedBangGia;
        private static readonly object _cacheLock = new();

        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedBangGia = null;
            }
        }

        public List<BangGia> GetAll()
        {
            return System.Threading.Tasks.Task.Run(() => GetAllAsync()).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<List<BangGia>> GetAllAsync()
        {
            lock (_cacheLock)
            {
                if (_cachedBangGia != null) return _cachedBangGia;
            }

            var list = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<BangGia>>(
                "LIST_BANG_GIA",
                async conn =>
                {
                    var list = new List<BangGia>();
                    using (var cmd = new SqlCommand( @"SELECT Id, LoaiXeId, LoaiVeId, GiaThang, TrangThai FROM dbo.BangGia ORDER BY Id", conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new BangGia
                            {
                                Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                GiaThang = r["GiaThang"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaThang"]) : null,
                                TrangThai = r["TrangThai"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<BangGia>();

            lock (_cacheLock)
            {
                _cachedBangGia = list;
            }
            return list;
        }

        public BangGia GetById(int id)
        {
            return System.Threading.Tasks.Task.Run(() => GetByIdAsync(id)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<BangGia?> GetByIdAsync(int id)
        {
            if (id <= 0) return null;
            var list = await GetAllAsync();
            return list.FirstOrDefault(x => x.Id == id);
        }

        public BangGia GetByLoaiXeAndLoaiVe(int loaiXeId, int loaiVeId)
        {
            return System.Threading.Tasks.Task.Run(() => GetByLoaiXeAndLoaiVeAsync(loaiXeId, loaiVeId)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<BangGia?> GetByLoaiXeAndLoaiVeAsync(int loaiXeId, int loaiVeId)
        {
            if (loaiXeId <= 0 || loaiVeId <= 0) return null;
            var list = await GetAllAsync();
            return list.FirstOrDefault(x => x.LoaiXeId == loaiXeId && x.LoaiVeId == loaiVeId);
        }

        public void Insert(BangGia entity)
        {
            System.Threading.Tasks.Task.Run(() => InsertAsync(entity)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<bool> InsertAsync(BangGia entity)
        {
            if (entity == null) return false;
            ValidateEntity(entity, isUpdate: false);

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "INSERT_BANG_GIA",
                entity,
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai) VALUES (@lx,@lv,@gt,@tt)", conn))
                    {
                        cmd.Parameters.AddWithValue("@lx", entity.LoaiXeId);
                        cmd.Parameters.AddWithValue("@lv", entity.LoaiVeId);
                        AddDecimalParameter(cmd, "@gt", entity.GiaThang);
                        cmd.Parameters.AddWithValue("@tt", (object?)entity.TrangThai ?? string.Empty);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateCache();
            return success;
        }

        public void Update(BangGia entity)
        {
            System.Threading.Tasks.Task.Run(() => UpdateAsync(entity)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<bool> UpdateAsync(BangGia entity)
        {
            if (entity == null || entity.Id <= 0) return false;
            ValidateEntity(entity, isUpdate: true);

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_BANG_GIA",
                entity,
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE dbo.BangGia SET LoaiXeId=@lx, LoaiVeId=@lv, GiaThang=@gt, TrangThai=@tt WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@lx", entity.LoaiXeId);
                        cmd.Parameters.AddWithValue("@lv", entity.LoaiVeId);
                        AddDecimalParameter(cmd, "@gt", entity.GiaThang);
                        cmd.Parameters.AddWithValue("@tt", (object?)entity.TrangThai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@id", entity.Id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateCache();
            return success;
        }

        public void Delete(int id)
        {
            System.Threading.Tasks.Task.Run(() => DeleteAsync(id)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<bool> DeleteAsync(int id)
        {
            if (id <= 0) return false;
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_BANG_GIA",
                new { Id = id },
                async conn =>
                {
                    // Delete child BangGiaKhungGio rows first (FK constraint)
                    using (var cmdChild = new SqlCommand( @"DELETE FROM dbo.BangGiaKhungGio WHERE BangGiaId=@id", conn))
                    {
                        cmdChild.Parameters.AddWithValue("@id", id);
                        await cmdChild.ExecuteNonQueryAsync();
                    }
                    // Then delete parent BangGia row
                    using (var cmd = new SqlCommand( @"DELETE FROM dbo.BangGia WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateCache();
            return success;
        }

        public bool Exists(int loaiXeId, int loaiVeId)
        {
            return System.Threading.Tasks.Task.Run(() => ExistsAsync(loaiXeId, loaiVeId)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<bool> ExistsAsync(int loaiXeId, int loaiVeId)
        {
            if (loaiXeId <= 0 || loaiVeId <= 0) return false;
            var list = await GetAllAsync();
            return list.Any(x => x.LoaiXeId == loaiXeId && x.LoaiVeId == loaiVeId);
        }

        private void AddDecimalParameter(SqlCommand cmd, string name, decimal? value)
        {
            var p = cmd.Parameters.Add(name, SqlDbType.Decimal);
            p.Precision = 18;
            p.Scale = 2;
            p.Value = (object?)value ?? DBNull.Value;
        }

        private void ValidateEntity(BangGia entity, bool isUpdate)
        {
            if (entity.LoaiXeId <= 0) throw new ArgumentException("LoaiXeId is required and must be > 0", nameof(entity.LoaiXeId));
            if (entity.LoaiVeId <= 0) throw new ArgumentException("LoaiVeId is required and must be > 0", nameof(entity.LoaiVeId));

            if (entity.GiaThang.HasValue && entity.GiaThang.Value < 0) throw new ArgumentException("GiaThang must be >= 0");

            bool isThang = IsMonthlyTicket(entity.LoaiVeId);
            bool isVangLai = !isThang;

            if (isThang)
            {
                if (!entity.GiaThang.HasValue) throw new ArgumentException("GiaThang is required for monthly (Thang) ticket types.");
            }
            else if (isVangLai)
            {
                entity.GiaThang = null;
            }
        }

        private bool IsMonthlyTicket(int loaiVeId)
        {
            if (loaiVeId <= 0) return false;
            try
            {
                // Use the service which is now offline-aware
                var loaiVeList = new LoaiVeService().GetAll();
                var lv = loaiVeList.FirstOrDefault(x => x.Id == loaiVeId);
                if (lv == null) return false;
                if (lv.CoTheGiaHan) return true;

                var name = (lv.TenLoai ?? string.Empty).ToLowerInvariant();
                return name.Contains("thang") || name.Contains("tháng") || name.Contains("month") ||
                       name.Contains("gia han") || name.Contains("gia hạn") || name.Contains("giahan");
            }
            catch { return false; }
        }
    }
}
