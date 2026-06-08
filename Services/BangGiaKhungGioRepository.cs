using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public class BangGiaKhungGioRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<BangGiaKhungGio> GetAll()
        {
            return System.Threading.Tasks.Task.Run(() => GetAllAsync()).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<List<BangGiaKhungGio>> GetAllAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<BangGiaKhungGio>>(
                "LIST_BANG_GIA_KHUNG_GIO",
                async conn =>
                {
                    var list = new List<BangGiaKhungGio>();
                    string q = "SELECT Id, BangGiaId, KhungGioId, GiaTien FROM dbo.BangGiaKhungGio ORDER BY Id";
                    using (var cmd = new SqlCommand(q, conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new BangGiaKhungGio
                            {
                                Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                BangGiaId = r["BangGiaId"] != DBNull.Value ? Convert.ToInt32(r["BangGiaId"]) : 0,
                                KhungGioId = r["KhungGioId"] != DBNull.Value ? Convert.ToInt32(r["KhungGioId"]) : 0,
                                GiaTien = r["GiaTien"] != DBNull.Value ? Convert.ToDecimal(r["GiaTien"]) : 0m
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<BangGiaKhungGio>();
        }

        public List<BangGiaKhungGio> GetByBangGiaId(int bangGiaId)
        {
            return System.Threading.Tasks.Task.Run(() => GetByBangGiaIdAsync(bangGiaId)).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<List<BangGiaKhungGio>> GetByBangGiaIdAsync(int bangGiaId)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<BangGiaKhungGio>>(
                $"BANG_GIA_KHUNG_GIO_{bangGiaId}",
                async conn =>
                {
                    var list = new List<BangGiaKhungGio>();
                    string q = "SELECT Id, BangGiaId, KhungGioId, GiaTien FROM dbo.BangGiaKhungGio WHERE BangGiaId = @bg";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@bg", bangGiaId);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            while (await r.ReadAsync())
                            {
                                list.Add(new BangGiaKhungGio
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    BangGiaId = r["BangGiaId"] != DBNull.Value ? Convert.ToInt32(r["BangGiaId"]) : 0,
                                    KhungGioId = r["KhungGioId"] != DBNull.Value ? Convert.ToInt32(r["KhungGioId"]) : 0,
                                    GiaTien = r["GiaTien"] != DBNull.Value ? Convert.ToDecimal(r["GiaTien"]) : 0m
                                });
                            }
                        }
                    }
                    return list;
                }
            ) ?? new List<BangGiaKhungGio>();
        }

        public void Insert(BangGiaKhungGio entity)
        {
            _ = InsertAsync(entity);
        }

        public async System.Threading.Tasks.Task<bool> InsertAsync(BangGiaKhungGio entity)
        {
            if (entity == null) return false;

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "INSERT_BANG_GIA_KHUNG_GIO",
                entity,
                async conn =>
                {
                    string q = "INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien) VALUES (@bg,@kg,@gt); SELECT SCOPE_IDENTITY();";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@bg", entity.BangGiaId);
                        cmd.Parameters.AddWithValue("@kg", entity.KhungGioId);
                        cmd.Parameters.AddWithValue("@gt", entity.GiaTien);
                        var id = await cmd.ExecuteScalarAsync();
                        entity.Id = Convert.ToInt32(id);
                    }
                }
            );
        }

        public void Update(BangGiaKhungGio entity)
        {
            _ = UpdateAsync(entity);
        }

        public async System.Threading.Tasks.Task<bool> UpdateAsync(BangGiaKhungGio entity)
        {
            if (entity == null || entity.Id <= 0) return false;

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_BANG_GIA_KHUNG_GIO",
                entity,
                async conn =>
                {
                    string q = "UPDATE dbo.BangGiaKhungGio SET BangGiaId=@bg, KhungGioId=@kg, GiaTien=@gt WHERE Id=@id";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@bg", entity.BangGiaId);
                        cmd.Parameters.AddWithValue("@kg", entity.KhungGioId);
                        cmd.Parameters.AddWithValue("@gt", entity.GiaTien);
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
                "DELETE_BANG_GIA_KHUNG_GIO",
                new { Id = id },
                async conn =>
                {
                    string q = "DELETE FROM dbo.BangGiaKhungGio WHERE Id=@id";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }
    }
}
