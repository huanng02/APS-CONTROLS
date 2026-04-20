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
                const string q = "SELECT Id, LoaiXeId, LoaiVeId, GiaTheoGio, GiaQuaDem, GiaThang, TrangThai FROM dbo.BangGia ORDER BY Id";
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
                            GiaTheoGio = r["GiaTheoGio"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaTheoGio"]) : null,
                            GiaQuaDem = r["GiaQuaDem"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaQuaDem"]) : null,
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
                const string q = "SELECT Id, LoaiXeId, LoaiVeId, GiaTheoGio, GiaQuaDem, GiaThang, TrangThai FROM dbo.BangGia WHERE Id = @id";
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
                                GiaTheoGio = r["GiaTheoGio"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaTheoGio"]) : null,
                                GiaQuaDem = r["GiaQuaDem"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaQuaDem"]) : null,
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
                const string q = "SELECT TOP(1) Id, LoaiXeId, LoaiVeId, GiaTheoGio, GiaQuaDem, GiaThang, TrangThai FROM dbo.BangGia WHERE LoaiXeId = @lx AND LoaiVeId = @lv ORDER BY Id DESC";
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
                                GiaTheoGio = r["GiaTheoGio"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaTheoGio"]) : null,
                                GiaQuaDem = r["GiaQuaDem"] != DBNull.Value ? (decimal?)Convert.ToDecimal(r["GiaQuaDem"]) : null,
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
                const string q = @"INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaTheoGio, GiaQuaDem, GiaThang, TrangThai)
                                     VALUES (@lx,@lv,@g1,@g2,@gt,@tt)";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@lx", entity.LoaiXeId);
                    cmd.Parameters.AddWithValue("@lv", entity.LoaiVeId);
                    AddDecimalParameter(cmd, "@g1", entity.GiaTheoGio);
                    AddDecimalParameter(cmd, "@g2", entity.GiaQuaDem);
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
                const string q = @"UPDATE dbo.BangGia SET LoaiXeId=@lx, LoaiVeId=@lv, GiaTheoGio=@g1, GiaQuaDem=@g2, GiaThang=@gt, TrangThai=@tt WHERE Id=@id";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@lx", entity.LoaiXeId);
                    cmd.Parameters.AddWithValue("@lv", entity.LoaiVeId);
                    AddDecimalParameter(cmd, "@g1", entity.GiaTheoGio);
                    AddDecimalParameter(cmd, "@g2", entity.GiaQuaDem);
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
            if (entity.GiaTheoGio.HasValue && entity.GiaTheoGio.Value < 0) throw new ArgumentException("GiaTheoGio must be >= 0");
            if (entity.GiaQuaDem.HasValue && entity.GiaQuaDem.Value < 0) throw new ArgumentException("GiaQuaDem must be >= 0");
            if (entity.GiaThang.HasValue && entity.GiaThang.Value < 0) throw new ArgumentException("GiaThang must be >= 0");

            // Determine LoaiVe type by name lookup
            var lv = _db.GetLoaiVe().FirstOrDefault(x => x.Id == entity.LoaiVeId);
            var tenLoai = (lv?.TenLoai ?? string.Empty).ToLowerInvariant();

            bool isThang = tenLoai.Contains("thang") || tenLoai.Contains("tháng");
            bool isVangLai = tenLoai.Contains("vang") || tenLoai.Contains("v angl") || tenLoai.Contains("v ang");

            // Business rules
            if (isThang)
            {
                // monthly: GiaThang required, others null
                if (!entity.GiaThang.HasValue) throw new ArgumentException("GiaThang is required for monthly (Thang) ticket types.");
                if (entity.GiaTheoGio.HasValue || entity.GiaQuaDem.HasValue) throw new ArgumentException("GiaTheoGio and GiaQuaDem must be NULL for monthly (Thang) ticket types.");
            }
            else if (isVangLai)
            {
                // transient: GiaTheoGio and GiaQuaDem required, GiaThang null
                if (!entity.GiaTheoGio.HasValue) throw new ArgumentException("GiaTheoGio is required for VangLai ticket types.");
                if (!entity.GiaQuaDem.HasValue) throw new ArgumentException("GiaQuaDem is required for VangLai ticket types.");
                if (entity.GiaThang.HasValue) throw new ArgumentException("GiaThang must be NULL for VangLai ticket types.");
            }
            else
            {
                // unknown type: at least one price provided
                if (!entity.GiaThang.HasValue && !entity.GiaTheoGio.HasValue && !entity.GiaQuaDem.HasValue)
                    throw new ArgumentException("At least one price field must be provided.");
            }
        }
    }
}
