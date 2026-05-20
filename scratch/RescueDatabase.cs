using System;
using System.Data.SqlClient;

namespace QuanLyGiuXe.Scratch
{
    public class RescueDatabase
    {
        public static async System.Threading.Tasks.Task Main()
        {
            // Try to connect to master to fix the target DB
            string masterConnString = "Data Source=.;Initial Catalog=master;Integrated Security=True;TrustServerCertificate=True;";
            string dbName = "BaiXe"; // Adjust if your DB name is different

            Console.WriteLine($"Attempting to rescue database {dbName}...");

            try
            {
                using (var conn = new SqlConnection(masterConnString))
                {
                    await conn.OpenAsync();
                    Console.WriteLine("Connected to master.");

                    // Force Multi User
                    string sql = $"ALTER DATABASE [{dbName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE;";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    Console.WriteLine("Successfully set database to MULTI_USER mode.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Rescue failed: " + ex.Message);
                Console.WriteLine("Please try running this in SQL Management Studio (SSMS):");
                Console.WriteLine($"ALTER DATABASE [{dbName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE;");
            }
        }
    }
}
