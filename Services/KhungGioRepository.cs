using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class KhungGioRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<KhungGio> GetAll()
        {
            var list = new List<KhungGio>();
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                // Return all khung gio rows; UI will decide which are active. Some databases store TrangThai as string values.
                string q = "SELECT Id, TenKhungGio, GioBatDau, GioKetThuc, QuaDem, TrangThai FROM dbo.KhungGio ORDER BY Id";
                using (var cmd = new SqlCommand(q, sql))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new KhungGio
                        {
                            Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                            TenKhungGio = r["TenKhungGio"]?.ToString() ?? string.Empty,
                            GioBatDau = r["GioBatDau"] != DBNull.Value ? TimeSpan.Parse(r["GioBatDau"].ToString()) : TimeSpan.Zero,
                            GioKetThuc = r["GioKetThuc"] != DBNull.Value ? TimeSpan.Parse(r["GioKetThuc"].ToString()) : TimeSpan.Zero,
                            QuaDem = r["QuaDem"] != DBNull.Value ? Convert.ToBoolean(r["QuaDem"]) : false,
                            TrangThai = r["TrangThai"] != DBNull.Value ? Convert.ToBoolean(r["TrangThai"]) : false
                        });
                    }
                }
            }
            return list;
        }

        public void Update(KhungGio entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id <= 0) throw new ArgumentException("Invalid Id", nameof(entity.Id));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "UPDATE dbo.KhungGio SET TenKhungGio=@name, GioBatDau=@gb, GioKetThuc=@gk, QuaDem=@qd, TrangThai=@tt WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@name", (object?)entity.TenKhungGio ?? string.Empty);
                    cmd.Parameters.AddWithValue("@gb", entity.GioBatDau.ToString());
                    cmd.Parameters.AddWithValue("@gk", entity.GioKetThuc.ToString());
                    cmd.Parameters.AddWithValue("@qd", entity.QuaDem);
                    cmd.Parameters.AddWithValue("@tt", entity.TrangThai);
                    cmd.Parameters.AddWithValue("@id", entity.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Insert(KhungGio entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "INSERT INTO dbo.KhungGio (TenKhungGio, GioBatDau, GioKetThuc, QuaDem, TrangThai) VALUES (@name,@gb,@gk,@qd,@tt); SELECT SCOPE_IDENTITY();";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@name", (object?)entity.TenKhungGio ?? string.Empty);
                    cmd.Parameters.AddWithValue("@gb", entity.GioBatDau.ToString());
                    cmd.Parameters.AddWithValue("@gk", entity.GioKetThuc.ToString());
                    cmd.Parameters.AddWithValue("@qd", entity.QuaDem);
                    cmd.Parameters.AddWithValue("@tt", entity.TrangThai);
                    var id = cmd.ExecuteScalar();
                    if (id != null && int.TryParse(id.ToString(), out var iid)) entity.Id = iid;
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
                string q = "DELETE FROM dbo.KhungGio WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
