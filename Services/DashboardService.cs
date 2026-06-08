using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using QuanLyGiuXe.Services.OfflineCache;

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
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<DashboardKpi>(
                $"DASHBOARD_KPI_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}",
                async conn =>
                {
                    var kpi = new DashboardKpi();
                    string sql = @"
                        SELECT 
                            (SELECT COUNT(*) FROM XeTrongBai WHERE ThoiGianRa IS NULL) AS XeTrongBai,
                            (SELECT COUNT(*) FROM LichSuXe WHERE ThoiGianVao >= @Start AND ThoiGianVao <= @End) AS LuotXeVao,
                            (SELECT ISNULL(SUM(Tien), 0) FROM LichSuXe WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End) AS DoanhThu,
                            (SELECT COUNT(*) FROM RFIDCards WHERE TrangThai = 'Active' AND (NgayHetHan IS NULL OR NgayHetHan >= GETDATE())) AS VeActive;
                    ";

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
                    return kpi;
                }
            ) ?? new DashboardKpi();
        }

        public async System.Threading.Tasks.Task<DataTable> GetRevenueByDayAsync(DateTime startDate, DateTime endDate)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<DataTable>(
                $"DASHBOARD_REVENUE_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}",
                async conn =>
                {
                    var dt = new DataTable();
                    string sql = @"
                        SELECT CAST(ThoiGianRa AS DATE) AS Ngay, SUM(Tien) AS DoanhThu
                        FROM LichSuXe
                        WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End AND Tien > 0
                        GROUP BY CAST(ThoiGianRa AS DATE)
                        ORDER BY Ngay;
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startDate);
                        cmd.Parameters.AddWithValue("@End", endDate);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }
                    return dt;
                }
            ) ?? new DataTable();
        }

        public async System.Threading.Tasks.Task<DataTable> GetEntriesByHourAsync(DateTime startDate, DateTime endDate)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<DataTable>(
                $"DASHBOARD_ENTRIES_HOUR_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}",
                async conn =>
                {
                    var dt = new DataTable();
                    string sql = @"
                        SELECT DATEPART(HOUR, ThoiGianVao) AS Gio, COUNT(*) AS SoLuot
                        FROM LichSuXe
                        WHERE ThoiGianVao >= @Start AND ThoiGianVao <= @End
                        GROUP BY DATEPART(HOUR, ThoiGianVao)
                        ORDER BY Gio;
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startDate);
                        cmd.Parameters.AddWithValue("@End", endDate);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }
                    return dt;
                }
            ) ?? new DataTable();
        }

        public async System.Threading.Tasks.Task<List<HoatDongGhiNhan>> GetRecentActivitiesAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<HoatDongGhiNhan>>(
                "DASHBOARD_RECENT_ACTIVITIES",
                async conn =>
                {
                    var result = new List<HoatDongGhiNhan>();
                    string sql = @"
                        SELECT TOP 10 BienSo, ThoiGianVao, ThoiGianRa, Tien
                        FROM LichSuXe
                        ORDER BY Id DESC;
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
                    return result;
                }
            ) ?? new List<HoatDongGhiNhan>();
        }

        public async System.Threading.Tasks.Task<DataTable> GetTransactionsAsync(DateTime startDate, DateTime endDate)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<DataTable>(
                $"DASHBOARD_TRANSACTIONS_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}",
                async conn =>
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
 
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", startDate);
                        cmd.Parameters.AddWithValue("@End", endDate);
 
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }
                    return dt;
                }
            ) ?? new DataTable();
        }

        // Synchronous wrappers for legacy UI
        public DashboardKpi GetKpi(DateTime start, DateTime end) => Task.Run(() => GetKpiAsync(start, end)).GetAwaiter().GetResult();
        public List<HoatDongGhiNhan> GetRecentActivities() => Task.Run(() => GetRecentActivitiesAsync()).GetAwaiter().GetResult();

        public async System.Threading.Tasks.Task SyncDashboardCacheAsync()
        {
            if (!ConnectivityStateService.Instance.IsOnline) return;

            string[] options = { "Hôm nay", "Hôm qua", "7 ngày qua", "30 ngày qua", "Tháng này", "Tháng trước" };
            foreach (var option in options)
            {
                var (start, end) = GetDateRange(option);
                await GetKpiAsync(start, end);
                await GetRevenueByDayAsync(start, end);
                await GetEntriesByHourAsync(start, end);
                await GetTransactionsAsync(start, end);
            }
            await GetRecentActivitiesAsync();
        }

        private (DateTime start, DateTime end) GetDateRange(string option)
        {
            DateTime now = DateTime.Now;
            DateTime today = DateTime.Today;

            switch (option)
            {
                case "Hôm nay": return (today, now);
                case "Hôm qua": return (today.AddDays(-1), today.AddTicks(-1));
                case "7 ngày qua": return (today.AddDays(-6), now);
                case "30 ngày qua": return (today.AddDays(-29), now);
                case "Tháng này": return (new DateTime(today.Year, today.Month, 1), now);
                case "Tháng trước":
                    var firstDayLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    var lastDayLastMonth = new DateTime(today.Year, today.Month, 1).AddTicks(-1);
                    return (firstDayLastMonth, lastDayLastMonth);
                default: return (today, now);
            }
        }
    }
}
