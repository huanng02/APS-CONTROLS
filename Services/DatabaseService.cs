using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class DatabaseService
    {
        string primaryConnection = "Server=.;Database=Baixe;Trusted_Connection=True;";
        string backupConnection = "Server=BACKUP_SERVER;Database=Baixe;Trusted_Connection=True;";

        private string GetWorkingConnection()
        {
            // Try primary connection first
            try
            {
                using (SqlConnection conn = new SqlConnection(primaryConnection))
                {
                    conn.Open();
                    conn.Close();
                    return primaryConnection;
                }
            }
            catch
            {
                Console.WriteLine("⚠️ Primary database connection failed, trying backup...");
            }

            // Try backup connection
            try
            {
                using (SqlConnection conn = new SqlConnection(backupConnection))
                {
                    conn.Open();
                    conn.Close();
                    Console.WriteLine("✅ Connected to backup database");
                    return backupConnection;
                }
            }
            catch
            {
                Console.WriteLine("❌ Both primary and backup database connections failed!");
                throw new Exception("Database connection failed. Both primary and backup servers are unavailable.");
            }
        }

        public void ThemXe(string bienSo, string anhXe, string uid)
        {
            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();

                string sql =
                "INSERT INTO XeTrongBai (cardUID, BienSo, ThoiGianVao, AnhXe) VALUES (@uid, @BienSo, @Time, @Anh)";

                SqlCommand cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.Parameters.AddWithValue("@BienSo", bienSo);
                cmd.Parameters.AddWithValue("@Time", DateTime.Now);
                cmd.Parameters.AddWithValue("@Anh", anhXe);


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
                cmd.Parameters.AddWithValue("@anh", anhXe);

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

                return result?.ToString() ?? "";
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

    }
}
