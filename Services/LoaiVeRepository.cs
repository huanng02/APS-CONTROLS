using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class LoaiVeRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        public List<LoaiVe> GetAll()
        {
            var list = new List<LoaiVe>();
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "SELECT Id, TenLoai, TrangThai, Detail FROM LoaiVe";
                using (var cmd = new SqlCommand(q, sql))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var lv = new LoaiVe
                        {
                            Id = rdr["Id"] != DBNull.Value ? Convert.ToInt32(rdr["Id"]) : 0,
                            TenLoai = rdr["TenLoai"]?.ToString() ?? string.Empty,
                            TrangThai = rdr["TrangThai"]?.ToString() ?? string.Empty,
                            Detail = rdr.IsDBNull(rdr.GetOrdinal("Detail")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Detail"))
                        };
                        list.Add(lv);
                    }
                }
            }
            return list;
        }

        public void Insert(LoaiVe lv)
        {
            if (lv == null) throw new ArgumentNullException(nameof(lv));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = @"INSERT INTO LoaiVe (TenLoai, TrangThai, Detail)
                             VALUES (@ten, @trang, @detail)";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@ten", lv.TenLoai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@trang", lv.TrangThai ?? string.Empty);
                    string detail = string.IsNullOrWhiteSpace(lv.Detail) ? "Chưa có mô tả" : lv.Detail;
                    cmd.Parameters.AddWithValue("@detail", (object)detail ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Update(LoaiVe lv)
        {
            if (lv == null) throw new ArgumentNullException(nameof(lv));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = @"UPDATE LoaiVe SET TenLoai=@ten, TrangThai=@trang, Detail=@detail WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@ten", lv.TenLoai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@trang", lv.TrangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@detail", string.IsNullOrWhiteSpace(lv.Detail) ? (object)DBNull.Value : lv.Detail);
                    cmd.Parameters.AddWithValue("@id", lv.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int id)
        {
            if (id <= 0) throw new ArgumentException("ID không hợp lệ", nameof(id));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "DELETE FROM LoaiVe WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
