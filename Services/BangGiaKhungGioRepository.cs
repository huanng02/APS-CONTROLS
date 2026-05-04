using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class BangGiaKhungGioRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<BangGiaKhungGio> GetByBangGiaId(int bangGiaId)
        {
            var list = new List<BangGiaKhungGio>();
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "SELECT Id, BangGiaId, KhungGioId, GiaTien FROM dbo.BangGiaKhungGio WHERE BangGiaId = @bg";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@bg", bangGiaId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
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
            }
            return list;
        }

        public void Insert(BangGiaKhungGio entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien) VALUES (@bg,@kg,@gt); SELECT SCOPE_IDENTITY();";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@bg", entity.BangGiaId);
                    cmd.Parameters.AddWithValue("@kg", entity.KhungGioId);
                    cmd.Parameters.AddWithValue("@gt", entity.GiaTien);
                    var id = cmd.ExecuteScalar();
                    entity.Id = Convert.ToInt32(id);
                }
            }
        }

        public void Update(BangGiaKhungGio entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id <= 0) throw new ArgumentException("Invalid Id", nameof(entity.Id));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "UPDATE dbo.BangGiaKhungGio SET BangGiaId=@bg, KhungGioId=@kg, GiaTien=@gt WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@bg", entity.BangGiaId);
                    cmd.Parameters.AddWithValue("@kg", entity.KhungGioId);
                    cmd.Parameters.AddWithValue("@gt", entity.GiaTien);
                    cmd.Parameters.AddWithValue("@id", entity.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int id)
        {
            if (id <= 0) return;
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "DELETE FROM dbo.BangGiaKhungGio WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
