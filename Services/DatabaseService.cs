using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public partial class DatabaseService
    {
        private string primaryConnection = "Server=DESKTOP-BFOEO42\\SQLEXPRESS02;Database=BaiXe;Trusted_Connection=True;TrustServerCertificate=True;";
        private string backupConnection = "Server=BACKUP_SERVER;Database=Baixe;Trusted_Connection=True;";

        private string GetWorkingConnection()
        {
            // Try primary connection first
            try
            {
                using (SqlConnection conn = new SqlConnection(primaryConnection))
                {
                    conn.Open();
                    return primaryConnection;
                }
            }
            catch
            {
                // fallback to backup
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(backupConnection))
                {
                    conn.Open();
                    return backupConnection;
                }
            }
            catch
            {
                throw new Exception("Database connection failed. Both primary and backup servers are unavailable.");
            }
        }
        /// <summary>
        /// Lookup an RFIDCard by plate (BienSo). Returns null if not found.
        /// </summary>
        public RFIDCard GetRFIDCardByBienSo(string bienSo)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "SELECT Id, CardUID, BienSo, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy FROM RFIDCards WHERE BienSo = @bs";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@bs", bienSo ?? string.Empty);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return new RFIDCard
                            {
                                Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                UID = r["CardUID"]?.ToString() ?? string.Empty,
                                BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                                LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                                NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue
                            };
                        }
                    }
                }
            }

            return null;
        }

        public void UpdateXeRaById(int id, DateTime thoiGianRa)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "UPDATE XeTrongBai SET ThoiGianRa = @ra WHERE Id = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ra", thoiGianRa);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// Calculate parking fee based on vehicle type, ticket type and duration.
        /// - If LoaiVe indicates a monthly/subscription type => returns 0.
        /// - Uses BangGia and KhungGio rules (Day/Night logic). Falls back to 5000/hour if not configured.
        /// </summary>
        public double TinhTien(int? loaiXeId, int? loaiVeId, DateTime checkIn, DateTime checkOut)
        {
            try
            {
                // default per-hour fallback
                double defaultRate = 5000.0;

                // Resolve LoaiVe to determine if it's monthly/subscription
                if (loaiVeId.HasValue && loaiVeId.Value > 0)
                {
                    var loaiVe = GetLoaiVe().FirstOrDefault(x => x.Id == loaiVeId.Value);
                    if (loaiVe != null)  
                    {
                        var name = (loaiVe.TenLoai ?? string.Empty).ToLowerInvariant();
                        if (name.Contains("thang") || name.Contains("tháng") || name.Contains("month"))
                        {
                            // monthly/subscription: charge handled outside (0 here)
                            return 0.0;
                        }
                    }
                }

                // Pricing is now DB-driven via KhungGio + BangGiaKhungGio.
                var bangGia = LayBangGia().FirstOrDefault(x => x.LoaiXeId == loaiXeId && x.LoaiVeId == loaiVeId);
                if (bangGia != null)
                {
                    var khungs = GetKhungGio();
                    var prices = GetBangGiaKhungGioByBangGiaId(bangGia.Id);

                    var dayKhung = khungs.FirstOrDefault(k => !k.QuaDem);
                    var nightKhung = khungs.FirstOrDefault(k => k.QuaDem);

                    var dayGia = dayKhung != null ? prices.FirstOrDefault(x => x.KhungGioId == dayKhung.Id) : null;
                    var nightGia = nightKhung != null ? prices.FirstOrDefault(x => x.KhungGioId == nightKhung.Id) : null;

                    decimal dayFee = dayGia != null ? dayGia.GiaTien : 0m;
                    decimal nightFee = nightGia != null ? nightGia.GiaTien : 0m;

                    TimeSpan dayStart = dayKhung != null ? dayKhung.GioBatDau : new TimeSpan(6, 0, 0);
                    TimeSpan dayEnd = dayKhung != null ? dayKhung.GioKetThuc : new TimeSpan(22, 0, 0);

                    decimal finalPrice = 0m;

                    if (checkIn.Date != checkOut.Date)
                    {
                        finalPrice = dayFee + nightFee;
                    }
                    else
                    {
                        TimeSpan startTime = checkIn.TimeOfDay;
                        TimeSpan endTime = checkOut.TimeOfDay;

                        bool hasDay = startTime < dayEnd && endTime > dayStart;
                        bool hasNight = startTime < dayStart || endTime > dayEnd;

                        if (hasDay && !hasNight)
                        {
                            finalPrice = dayFee;
                        }
                        else
                        {
                            finalPrice = nightFee;
                        }
                    }
                    return (double)finalPrice;
                }

                // Fallback if no BangGia configured
                var duration = checkOut - checkIn;
                double hours = Math.Ceiling(duration.TotalHours <= 0 ? 1 : duration.TotalHours);
                return defaultRate * hours;
            }
            catch
            {
                // On any failure, fallback to simple rule to preserve compatibility
                var duration = checkOut - checkIn;
                double hours = Math.Ceiling(duration.TotalHours <= 0 ? 1 : duration.TotalHours);
                return 5000.0 * hours;
            }
        }

        // Return active KhungGio entries
        public List<QuanLyGiuXe.Models.KhungGio> GetKhungGio()
        {
            var repo = new KhungGioRepository();
            return repo.GetAll();
        }

        // Return BangGiaKhungGio entries for a BangGia id
        public List<QuanLyGiuXe.Models.BangGiaKhungGio> GetBangGiaKhungGioByBangGiaId(int bangGiaId)
        {
            var repo = new BangGiaKhungGioRepository();
            return repo.GetByBangGiaId(bangGiaId);
        }

        // Expose working connection string for UI components
        public string GetConnectionString() => GetWorkingConnection();

        // Cache for column existence checks to avoid repeated metadata queries
        private readonly Dictionary<string, bool> _columnExistsCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Check whether a column exists in a table. tableName may be 'BangGia' or 'dbo.BangGia'.
        public bool ColumnExistsInTable(string tableName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(columnName)) return false;

            // normalize
            string schema = "dbo";
            string table = tableName;
            if (tableName.Contains('.'))
            {
                var parts = tableName.Split(new[] {'.'}, 2);
                schema = parts[0].Trim('[', ']');
                table = parts[1].Trim('[', ']');
            }

            string cacheKey = $"{schema}.{table}.{columnName}";
            lock (_columnExistsCache)
            {
                if (_columnExistsCache.TryGetValue(cacheKey, out var cached)) return cached;
            }

            bool exists = false;
            string conn = GetWorkingConnection();
            using (var sql = new SqlConnection(conn))
            {
                sql.Open();
                const string q = @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table AND COLUMN_NAME = @col";
                using (var cmd = new SqlCommand(q, sql))
                {
                    cmd.Parameters.AddWithValue("@schema", schema);
                    cmd.Parameters.AddWithValue("@table", table);
                    cmd.Parameters.AddWithValue("@col", columnName);
                    var v = cmd.ExecuteScalar();
                    exists = Convert.ToInt32(v) > 0;
                }
            }

            lock (_columnExistsCache)
            {
                _columnExistsCache[cacheKey] = exists;
            }

            return exists;
        }

        // Legacy helpers removed: pricing is fully driven by KhungGio + BangGiaKhungGio.

        public RFIDCard GetRFIDCardByUid(string uid)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "SELECT Id, CardUID, BienSo, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy FROM RFIDCards WHERE CardUID = @uid";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return new RFIDCard
                            {
                                Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                UID = r["CardUID"]?.ToString() ?? string.Empty,
                                BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                                LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                                NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get entry time for a vehicle currently in the lot by its plate.
        /// Returns null if not found.
        /// </summary>
        public DateTime? GetXeVaoTimeByBienSo(string bienSo)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "SELECT ThoiGianVao FROM XeTrongBai WHERE BienSo = @bs";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@bs", bienSo ?? string.Empty);
                    var v = cmd.ExecuteScalar();
                    if (v != null && v != DBNull.Value)
                    {
                        return Convert.ToDateTime(v);
                    }
                }
            }

            return null;
        }

        private void ExecuteNonQuery(string sql, Action<SqlCommand> addParams)
        {
            string conn = GetWorkingConnection();
            using (SqlConnection con = new SqlConnection(conn))
            {
                con.Open();
                using (SqlCommand cmd = new SqlCommand(sql, con))
                {
                    addParams(cmd);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // =====================================================
        // 🟢 MODERN (MVVM CLEAN VERSION)
        // =====================================================

        public List<LoaiXe> GetLoaiXe()
        {
            var list = new List<LoaiXe>();

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "SELECT Id, TenLoai, TrangThai FROM LoaiXe";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new LoaiXe
                        {
                            Id = (int)r["Id"],
                            TenLoai = r["TenLoai"].ToString(),
                            TrangThai = r["TrangThai"].ToString()
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Bulk insert RFIDCards from a DataTable. Columns must match: CardUID,BienSo,LoaiVeId,LoaiXeId,NgayDangKy,NgayHetHan,TrangThai
        /// </summary>
        public void BulkInsertRFIDCards(System.Data.DataTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            string conn_string = GetWorkingConnection();
            using (var conn = new SqlConnection(conn_string))
            {
                conn.Open();
                using (var bulk = new SqlBulkCopy(conn))
                {
                    bulk.DestinationTableName = "RFIDCards";
                    // map columns
                    bulk.ColumnMappings.Add("CardUID", "CardUID");
                    bulk.ColumnMappings.Add("BienSo", "BienSo");
                    bulk.ColumnMappings.Add("LoaiVeId", "LoaiVeId");
                    bulk.ColumnMappings.Add("LoaiXeId", "LoaiXeId");
                    bulk.ColumnMappings.Add("NgayDangKy", "NgayDangKy");
                    bulk.ColumnMappings.Add("NgayHetHan", "NgayHetHan");
                    bulk.ColumnMappings.Add("TrangThai", "TrangThai");

                    bulk.WriteToServer(table);
                }
            }
        }

        public List<BangGia> LayBangGia()
        {
            var list = new List<BangGia>();
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                // New schema: pricing per KhungGio. Keep legacy columns for compatibility but avoid using them.
                string sql = "SELECT Id, LoaiXeId, LoaiVeId, GiaThang FROM dbo.BangGia";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
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

        // Update BangGia: only update GiaThang and TrangThai in the new model. Legacy per-slot prices are managed
        // via BangGiaKhungGio and should not be written here.
        public void UpdateBangGia(int id, decimal? giaThang = null, string trangThai = null)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "UPDATE dbo.BangGia SET GiaThang=@gt, TrangThai=@tt WHERE Id=@id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@gt", (object?)giaThang ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tt", (object?)trangThai ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void InsertLoaiXe(string tenLoai, string trangThai)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();

                string sql = @"INSERT INTO LoaiXe (TenLoai, TrangThai)
                       VALUES (@ten, @tt)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoai);
                    cmd.Parameters.AddWithValue("@tt", trangThai ?? "Active");

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateLoaiXe(int id, string tenLoai, string trangThai)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();

                string sql = @"UPDATE LoaiXe 
                       SET TenLoai=@ten, TrangThai=@tt 
                       WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@ten", tenLoai);
                    cmd.Parameters.AddWithValue("@tt", trangThai ?? "Active");

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteLoaiXe(int id)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "DELETE FROM LoaiXe WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        // -----------------------
        // LOAI THE CRUD (mapped to LoaiVe table in DB)
        // -----------------------

        public List<LoaiThe> GetLoaiThe()
        {
            var list = new List<LoaiThe>();

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "SELECT Id, TenLoai, TrangThai, Detail FROM LoaiVe";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new LoaiThe
                        {
                            Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                            TenLoaiThe = r["TenLoai"]?.ToString() ?? string.Empty,
                            GiaTien = 0m, // pricing moved to BangGia
                            TrangThai = r["TrangThai"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }

            return list;
        }

        public void InsertLoaiThe(string tenLoaiThe, decimal giaTien, string trangThai)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "INSERT INTO LoaiVe (TenLoai, TrangThai) VALUES (@ten, @trang)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiThe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateLoaiThe(int id, string tenLoaiThe, decimal giaTien, string trangThai)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "UPDATE LoaiVe SET TenLoai=@ten, TrangThai=@trang WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiThe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteLoaiThe(int id)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "DELETE FROM LoaiVe WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }


        // LOAI VE CRUD
        public List<LoaiVe> GetLoaiVe()
        {
            var list = new List<LoaiVe>();

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "SELECT Id, TenLoai, TrangThai, Detail, CoTheGiaHan FROM LoaiVe";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new LoaiVe
                        {
                            Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                            TenLoai = r["TenLoai"]?.ToString() ?? string.Empty,
                            TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                            Detail = r["Detail"]?.ToString() ?? string.Empty,
                            CoTheGiaHan = r["CoTheGiaHan"] != DBNull.Value && Convert.ToBoolean(r["CoTheGiaHan"])
                        });
                    }
                }
            }

            return list;
        }

        public void InsertLoaiVe(string tenLoaiVe, string trangThai, string detail = null)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "INSERT INTO LoaiVe (TenLoai, TrangThai, Detail) VALUES (@ten, @trang, @detail)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiVe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@detail", (object?)detail ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateLoaiVe(int id, string tenLoaiVe, string trangThai, string detail = null)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "UPDATE LoaiVe SET TenLoai=@ten, TrangThai=@trang, Detail=@detail WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiVe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@detail", (object?)detail ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteLoaiVe(int id)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "DELETE FROM LoaiVe WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // RFID CARD CRUD (strongly-typed RFIDCard model)
        public List<RFIDCard> GetRFIDCards()
        {
            var list = new List<RFIDCard>();

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = @"SELECT Id, CardUID, BienSo, 
                               CardName, LoaiVeId, LoaiXeId, TrangThai, 
                               NgayDangKy, NgayHetHan 
                               FROM RFIDCards";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new RFIDCard
                        {
                            Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                            UID = r["CardUID"]?.ToString() ?? string.Empty,
                            BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                            CardName = r["CardName"]?.ToString() ?? string.Empty,
                            LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                            LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                            TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                            NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue,
                            NgayHetHan = r["NgayHetHan"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["NgayHetHan"]) : null
                        });
                    }
                }
            }

            return list;
        }

        public void InsertRFIDCard(string uid, string bienSo, string cardName, int loaiVeId, int loaiXeId, string trangThai, DateTime ngayTao, DateTime? ngayHetHan)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = @"INSERT INTO RFIDCards (CardUID, BienSo, CardName, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy, NgayHetHan)
                               VALUES (@uid, @bien, @card, @loaive, @loaixe, @trang, @ngay, @ngayhh)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                    cmd.Parameters.AddWithValue("@bien", bienSo ?? string.Empty);
                    cmd.Parameters.AddWithValue("@card", (object?)cardName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@loaive", loaiVeId);
                    cmd.Parameters.AddWithValue("@loaixe", loaiXeId);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@ngay", ngayTao);
                    cmd.Parameters.AddWithValue("@ngayhh", (object?)ngayHetHan ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateRFIDCard(int id, string uid, string bienSo, string cardName, int loaiVeId, int loaiXeId, string trangThai, DateTime? ngayDangKy, DateTime? ngayHetHan)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = @"UPDATE RFIDCards SET CardUID=@uid, BienSo=@bien, CardName=@name, LoaiVeId=@loaive, LoaiXeId=@loaixe, TrangThai=@trang, NgayDangKy=@ngay, NgayHetHan=@ngayhh WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                    cmd.Parameters.AddWithValue("@bien", bienSo ?? string.Empty);
                    cmd.Parameters.AddWithValue("@name", (object?)cardName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@loaive", loaiVeId);
                    cmd.Parameters.AddWithValue("@loaixe", loaiXeId);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@ngay", (object?)ngayDangKy ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ngayhh", (object?)ngayHetHan ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteRFIDCard(int id)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "DELETE FROM RFIDCards WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Gia hạn thẻ RFID theo số tháng.
        /// Cập nhật NgayHetHan = DATEADD(MONTH, SoThang, CurrentNgayHetHan)
        /// Set TrangThai = 'Active'
        /// </summary>
        public void GiaHanRFIDCard(int id, int soThang)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                // 1. Update expiry and get the new date
                // CoTheGiaHan-based check: only renew cards whose ticket type allows renewal (CoTheGiaHan = 1)
                string sql = @"
                    UPDATE r 
                    SET r.NgayHetHan = DATEADD(MONTH, @months, CASE WHEN r.NgayHetHan < GETDATE() OR r.NgayHetHan IS NULL THEN GETDATE() ELSE r.NgayHetHan END),
                        r.TrangThai = 'Active'
                    OUTPUT INSERTED.NgayHetHan
                    FROM RFIDCards r
                    INNER JOIN LoaiVe lv ON r.LoaiVeId = lv.Id
                    WHERE r.Id = @id AND lv.CoTheGiaHan = 1";

                DateTime newExpiry;
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@months", soThang);
                    var result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value) return; 
                    newExpiry = (DateTime)result;
                }

                // 2. Log to GiaHanRFIDLog
                string logSql = @"
                    IF OBJECT_ID('dbo.GiaHanRFIDLog') IS NULL
                    BEGIN
                        CREATE TABLE dbo.GiaHanRFIDLog (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            CardId INT,
                            SoThang INT,
                            NgayGiaHan DATETIME DEFAULT GETDATE(),
                            NgayHetHanMoi DATETIME
                        )
                    END
                    INSERT INTO GiaHanRFIDLog (CardId, SoThang, NgayGiaHan, NgayHetHanMoi)
                    VALUES (@cardId, @soThang, GETDATE(), @newExpiry)";

                using (SqlCommand cmd = new SqlCommand(logSql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", id);
                    cmd.Parameters.AddWithValue("@soThang", soThang);
                    cmd.Parameters.AddWithValue("@newExpiry", newExpiry);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<GiaHanRFIDLog> GetGiaHanHistory(string searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var list = new List<GiaHanRFIDLog>();
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                
                // Ensure table exists
                string checkTableSql = @"
                    IF OBJECT_ID('dbo.GiaHanRFIDLog') IS NULL
                    BEGIN
                        CREATE TABLE dbo.GiaHanRFIDLog (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            CardId INT,
                            SoThang INT,
                            NgayGiaHan DATETIME DEFAULT GETDATE(),
                            NgayHetHanMoi DATETIME
                        )
                    END";
                using (SqlCommand checkCmd = new SqlCommand(checkTableSql, conn)) checkCmd.ExecuteNonQuery();

                string sql = @"
                    SELECT l.Id, l.CardId, l.SoThang, l.NgayGiaHan, l.NgayHetHanMoi,
                           c.CardUID, c.CardName, c.BienSo
                    FROM GiaHanRFIDLog l
                    JOIN RFIDCards c ON l.CardId = c.Id
                    WHERE (@searchTerm IS NULL OR c.CardUID LIKE @searchTerm OR c.BienSo LIKE @searchTerm OR CAST(l.CardId AS NVARCHAR) LIKE @searchTerm)
                      AND (@fromDate IS NULL OR l.NgayGiaHan >= @fromDate)
                      AND (@toDate IS NULL OR l.NgayGiaHan <= @toDate)
                    ORDER BY l.NgayGiaHan DESC";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@searchTerm", string.IsNullOrEmpty(searchTerm) ? DBNull.Value : $"%{searchTerm}%");
                    cmd.Parameters.AddWithValue("@fromDate", (object?)fromDate ?? DBNull.Value);
                    
                    // ToDate should include the whole day if only date is provided
                    if (toDate.HasValue && toDate.Value.TimeOfDay == TimeSpan.Zero)
                    {
                        toDate = toDate.Value.AddDays(1).AddSeconds(-1);
                    }
                    cmd.Parameters.AddWithValue("@toDate", (object?)toDate ?? DBNull.Value);

                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new GiaHanRFIDLog
                            {
                                Id = (int)r["Id"],
                                CardId = (int)r["CardId"],
                                SoThang = (int)r["SoThang"],
                                NgayGiaHan = (DateTime)r["NgayGiaHan"],
                                NgayHetHanMoi = (DateTime)r["NgayHetHanMoi"],
                                CardUID = r["CardUID"]?.ToString(),
                                CardName = r["CardName"]?.ToString(),
                                BienSo = r["BienSo"]?.ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        public bool IsRFIDUidExists(string uid)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "SELECT COUNT(1) FROM RFIDCards WHERE CardUID = @uid";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                    var v = cmd.ExecuteScalar();
                    return Convert.ToInt32(v) > 0;
                }
            }
        }

        public void InsertButtonPressLog(DateTime timestamp, byte? door, int? eventType, int? inOutState,
            string? cardNo, int? pin, string? rawData, string? action,
            byte? barrierResult, string? plateImagePath, string? fullImagePath,
            string? operatorName, string? sourceIp, string? notes)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = @"INSERT INTO dbo.ButtonPressLog
                    (Timestamp, Door, EventType, InOutState, CardNo, Pin, RawData, Action, BarrierResult, PlateImagePath, FullImagePath, Operator, SourceIp, Notes)
                    VALUES
                    (@ts, @door, @evt, @inout, @card, @pin, @raw, @action, @barrier, @plate, @full, @op, @srcip, @notes)";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ts", timestamp);
                    cmd.Parameters.AddWithValue("@door", (object?)door ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@evt", (object?)eventType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@inout", (object?)inOutState ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@card", (object?)cardNo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pin", (object?)pin ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@raw", (object?)rawData ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@action", (object?)action ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@barrier", (object?)barrierResult ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@plate", (object?)plateImagePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@full", (object?)fullImagePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@op", (object?)operatorName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@srcip", (object?)sourceIp ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Reconcile a recent manual open record: if there's a MANUAL_OPEN row for the same door
        /// with null or 0 BarrierResult within +/- secondsWindow seconds of rtTimestamp, update it.
        /// </summary>
        public void ReconcileManualOpen(DateTime rtTimestamp, byte door, byte barrierResult, int secondsWindow = 5, string appendNote = "")
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = @"UPDATE dbo.ButtonPressLog
                                SET BarrierResult = @barrier, Notes = COALESCE(Notes,'') + @append
                                WHERE Door = @door AND Action = 'MANUAL_OPEN' AND (BarrierResult IS NULL OR BarrierResult = 0)
                                  AND ABS(DATEDIFF(SECOND, Timestamp, @ts)) <= @window";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@barrier", (object?)barrierResult ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@append", appendNote ?? string.Empty);
                    cmd.Parameters.AddWithValue("@door", door);
                    cmd.Parameters.AddWithValue("@ts", rtTimestamp);
                    cmd.Parameters.AddWithValue("@window", secondsWindow);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Add an entry when a vehicle enters. Primary key for identification is uid (CardUID).
        // Parameters: uid (CardUID), bienSo (nullable), anhXe (nullable)
        public void ThemXe(int cardId, string bienSo, string anhXe)
        {
            if (cardId <= 0)
                throw new ArgumentException("CardId is invalid", nameof(cardId));

            string conn_string = GetWorkingConnection();

            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                // kiểm tra xe đang trong bãi theo CardId (ĐÚNG DB)
                string checkSql = "SELECT COUNT(1) FROM XeTrongBai WHERE CardId = @cardId AND ThoiGianRa IS NULL";

                using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@cardId", cardId);

                    int exists = Convert.ToInt32(checkCmd.ExecuteScalar());
                    if (exists > 0)
                        return; // tránh insert trùng
                }

                // INSERT theo đúng schema DB
                string sql = @"
            INSERT INTO XeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe)
            VALUES (@CardId, @BienSo, @Time, @AnhXe)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@CardId", cardId);
                    cmd.Parameters.AddWithValue("@BienSo",
                        string.IsNullOrEmpty(bienSo) ? (object)DBNull.Value : bienSo);

                    cmd.Parameters.AddWithValue("@Time", DateTime.Now);

                    cmd.Parameters.AddWithValue("@AnhXe",
                        string.IsNullOrEmpty(anhXe) ? (object)DBNull.Value : anhXe);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable LayXeTrongBai()
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = "SELECT * FROM XeTrongBai";

                SqlDataAdapter adapter = new SqlDataAdapter(sql, conn);
                DataTable table = new DataTable();

                adapter.Fill(table);

                return table;
            }
        }

        public void XoaXe(string bienSo)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = "DELETE FROM XeTrongBai WHERE BienSo = @bienSo";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@bienSo", bienSo ?? string.Empty);

                cmd.ExecuteNonQuery();
            }
        }

        // Delete active entries by CardId (preferred). Do NOT use CardUID in XeTrongBai queries.
        public void XoaXeByCardId(int cardId)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "DELETE FROM XeTrongBai WHERE CardId = @cardId";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cardId", cardId);
                cmd.ExecuteNonQuery();
            }
        }

        public bool IsXeTrongBaiByCardId(int cardId)
        {
            string conn_string = GetWorkingConnection();

            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = @"
            SELECT COUNT(1)
            FROM XeTrongBai
            WHERE CardId = @cardId
              AND ThoiGianRa IS NULL";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", cardId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public int GetXeTrongBaiCountByCardId(int cardId)
        {
            string conn_string = GetWorkingConnection();

            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = @"
            SELECT COUNT(1)
            FROM XeTrongBai
            WHERE CardId = @cardId
              AND ThoiGianRa IS NULL";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", cardId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // Get plate from active XeTrongBai by CardId
        public string GetBienSoFromXeTrongBaiByCardId(int cardId)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "SELECT BienSo FROM XeTrongBai WHERE CardId = @cardId AND ThoiGianRa IS NULL";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", cardId);
                    var v = cmd.ExecuteScalar();
                    return v?.ToString() ?? string.Empty;
                }
            }
        }

        // Return XeTrongBai record for a CardId where ThoiGianRa IS NULL. Returns null if not found.
        public (int Id, string BienSo, DateTime ThoiGianVao)? GetXeTrongBaiRecordByCardId(int cardId)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "SELECT TOP 1 Id, BienSo, ThoiGianVao FROM XeTrongBai WHERE CardId = @cardId AND ThoiGianRa IS NULL ORDER BY ThoiGianVao DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", cardId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            int id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0;
                            string bs = r["BienSo"]?.ToString() ?? string.Empty;
                            DateTime vao = r["ThoiGianVao"] != DBNull.Value ? Convert.ToDateTime(r["ThoiGianVao"]) : DateTime.MinValue;
                            return (id, bs, vao);
                        }
                    }
                }
            }

            return null;
        }

        private bool ColumnExists(SqlConnection conn, string tableName, string columnName)
        {
            using (var cmd = new SqlCommand("SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @col", conn))
            {
                cmd.Parameters.AddWithValue("@table", tableName);
                cmd.Parameters.AddWithValue("@col", columnName);
                var v = cmd.ExecuteScalar();
                return v != null && Convert.ToInt32(v) > 0;
            }
        }

        public void LuuLichSu(string bienSo, DateTime vao, DateTime ra, double tien, string anhXe, string cardUid = null)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                // Align with actual LichSuXe schema:
                // Columns: Id, CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai, AnhVao, AnhRa
                // We will look up CardId (if any) from RFIDCards by BienSo and insert into CardId.
                // The incoming anhXe parameter represents the exit snapshot, so we store it in AnhRa.

                int? cardId = null;
                try
                {
                    if (!string.IsNullOrEmpty(cardUid))
                    {
                        using (var lookup = new SqlCommand("SELECT Id FROM RFIDCards WHERE CardUID = @uid", conn))
                        {
                            lookup.Parameters.AddWithValue("@uid", cardUid);
                            var v = lookup.ExecuteScalar();
                            if (v != null && v != DBNull.Value)
                                cardId = Convert.ToInt32(v);
                        }
                    }
                    else
                    {
                        using (var lookup = new SqlCommand("SELECT Id FROM RFIDCards WHERE BienSo = @bs", conn))
                        {
                            lookup.Parameters.AddWithValue("@bs", bienSo ?? string.Empty);
                            var v = lookup.ExecuteScalar();
                            if (v != null && v != DBNull.Value)
                                cardId = Convert.ToInt32(v);
                        }
                    }
                }
                catch { /* swallow lookup errors to avoid failing history save */ }

                string sql = "INSERT INTO LichSuXe (CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, AnhRa) VALUES (@cardId, @bs, @vao, @ra, @tien, @anh)";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", (object?)cardId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@bs", string.IsNullOrEmpty(bienSo) ? (object?)DBNull.Value : bienSo);
                    cmd.Parameters.AddWithValue("@vao", vao);
                    cmd.Parameters.AddWithValue("@ra", ra);
                    cmd.Parameters.AddWithValue("@tien", tien);
                    cmd.Parameters.AddWithValue("@anh", (object?)anhXe ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<LichSuXe> LayLichSu()
        {
            List<LichSuXe> list = new List<LichSuXe>();

            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string query = @"
            SELECT 
                Id,
                CardId,
                BienSo,
                ThoiGianVao,
                ThoiGianRa,
                Tien,
                TrangThai,
                AnhVao,
                AnhRa
            FROM LichSuXe
            ORDER BY ThoiGianVao DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new LichSuXe
                    {
                        Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                        CardId = reader["CardId"] != DBNull.Value ? Convert.ToInt32(reader["CardId"]) : 0,

                        BienSo = reader["BienSo"]?.ToString(),

                        ThoiGianVao = reader["ThoiGianVao"] != DBNull.Value
                            ? Convert.ToDateTime(reader["ThoiGianVao"])
                            : DateTime.MinValue,

                        ThoiGianRa = reader["ThoiGianRa"] != DBNull.Value
                            ? Convert.ToDateTime(reader["ThoiGianRa"])
                            : (DateTime?)null,

                        Tien = reader["Tien"] != DBNull.Value
                            ? Convert.ToDouble(reader["Tien"])
                            : (double?)null,

                        TrangThai = reader["TrangThai"]?.ToString(),
                        AnhVao = reader["AnhVao"]?.ToString(),
                        AnhRa = reader["AnhRa"]?.ToString()
                    });
                }
            }

            return list;
        }

        public bool CheckCardExists(string uid)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = "SELECT COUNT(*) FROM RFIDCards WHERE CardUID = @uid";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@uid", uid);

                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public string GetBienSoFromUID(string uid)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string query = "SELECT BienSo FROM RFIDCards WHERE CardUID = @uid";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@uid", uid);

                var result = cmd.ExecuteScalar();

                return result?.ToString() ?? string.Empty;
            }
        }

        public bool AddRFIDCards(string uid, string bienSo, string loaiThe)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string insertQuery = "INSERT INTO RFIDCards(CardUID,BienSo,LoaiThe) VALUES(@uid,@bs,@lt)";

                SqlCommand cmd = new SqlCommand(insertQuery, conn);
                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.Parameters.AddWithValue("@bs", bienSo);
                cmd.Parameters.AddWithValue("@lt", loaiThe);

                try
                {
                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"Database error: {ex.Message}");
                    throw;
                }
            }
        }

        public List<RFIDCards> LayDanhSachRFIDCards()
        {
            var list = new List<RFIDCards>();
            string conn_string = GetWorkingConnection();

            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = @"
                    SELECT 
                        r.Id,
                        r.CardUID,
                        r.BienSo,
                        r.TrangThai,
                        lx.TenLoai AS LoaiXe,
                        lv.TenLoai AS LoaiVe
                    FROM RFIDCards r
                    LEFT JOIN LoaiXe lx ON r.LoaiXeId = lx.Id
                    LEFT JOIN LoaiVe lv ON r.LoaiVeId = lv.Id
                    ";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new RFIDCards
                        {
                            Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,

                            CardUID = reader["CardUID"]?.ToString(),
                            BienSo = reader["BienSo"]?.ToString(),
                            TrangThai = reader["TrangThai"]?.ToString(),

                            LoaiXe = reader["LoaiXe"]?.ToString(),
                            LoaiVe = reader["LoaiVe"]?.ToString()
                        });
                    }
                }
            }

            return list;
        }

        // Insert a generic app log record for audit/important events
        public void InsertAppLog(DateTime timestampUtc, string level, string eventType, string source, string userId, string plate, string details, string exception)
        {
            try
            {
                string conn_string = GetWorkingConnection();
                using (SqlConnection conn = new SqlConnection(conn_string))
                {
                    conn.Open();
                    string sql = @"IF OBJECT_ID('dbo.AppLogs') IS NULL
                                    BEGIN
                                        CREATE TABLE dbo.AppLogs (
                                            Id INT IDENTITY(1,1) PRIMARY KEY,
                                            TimestampUtc DATETIME2,
                                            [Level] NVARCHAR(50),
                                            EventType NVARCHAR(200),
                                            Source NVARCHAR(200),
                                            UserId NVARCHAR(200),
                                            Plate NVARCHAR(200),
                                            Details NVARCHAR(MAX),
                                            Exception NVARCHAR(MAX)
                                        )
                                    END
                                    INSERT INTO dbo.AppLogs (TimestampUtc, [Level], EventType, Source, UserId, Plate, Details, Exception)
                                    VALUES (@ts, @lvl, @evt, @src, @uid, @plate, @details, @ex)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ts", timestampUtc);
                        cmd.Parameters.AddWithValue("@lvl", level ?? string.Empty);
                        cmd.Parameters.AddWithValue("@evt", eventType ?? string.Empty);
                        cmd.Parameters.AddWithValue("@src", source ?? string.Empty);
                        cmd.Parameters.AddWithValue("@uid", userId ?? string.Empty);
                        cmd.Parameters.AddWithValue("@plate", plate ?? string.Empty);
                        cmd.Parameters.AddWithValue("@details", details ?? string.Empty);
                        cmd.Parameters.AddWithValue("@ex", exception ?? string.Empty);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
                // swallow DB logging errors
            }
        }

        // Read persisted app logs from the AppLogs table. Returns newest-first list.
        public System.Collections.Generic.List<QuanLyGiuXe.Services.LogEntry> GetAppLogs(DateTime? fromUtc = null, DateTime? toUtc = null, int max = 1000)
        {
            var list = new System.Collections.Generic.List<QuanLyGiuXe.Services.LogEntry>();
            try
            {
                string conn_string = GetWorkingConnection();
                using (SqlConnection conn = new SqlConnection(conn_string))
                {
                    conn.Open();
                    string sql = @"SELECT TimestampUtc, [Level], EventType, Source, UserId, Plate, Details, Exception
                                   FROM dbo.AppLogs
                                   WHERE (@from IS NULL OR TimestampUtc >= @from)
                                     AND (@to IS NULL OR TimestampUtc <= @to)
                                   ORDER BY TimestampUtc DESC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@from", (object?)fromUtc ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@to", (object?)toUtc ?? DBNull.Value);

                        using (var reader = cmd.ExecuteReader())
                        {
                            int count = 0;
                            while (reader.Read())
                            {
                                try
                                {
                                    var ts = reader.GetValue(0);
                                    DateTime timestampUtc = ts is DateTime dt ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : DateTime.UtcNow;
                                    var entry = new QuanLyGiuXe.Services.LogEntry
                                    {
                                        Timestamp = timestampUtc,
                                        Level = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                        EventType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                        Source = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                        UserId = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                        Plate = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                        Details = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                        Exception = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                                    };
                                    list.Add(entry);
                                }
                                catch { }

                                count++;
                                if (count >= max) break;
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }
        /// <summary>
        /// Executes a SELECT query and returns a DataTable.
        /// </summary>
        public System.Data.DataTable ExecuteQuery(string sql)
        {
            var dt = new System.Data.DataTable();
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (SqlDataAdapter adapter = new SqlDataAdapter(sql, conn))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        /// <summary>
        /// Executes INSERT, UPDATE, DELETE and returns the number of rows affected.
        /// </summary>
        public int ExecuteNonQuery(string sql)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    return cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
