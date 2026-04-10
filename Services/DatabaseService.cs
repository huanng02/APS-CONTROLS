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

                string query = "SELECT BienSo, ThoiGianVao, ThoiGianRa, Tien FROM LichSuXe";

                SqlCommand cmd = new SqlCommand(query, conn);

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new LichSuXe
                    {
                        BienSo = reader["BienSo"].ToString(),
                        ThoiGianVao = Convert.ToDateTime(reader["ThoiGianVao"]),
                        ThoiGianRa = Convert.ToDateTime(reader["ThoiGianRa"]),
                        Tien = Convert.ToDouble(reader["Tien"])
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

        public bool AddRFIDCard(string uid, string bienSo, string loaiThe)
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
    }
}
