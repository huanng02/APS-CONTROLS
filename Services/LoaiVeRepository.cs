using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public class LoaiVeRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<LoaiVe> GetAll()
        {
            return System.Threading.Tasks.Task.Run(() => GetAllAsync()).GetAwaiter().GetResult();
        }

        public async System.Threading.Tasks.Task<List<LoaiVe>> GetAllAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<LoaiVe>>(
                "LIST_LOAI_VE",
                async conn =>
                {
                    var list = new List<LoaiVe>();
                    string q = "SELECT Id, TenLoai, TrangThai, Detail, CoTheGiaHan FROM LoaiVe";
                    using (var cmd = new SqlCommand(q, conn))
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            var lv = new LoaiVe
                            {
                                Id = rdr["Id"] != DBNull.Value ? Convert.ToInt32(rdr["Id"]) : 0,
                                TenLoai = rdr["TenLoai"]?.ToString() ?? string.Empty,
                                TrangThai = rdr["TrangThai"]?.ToString() ?? string.Empty,
                                Detail = rdr.IsDBNull(rdr.GetOrdinal("Detail")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Detail")),
                                CoTheGiaHan = rdr["CoTheGiaHan"] != DBNull.Value && Convert.ToBoolean(rdr["CoTheGiaHan"])
                            };
                            list.Add(lv);
                        }
                    }
                    return list;
                }
            ) ?? new List<LoaiVe>();
        }

        public void Insert(LoaiVe lv)
        {
            _ = InsertAsync(lv);
        }

        public async System.Threading.Tasks.Task<bool> InsertAsync(LoaiVe lv)
        {
            if (lv == null) return false;

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "INSERT_LOAI_VE",
                lv,
                async conn =>
                {
                    string q = @"INSERT INTO LoaiVe (TenLoai, TrangThai, Detail) VALUES (@ten, @trang, @detail)";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ten", lv.TenLoai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@trang", lv.TrangThai ?? string.Empty);
                        string detail = string.IsNullOrWhiteSpace(lv.Detail) ? "Chưa có mô tả" : lv.Detail;
                        cmd.Parameters.AddWithValue("@detail", (object)detail ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public void Update(LoaiVe lv)
        {
            _ = UpdateAsync(lv);
        }

        public async System.Threading.Tasks.Task<bool> UpdateAsync(LoaiVe lv)
        {
            if (lv == null) return false;

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_LOAI_VE",
                lv,
                async conn =>
                {
                    string q = @"UPDATE LoaiVe SET TenLoai=@ten, TrangThai=@trang, Detail=@detail WHERE Id=@id";
                    using (var cmd = new SqlCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@ten", lv.TenLoai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@trang", lv.TrangThai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@detail", string.IsNullOrWhiteSpace(lv.Detail) ? (object)DBNull.Value : lv.Detail);
                        cmd.Parameters.AddWithValue("@id", lv.Id);
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
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_LOAI_VE",
                new { Id = id },
                async conn =>
                {
                    string q = "DELETE FROM LoaiVe WHERE Id=@id";
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
