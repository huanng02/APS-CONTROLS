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
        string connectionString =
        "Server=.;Database=Baixe;Trusted_Connection=True;";

        public void ThemXe(string bienSo, string anhXe, string uid)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
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
            using (SqlConnection conn = new SqlConnection(connectionString))
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
            using (SqlConnection conn = new SqlConnection(connectionString))
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
            using (SqlConnection conn = new SqlConnection(connectionString))
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

            using (SqlConnection conn = new SqlConnection(connectionString))
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
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string sql = "SELECT COUNT(*) FROM RFIDCards WHERE CardUID = @uid";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@uid", uid);

                return (int)cmd.ExecuteScalar() > 0;
            }
        }
        
    }
}
