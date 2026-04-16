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
    public class DatabaseService
    {
        private string primaryConnection = "Server=.;Database=BaiXe;Trusted_Connection=True;";
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

        // Expose working connection string for UI components
        public string GetConnectionString() => GetWorkingConnection();

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

        public List<BangGia> LayBangGia()
        {
            var list = new List<BangGia>();
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "SELECT Id, LoaiXeId, GiaTheoGio, GiaQuaDem, TrangThai FROM dbo.BangGia";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new BangGia
                        {
                            Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                            LoaiXeId = r["LoaiXeId"] != DBNull.Value ? (int?)Convert.ToInt32(r["LoaiXeId"]) : null,
                            GiaTheoGio = r["GiaTheoGio"] != DBNull.Value ? (double?)Convert.ToDouble(r["GiaTheoGio"]) : null,
                            GiaQuaDem = r["GiaQuaDem"] != DBNull.Value ? (double?)Convert.ToDouble(r["GiaQuaDem"]) : null,
                            TrangThai = r["TrangThai"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }

            return list;
        }

        public void UpdateBangGia(int id, double giaTheoGio, double giaQuaDem)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                string sql = "UPDATE dbo.BangGia SET GiaTheoGio=@g1, GiaQuaDem=@g2 WHERE Id=@id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@g1", giaTheoGio);
                    cmd.Parameters.AddWithValue("@g2", giaQuaDem);
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
                string sql = "SELECT Id, TenLoai, GiaTien, TrangThai FROM LoaiVe";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new LoaiThe
                        {
                            Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                            TenLoaiThe = r["TenLoai"]?.ToString() ?? string.Empty,
                            GiaTien = r["GiaTien"] != DBNull.Value ? Convert.ToDecimal(r["GiaTien"]) : 0m,
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
                string sql = "INSERT INTO LoaiVe (TenLoai, GiaTien, TrangThai) VALUES (@ten, @gia, @trang)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiThe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@gia", giaTien);
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
                string sql = "UPDATE LoaiVe SET TenLoai=@ten, GiaTien=@gia, TrangThai=@trang WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiThe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@gia", giaTien);
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
                string sql = "SELECT Id, TenLoai, GiaTien, TrangThai FROM LoaiVe";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new LoaiVe
                        {
                            Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                            TenLoai = r["TenLoai"]?.ToString() ?? string.Empty,
                            GiaTien = r["GiaTien"] != DBNull.Value ? Convert.ToDecimal(r["GiaTien"]) : 0m,
                            TrangThai = r["TrangThai"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }

            return list;
        }

        public void InsertLoaiVe(string tenLoaiVe, decimal giaTien, string trangThai)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "INSERT INTO LoaiVe (TenLoai, GiaTien, TrangThai) VALUES (@ten, @gia, @trang)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiVe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@gia", giaTien);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateLoaiVe(int id, string tenLoaiVe, decimal giaTien, string trangThai)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "UPDATE LoaiVe SET TenLoai=@ten, GiaTien=@gia, TrangThai=@trang WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ten", tenLoaiVe ?? string.Empty);
                    cmd.Parameters.AddWithValue("@gia", giaTien);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
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
                string sql = @"SELECT Id, CardUID, BienSo, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy FROM RFIDCards";

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
                            LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                            LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                            TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                            NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue
                        });
                    }
                }
            }

            return list;
        }

        public void InsertRFIDCard(string uid, string bienSo, int loaiVeId, int loaiXeId, string trangThai, DateTime ngayTao, DateTime? ngayHetHan)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = @"INSERT INTO RFIDCards (CardUID, BienSo, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy, NgayHetHan) VALUES (@uid, @bien, @loaive, @loaixe, @trang, @ngay, @ngayhh)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                    cmd.Parameters.AddWithValue("@bien", bienSo ?? string.Empty);
                    cmd.Parameters.AddWithValue("@loaive", loaiVeId);
                    cmd.Parameters.AddWithValue("@loaixe", loaiXeId);
                    cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                    cmd.Parameters.AddWithValue("@ngay", ngayTao);
                    cmd.Parameters.AddWithValue("@ngayhh", (object?)ngayHetHan ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateRFIDCard(int id, string uid, string bienSo, int loaiVeId, int loaiXeId, string trangThai, DateTime? ngayDangKy, DateTime? ngayHetHan)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = @"UPDATE RFIDCards SET CardUID=@uid, BienSo=@bien, LoaiVeId=@loaive, LoaiXeId=@loaixe, TrangThai=@trang, NgayDangKy=@ngay, NgayHetHan=@ngayhh WHERE Id=@id";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                    cmd.Parameters.AddWithValue("@bien", bienSo ?? string.Empty);
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

        public void ThemXe(string bienSo, string anhXe, string uid)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = "INSERT INTO XeTrongBai (cardUID, BienSo, ThoiGianVao, AnhXe) VALUES (@uid, @BienSo, @Time, @Anh)";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.Parameters.AddWithValue("@BienSo", bienSo);
                cmd.Parameters.AddWithValue("@Time", DateTime.Now);
                cmd.Parameters.AddWithValue("@Anh", anhXe ?? string.Empty);

                cmd.ExecuteNonQuery();
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
                cmd.Parameters.AddWithValue("@bienSo", bienSo);

                cmd.ExecuteNonQuery();
            }
        }

        public void LuuLichSu(string bienSo, DateTime vao, DateTime ra, double tien, string anhXe)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql = "INSERT INTO LichSuXe (BienSo, ThoiGianVao, ThoiGianRa, Tien, AnhXe) VALUES (@bs,@vao,@ra,@tien,@anh)";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@bs", bienSo);
                cmd.Parameters.AddWithValue("@vao", vao);
                cmd.Parameters.AddWithValue("@ra", ra);
                cmd.Parameters.AddWithValue("@tien", tien);
                cmd.Parameters.AddWithValue("@anh", anhXe ?? string.Empty);

                cmd.ExecuteNonQuery();
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
    }
}
