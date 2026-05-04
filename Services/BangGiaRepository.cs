using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class BangGiaRepository
    {
        private readonly DatabaseService _db = new DatabaseService();

        /// <summary>
        /// Get all BangGia rows.
        /// </summary>
        public List<BangGia> GetAll()
        {
            var list = new List<BangGia>();
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                // New schema: pricing per KhungGio. Only read GiaThang here.
                string q = "SELECT Id, LoaiXeId, LoaiVeId, GiaThang, TrangThai FROM dbo.BangGia ORDER BY Id";
                using (var cmd = new SqlCommand(q, sql))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
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
            }
            return list;
        }

        /// <summary>
        /// Get by primary id
        /// </summary>
        public BangGia GetById(int id)
        {
            if (id <= 0) return null;
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "SELECT Id, LoaiXeId, LoaiVeId, GiaThang, TrangThai FROM dbo.BangGia WHERE Id = @id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
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
            }
            return null;
        }

        /// <summary>
        /// Get BangGia by LoaiXeId + LoaiVeId (returns most recent match if multiple)
        /// </summary>
        public BangGia GetByLoaiXeAndLoaiVe(int loaiXeId, int loaiVeId)
        {
            if (loaiXeId <= 0 || loaiVeId <= 0) return null;
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                string q = "SELECT TOP(1) Id, LoaiXeId, LoaiVeId, GiaThang, TrangThai FROM dbo.BangGia WHERE LoaiXeId = @lx AND LoaiVeId = @lv ORDER BY Id DESC";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@lx", loaiXeId);
                    cmd.Parameters.AddWithValue("@lv", loaiVeId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
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
            }
            return null;
        }

        /// <summary>
        /// Insert new BangGia. Validates business rules and uniqueness.
        /// </summary>
        public void Insert(BangGia entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            ValidateEntity(entity, isUpdate: false);
            if (Exists(entity.LoaiXeId, entity.LoaiVeId))
                throw new InvalidOperationException("A pricing row for the given LoaiXeId and LoaiVeId already exists.");

            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                // New model: do not write legacy per-day/night columns from repository.
                string q = @"INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai)
                                     VALUES (@lx,@lv,@gt,@tt)";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@lx", entity.LoaiXeId);
                    cmd.Parameters.AddWithValue("@lv", entity.LoaiVeId);
                    AddDecimalParameter(cmd, "@gt", entity.GiaThang);
                    cmd.Parameters.AddWithValue("@tt", (object?)entity.TrangThai ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Update existing BangGia. Validates business rules and uniqueness.
        /// </summary>
        public void Update(BangGia entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id <= 0) throw new ArgumentException("Invalid Id", nameof(entity));
            ValidateEntity(entity, isUpdate: true);

            // ensure uniqueness: no other row with same pair
            var existing = GetByLoaiXeAndLoaiVe(entity.LoaiXeId, entity.LoaiVeId);
            if (existing != null && existing.Id != entity.Id)
                throw new InvalidOperationException("Another pricing row with same LoaiXeId and LoaiVeId exists.");

            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                // New model: do not update legacy per-day/night columns here.
                string q = @"UPDATE dbo.BangGia SET LoaiXeId=@lx, LoaiVeId=@lv, GiaThang=@gt, TrangThai=@tt WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@lx", entity.LoaiXeId);
                    cmd.Parameters.AddWithValue("@lv", entity.LoaiVeId);
                    AddDecimalParameter(cmd, "@gt", entity.GiaThang);
                    cmd.Parameters.AddWithValue("@tt", (object?)entity.TrangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@id", entity.Id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Delete by id
        /// </summary>
        public void Delete(int id)
        {
            if (id <= 0) throw new ArgumentException("Invalid id", nameof(id));
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                const string q = "DELETE FROM dbo.BangGia WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Check existence of LoaiXeId+LoaiVeId pair
        /// </summary>
        public bool Exists(int loaiXeId, int loaiVeId)
        {
            if (loaiXeId <= 0 || loaiVeId <= 0) return false;
            string conn = _db.GetConnectionString();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                const string q = "SELECT COUNT(1) FROM dbo.BangGia WHERE LoaiXeId=@lx AND LoaiVeId=@lv";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@lx", loaiXeId);
                    cmd.Parameters.AddWithValue("@lv", loaiVeId);
                    var v = cmd.ExecuteScalar();
                    return Convert.ToInt32(v) > 0;
                }
            }
        }

        // ------------------ helpers ------------------
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

            // Price values must be non-negative when provided
            if (entity.GiaThang.HasValue && entity.GiaThang.Value < 0) throw new ArgumentException("GiaThang must be >= 0");

            // Determine ticket type using name-based detection (not hardcoded IDs)
            bool isThang = IsMonthlyTicket(entity.LoaiVeId);
            bool isVangLai = !isThang;

            if (isThang)
            {
                // monthly: GiaThang required and non-negative
                if (!entity.GiaThang.HasValue) throw new ArgumentException("GiaThang is required for monthly (Thang) ticket types.");
                if (entity.GiaThang.HasValue && entity.GiaThang.Value < 0) throw new ArgumentException("GiaThang must be >= 0");
            }
            else if (isVangLai)
            {
                // transient (vé lượt, vãng lai, etc.): pricing is driven by BangGiaKhungGio; ensure GiaThang is cleared
                entity.GiaThang = null;
            }
        }

        /// Check using DB CoTheGiaHan column
        /// </summary>
        private bool IsMonthlyTicket(int loaiVeId)
        {
            if (loaiVeId <= 0) return false;
            try
            {
                var loaiVeList = _db.GetLoaiVe();
                var lv = loaiVeList.FirstOrDefault(x => x.Id == loaiVeId);
                if (lv == null) return false;
                return lv.CoTheGiaHan;
            }
            catch { return false; }
        }
    }
}
