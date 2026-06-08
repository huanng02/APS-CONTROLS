using System;
using System.Data.SqlClient;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Scratch
{
    public class CheckBackup
    {
        public static async System.Threading.Tasks.Task Main()
        {
            string connectionString = "Data Source=.;Initial Catalog=QuanLyGiuXe;Integrated Security=True"; // Adjust as needed
            try {
                using (var conn = new SqlConnection(connectionString)) {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("SELECT TOP 5 * FROM BackupHistory ORDER BY Id DESC", conn);
                    using (var reader = await cmd.ExecuteReaderAsync()) {
                        while (await reader.ReadAsync()) {
                            Console.WriteLine($"ID: {reader["Id"]}, File: {reader["FileName"]}, Path: {reader["FilePath"]}, Status: {reader["Status"]}");
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
