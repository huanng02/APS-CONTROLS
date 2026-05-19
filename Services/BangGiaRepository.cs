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

        public List<BangGia> GetAll()
        {
            return System.Threading.Tasks.Task.Run(() => GetAllAsync()).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<List<BangGia>> GetAllAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<BangGia>>(
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
        }

        public BangGia GetById(int id)
        {
            return System.Threading.Tasks.Task.Run(() => GetByIdAsync(id)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<BangGia?> GetByIdAsync(int id)
        {
            if (id <= 0) return null;
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<BangGia>(
                $"BANG_GIA_{id}",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT Id, LoaiXeId, LoaiVeId, GiaThang, TrangThai FROM dbo.BangGia WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return new BangGia
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                    LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                    GiaThang = r["GiaThang"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaThang"]) : null,
                                    TrangThai = r["TrangThai"]?.ToString() ?? string.Empty
                                };
                            }
                        }
                    }
                    return null;
                }
            );
        }

        public BangGia GetByLoaiXeAndLoaiVe(int loaiXeId, int loaiVeId)
        {
            return System.Threading.Tasks.Task.Run(() => GetByLoaiXeAndLoaiVeAsync(loaiXeId, loaiVeId)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<BangGia?> GetByLoaiXeAndLoaiVeAsync(int loaiXeId, int loaiVeId)
        {
            if (loaiXeId <= 0 || loaiVeId <= 0) return null;
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<BangGia>(
                $"BANG_GIA_LX_{loaiXeId}_LV_{loaiVeId}",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT TOP(1) Id, LoaiXeId, LoaiVeId, GiaThang, TrangThai FROM dbo.BangGia WHERE LoaiXeId = @lx AND LoaiVeId = @lv ORDER BY Id DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@lx", loaiXeId);
                        cmd.Parameters.AddWithValue("@lv", loaiVeId);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return new BangGia
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                    LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                    GiaThang = r["GiaThang"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaThang"]) : null,
                                    TrangThai = r["TrangThai"]?.ToString() ?? string.Empty
                                };
                            }
                        }
                    }
                    return null;
                }
            );
        }

        public void Insert(BangGia entity)
        {
            _ = InsertAsync(entity);
        }

        public async System.Threading.Tasks.Task<bool> InsertAsync(BangGia entity)
        {
            if (entity == null) return false;
            ValidateEntity(entity, isUpdate: false);

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
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
        }

        public void Update(BangGia entity)
        {
            _ = UpdateAsync(entity);
        }

        public async System.Threading.Tasks.Task<bool> UpdateAsync(BangGia entity)
        {
            if (entity == null || entity.Id <= 0) return false;
            ValidateEntity(entity, isUpdate: true);

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
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
        }

        public void Delete(int id)
        {
            _ = DeleteAsync(id);
        }

        public async System.Threading.Tasks.Task<bool> DeleteAsync(int id)
        {
            if (id <= 0) return false;
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_BANG_GIA",
                new { Id = id },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"DELETE FROM dbo.BangGia WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public bool Exists(int loaiXeId, int loaiVeId)
        {
            return System.Threading.Tasks.Task.Run(() => ExistsAsync(loaiXeId, loaiVeId)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<bool> ExistsAsync(int loaiXeId, int loaiVeId)
        {
            if (loaiXeId <= 0 || loaiVeId <= 0) return false;
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<bool>(
                $"BANG_GIA_EXISTS_{loaiXeId}_{loaiVeId}",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT COUNT(1) FROM dbo.BangGia WHERE LoaiXeId=@lx AND LoaiVeId=@lv", conn))
                    {
                        cmd.Parameters.AddWithValue("@lx", loaiXeId);
                        cmd.Parameters.AddWithValue("@lv", loaiVeId);
                        var v = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(v) > 0;
                    }
                }
            );
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
                return lv?.CoTheGiaHan ?? false;
            }
            catch { return false; }
        }
    }
}
