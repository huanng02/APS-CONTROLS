using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace QuanLyGiuXe.Services
{
    public class DashboardKpi
    {
        public int XeTrongBai { get; set; }
        public int LuotXeVao { get; set; }
        public double DoanhThu { get; set; }
        public int VeActive { get; set; }
    }

    public class HoatDongGhiNhan
    {
        public string BienSo { get; set; } = string.Empty;
        public DateTime ThoiGian { get; set; }
        public string HanhDong { get; set; } = string.Empty;
        public double GiaTien { get; set; }
        public string TheLoai { get; set; } = string.Empty;
    }

    public class DashboardService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public async System.Threading.Tasks.Task<DashboardKpi> GetKpiAsync(DateTime startDate, DateTime endDate)
        {
            var kpi = new DashboardKpi();
            string sql = @"
                SELECT 
                    (SELECT COUNT(*) FROM XeTrongBai) AS XeTrongBai,
                    (SELECT COUNT(*) FROM LichSuXe WHERE ThoiGianVao >= @Start AND ThoiGianVao <= @End) AS LuotXeVao,
                    (SELECT ISNULL(SUM(Tien), 0) FROM LichSuXe WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End) AS DoanhThu,
                    (SELECT COUNT(*) FROM RFIDCards WHERE TrangThai = 'Active' AND (NgayHetHan IS NULL OR NgayHetHan >= GETDATE())) AS VeActive;
            ";

            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startDate);
                        cmd.Parameters.AddWithValue("@End", endDate);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                kpi.XeTrongBai = reader["XeTrongBai"] != DBNull.Value ? Convert.ToInt32(reader["XeTrongBai"]) : 0;
                                kpi.LuotXeVao = reader["LuotXeVao"] != DBNull.Value ? Convert.ToInt32(reader["LuotXeVao"]) : 0;
                                kpi.DoanhThu = reader["DoanhThu"] != DBNull.Value ? Convert.ToDouble(reader["DoanhThu"]) : 0;
                                kpi.VeActive = reader["VeActive"] != DBNull.Value ? Convert.ToInt32(reader["VeActive"]) : 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardService", "GetKpiAsync", "Lỗi lấy KPI", ex);
            }

            return kpi;
        }

        public async System.Threading.Tasks.Task<DataTable> GetRevenueByDayAsync(DateTime startDate, DateTime endDate)
        {
            var dt = new DataTable();
            string sql = @"
                SELECT CAST(ThoiGianRa AS DATE) AS Ngay, SUM(Tien) AS DoanhThu
                FROM LichSuXe
                WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End AND Tien > 0
                GROUP BY CAST(ThoiGianRa AS DATE)
                ORDER BY Ngay;
            ";

            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startDate);
                        cmd.Parameters.AddWithValue("@End", endDate);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardService", "GetRevenueByDayAsync", "Lỗi lấy biểu đồ doanh thu", ex);
            }

            return dt;
        }

        public async System.Threading.Tasks.Task<DataTable> GetEntriesByHourAsync(DateTime startDate, DateTime endDate)
        {
            var dt = new DataTable();
            string sql = @"
                SELECT DATEPART(HOUR, ThoiGianVao) AS Gio, COUNT(*) AS SoLuot
                FROM LichSuXe
                WHERE ThoiGianVao >= @Start AND ThoiGianVao <= @End
                GROUP BY DATEPART(HOUR, ThoiGianVao)
                ORDER BY Gio;
            ";

            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startDate);
                        cmd.Parameters.AddWithValue("@End", endDate);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardService", "GetEntriesByHourAsync", "Lỗi lấy biểu đồ lượt xe theo giờ", ex);
            }

            return dt;
        }

        public List<HoatDongGhiNhan> GetRecentActivities()
        {
            var result = new List<HoatDongGhiNhan>();
            string sql = @"
                SELECT TOP 10 BienSo, ThoiGianVao, ThoiGianRa, Tien
                FROM LichSuXe
                ORDER BY Id DESC;
            ";

            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var hd = new HoatDongGhiNhan
                                {
                                    BienSo = reader["BienSo"]?.ToString() ?? "N/A",
                                    GiaTien = reader["Tien"] != DBNull.Value ? Convert.ToDouble(reader["Tien"]) : 0
                                };

                                if (reader["ThoiGianRa"] != DBNull.Value)
                                {
                                    hd.HanhDong = "RA";
                                    hd.ThoiGian = Convert.ToDateTime(reader["ThoiGianRa"]);
                                    hd.TheLoai = hd.GiaTien > 0 ? "Vé lượt" : "Vé tháng";
                                }
                                else if (reader["ThoiGianVao"] != DBNull.Value)
                                {
                                    hd.HanhDong = "VÀO";
                                    hd.ThoiGian = Convert.ToDateTime(reader["ThoiGianVao"]);
                                    hd.TheLoai = "Vào bãi";
                                }

                                result.Add(hd);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardService", "GetRecentActivities", "Lỗi lấy hoạt động gần đây", ex);
            }

            return result;
        }

        public async System.Threading.Tasks.Task<DataTable> GetTransactionsAsync(DateTime startDate, DateTime endDate)
        {
            var dt = new DataTable();
            string sql = @"
                SELECT 
                    ThoiGianVao AS [Giờ Vào],
                    ThoiGianRa AS [Giờ Ra],
                    BienSo AS [Biển Số],
                    Tien AS [Số Tiền],
                    TrangThai AS [Trạng Thái]
                FROM LichSuXe
                WHERE (ThoiGianVao >= @Start AND ThoiGianVao <= @End)
                   OR (ThoiGianRa >= @Start AND ThoiGianRa <= @End)
                ORDER BY Id DESC;
            ";

            try
            {
                using (var conn = new SqlConnection(_db.GetConnectionString()))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startDate);
                        cmd.Parameters.AddWithValue("@End", endDate);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardService", "GetTransactionsAsync", "Lỗi lấy dữ liệu giao dịch", ex);
            }

            return dt;
        }
    }
}
