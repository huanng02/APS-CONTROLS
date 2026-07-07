using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;
using System.Data;
using System.Data.SqlClient;

namespace QuanLyGiuXe.Services
{
    public partial class DatabaseService
    {
        private string primaryConnection 
        {
            get 
            {
                return ConnectionManager.Instance.CurrentConnectionString;
            }
        }

        private static bool _appLogsTableChecked = false;

        private static List<LoaiXe>? _cachedLoaiXe;
        private static readonly object _loaiXeLock = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RFIDCard?> _cachedCardsByUid = new();

        public static void InvalidateLoaiXeCache()
        {
            lock (_loaiXeLock)
            {
                _cachedLoaiXe = null;
            }
        }

        public static void InvalidateRFIDCardCache()
        {
            _cachedCardsByUid.Clear();
        }

        /// <summary>
        /// Insert an audit/log entry into AppLogs. Best-effort: swallow errors and auto-provisions table/columns if missing.
        /// </summary>
        public void InsertAppLog(DateTime timestampUtc, string level, string eventType, string source, string userId, string plate, string details, string exception,
            string username = null, string action = null, string entityName = null, string entityId = null,
            string oldValues = null, string newValues = null, string ipAddress = null, string machineName = null,
            string deviceName = null, string sessionId = null, string correlationId = null,
            long? durationMs = null, int? retryCount = null, long? fileSize = null, string testName = null,
            bool? isRecovered = null, string additionalData = null)
        {
            Task.Run(async () =>
            {
                try
                {
                    await InsertAppLogAsync(timestampUtc, level, eventType, source, userId, plate, details, exception,
                        username, action, entityName, entityId, oldValues, newValues, ipAddress, machineName, deviceName, sessionId, correlationId,
                        durationMs, retryCount, fileSize, testName, isRecovered, additionalData);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ [DatabaseService] InsertAppLog Task failed: {ex.Message}");
                }
            });
        }

        public async Task<bool> InsertAppLogAsync(DateTime timestampUtc, string level, string eventType, string source, string userId, string plate, string details, string exception,
            string username = null, string action = null, string entityName = null, string entityId = null,
            string oldValues = null, string newValues = null, string ipAddress = null, string machineName = null,
            string deviceName = null, string sessionId = null, string correlationId = null,
            long? durationMs = null, int? retryCount = null, long? fileSize = null, string testName = null,
            bool? isRecovered = null, string additionalData = null)
        {
            try
            {
                return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                    "INSERT_LOG",
                    new { TimestampUtc = timestampUtc, EventType = eventType, Details = details },
                    async conn =>
                    {
                        if (!_appLogsTableChecked)
                        {
                            // table check logic (sync is fine inside this block)
                            _appLogsTableChecked = true;
                        }

                        using (SqlCommand cmd = new SqlCommand( @"INSERT INTO dbo.AppLogs 
                            (TimestampUtc, [Level], EventType, Source, UserId, Plate, Details, Exception, 
                             Username, [Action], EntityName, EntityId, OldValues, NewValues, IpAddress, MachineName, DeviceName, SessionId, CorrelationId,
                             DurationMs, RetryCount, FileSize, TestName, IsRecovered, AdditionalData)
                            VALUES 
                            (@ts, @lvl, @evt, @src, @uid, @plate, @details, @ex, 
                             @user, @action, @entity, @entityId, @old, @new, @ip, @mach, @dev, @sess, @corr,
                             @dur, @retry, @fsize, @tname, @recov, @data)", conn))
                        {
                            cmd.Parameters.AddWithValue("@ts", timestampUtc);
                            cmd.Parameters.AddWithValue("@lvl", level ?? string.Empty);
                            cmd.Parameters.AddWithValue("@evt", eventType ?? string.Empty);
                            cmd.Parameters.AddWithValue("@src", source ?? string.Empty);
                            cmd.Parameters.AddWithValue("@uid", userId ?? string.Empty);
                            cmd.Parameters.AddWithValue("@plate", plate ?? string.Empty);
                            cmd.Parameters.AddWithValue("@details", details ?? string.Empty);
                            cmd.Parameters.AddWithValue("@ex", exception ?? string.Empty);
                            cmd.Parameters.AddWithValue("@user", (object?)username ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@action", (object?)action ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@entity", (object?)entityName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@entityId", (object?)entityId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@old", (object?)oldValues ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@new", (object?)newValues ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@ip", (object?)ipAddress ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@mach", (object?)machineName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@dev", (object?)deviceName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@sess", (object?)sessionId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@corr", (object?)correlationId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@dur", (object?)durationMs ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@retry", (object?)retryCount ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@fsize", (object?)fileSize ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@tname", (object?)testName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@recov", (object?)isRecovered ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@data", (object?)additionalData ?? DBNull.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [DatabaseService] InsertAppLogAsync failed: {ex.Message}");
                return false;
            }
        }

        private string backupConnection = "Server=BACKUP_SERVER;Database=Baixe;Trusted_Connection=True;";



        private static string? _cachedWorkingConnection;
        private static DateTime _lastCheckTime = DateTime.MinValue;

        private string GetWorkingConnection()
        {
            return ConnectionManager.Instance.CurrentConnectionString;
        }
        /// <summary>
        /// Lookup an RFIDCard by plate (BienSo). Returns null if not found.
        /// </summary>
        public RFIDCard GetRFIDCardByBienSo(string bienSo)
        {
            return Task.Run(() => GetRFIDCardByBienSoAsync(bienSo)).GetAwaiter().GetResult();
        }

        public async Task<RFIDCard?> GetRFIDCardByBienSoAsync(string bienSo)
        {
            var card = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<RFIDCard>(
                $"RFID_BIENSO_{bienSo}",
                async conn =>
                {
                    string sql = @"
                        SELECT rc.Id, rc.CardUID, rc.BienSo, rc.CardName, rc.LoaiVeId, rc.LoaiXeId, rc.TrangThai, rc.NgayDangKy, rc.NgayHetHan, rc.GroupId, rc.EmployeeId,
                               e.FullName AS EmployeeName, e.EmployeeCode AS EmployeeCode
                        FROM RFIDCards rc
                        LEFT JOIN Employees e ON rc.EmployeeId = e.Id AND e.IsDeleted = 0
                        WHERE rc.BienSo = @bs";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@bs", bienSo ?? string.Empty);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return new RFIDCard
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    UID = r["CardUID"]?.ToString() ?? string.Empty,
                                    BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                                    CardName = r["CardName"]?.ToString() ?? string.Empty,
                                    LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                    LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                    TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                                    NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue,
                                    NgayHetHan = r["NgayHetHan"] != DBNull.Value ? Convert.ToDateTime(r["NgayHetHan"]) : (DateTime?)null,
                                    GroupId = r["GroupId"] != DBNull.Value ? Convert.ToInt32(r["GroupId"]) : (int?)null,
                                    EmployeeId = r["EmployeeId"] != DBNull.Value ? Convert.ToInt32(r["EmployeeId"]) : (int?)null,
                                    EmployeeName = r["EmployeeName"] == DBNull.Value ? null : r["EmployeeName"].ToString(),
                                    EmployeeCode = r["EmployeeCode"] == DBNull.Value ? null : r["EmployeeCode"].ToString()
                                };
                            }
                        }
                    }
                    return null;
                }
            );

            // 🟢 OFFLINE FALLBACK: Nếu cache bị miss khi mất mạng, ta lấy từ LIST_RFID_CARDS
            if (card == null && (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline))
            {
                var list = await QuanLyGiuXe.Services.OfflineCache.OfflineCacheService.Instance.GetCacheAsync<List<RFIDCard>>("LIST_RFID_CARDS");
                if (list != null)
                {
                    card = list.FirstOrDefault(c => string.Equals(c.BienSo, bienSo, StringComparison.OrdinalIgnoreCase));
                }
            }

            return card;
        }

        public RFIDCard GetRFIDCardByNormalizedBienSo(string bienSo)
        {
            return Task.Run(() => GetRFIDCardByNormalizedBienSoAsync(bienSo)).GetAwaiter().GetResult();
        }

        public async Task<RFIDCard?> GetRFIDCardByNormalizedBienSoAsync(string bienSo)
        {
            string normalized = new string((bienSo ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpper();
            if (string.IsNullOrEmpty(normalized)) return null;

            var card = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<RFIDCard>(
                $"RFID_NORMALIZED_BIENSO_{normalized}",
                async conn =>
                {
                    string sql = @"
                        SELECT rc.Id, rc.CardUID, rc.BienSo, rc.CardName, rc.LoaiVeId, rc.LoaiXeId, rc.TrangThai, rc.NgayDangKy, rc.NgayHetHan, rc.GroupId, rc.EmployeeId,
                               e.FullName AS EmployeeName, e.EmployeeCode AS EmployeeCode
                        FROM RFIDCards rc
                        LEFT JOIN Employees e ON rc.EmployeeId = e.Id AND e.IsDeleted = 0
                        WHERE UPPER(REPLACE(REPLACE(REPLACE(rc.BienSo, '-', ''), '.', ''), ' ', '')) = @bs";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@bs", normalized);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return new RFIDCard
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    UID = r["CardUID"]?.ToString() ?? string.Empty,
                                    BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                                    CardName = r["CardName"]?.ToString() ?? string.Empty,
                                    LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                    LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                    TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                                    NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue,
                                    NgayHetHan = r["NgayHetHan"] != DBNull.Value ? Convert.ToDateTime(r["NgayHetHan"]) : (DateTime?)null,
                                    GroupId = r["GroupId"] != DBNull.Value ? Convert.ToInt32(r["GroupId"]) : (int?)null,
                                    EmployeeId = r["EmployeeId"] != DBNull.Value ? Convert.ToInt32(r["EmployeeId"]) : (int?)null,
                                    EmployeeName = r["EmployeeName"] == DBNull.Value ? null : r["EmployeeName"].ToString(),
                                    EmployeeCode = r["EmployeeCode"] == DBNull.Value ? null : r["EmployeeCode"].ToString()
                                };
                            }
                        }
                    }
                    return null;
                }
            );

            // 🟢 OFFLINE FALLBACK
            if (card == null)
            {
                var list = await QuanLyGiuXe.Services.OfflineCache.OfflineCacheService.Instance.GetCacheAsync<List<RFIDCard>>("LIST_RFID_CARDS");
                if (list != null)
                {
                    card = list.FirstOrDefault(c => {
                        string cachedNorm = new string((c.BienSo ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpper();
                        return cachedNorm == normalized;
                    });
                }
            }

            return card;
        }

        public void UpdateXeRaById(int id, DateTime thoiGianRa)
        {
            Task.Run(() => UpdateXeRaByIdAsync(id, thoiGianRa)).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateXeRaByIdAsync(int id, DateTime thoiGianRa)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_XE_RA",
                new { Id = id, ThoiGianRa = thoiGianRa },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"UPDATE XeTrongBai SET ThoiGianRa = @ra WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@ra", thoiGianRa);
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public int GetTotalXeTrongBaiCount()
        {
            return Task.Run(() => GetTotalXeTrongBaiCountAsync()).GetAwaiter().GetResult();
        }

        public async Task<int> GetTotalXeTrongBaiCountAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<int>(
                "STATS_XE_TRONG_BAI",
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"SELECT COUNT(*) FROM XeTrongBai WHERE ThoiGianRa IS NULL", conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            );
        }

        public class TodayStats
        {
            public int LuotVao { get; set; }
            public int LuotRa { get; set; }
            public double DoanhThu { get; set; }
            public int XeTrongBai { get; set; }
            public int XeTonQuaNgay { get; set; }
        }

        public async Task<TodayStats> GetTodayStatsAsync(int? siteId)
        {
            string cacheKey = $"STATS_TODAY_{siteId}_{DateTime.Today:yyyyMMdd}";
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<TodayStats>(
                cacheKey,
                async conn =>
                {
                    var stats = new TodayStats();
                    DateTime start = DateTime.Today;
                    DateTime end = DateTime.Today.AddDays(1).AddTicks(-1);

                    string sql = @"
                        SELECT 
                            ((SELECT COUNT(*) FROM LichSuXe WHERE ThoiGianVao >= @Start AND ThoiGianVao <= @End AND (@SiteId IS NULL OR SiteId = @SiteId)) + 
                             (SELECT COUNT(*) FROM XeTrongBai WHERE ThoiGianVao >= @Start AND ThoiGianVao <= @End AND (@SiteId IS NULL OR SiteId = @SiteId))) AS LuotVao,
                            (SELECT COUNT(*) FROM LichSuXe WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End AND (@SiteId IS NULL OR SiteId = @SiteId)) AS LuotRa,
                            (SELECT ISNULL(SUM(Tien), 0) FROM LichSuXe WHERE ThoiGianRa >= @Start AND ThoiGianRa <= @End AND (@SiteId IS NULL OR SiteId = @SiteId)) AS DoanhThu,
                            (SELECT COUNT(*) FROM XeTrongBai WHERE ThoiGianRa IS NULL AND (@SiteId IS NULL OR SiteId = @SiteId)) AS XeTrongBai,
                            (SELECT COUNT(*) FROM XeTrongBai WHERE ThoiGianRa IS NULL AND ThoiGianVao < @Start AND (@SiteId IS NULL OR SiteId = @SiteId)) AS XeTonQuaNgay;
                    ";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Start", start);
                        cmd.Parameters.AddWithValue("@End", end);
                        cmd.Parameters.AddWithValue("@SiteId", (object?)siteId ?? DBNull.Value);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                stats.LuotVao = reader["LuotVao"] != DBNull.Value ? Convert.ToInt32(reader["LuotVao"]) : 0;
                                stats.LuotRa = reader["LuotRa"] != DBNull.Value ? Convert.ToInt32(reader["LuotRa"]) : 0;
                                stats.DoanhThu = reader["DoanhThu"] != DBNull.Value ? Convert.ToDouble(reader["DoanhThu"]) : 0.0;
                                stats.XeTrongBai = reader["XeTrongBai"] != DBNull.Value ? Convert.ToInt32(reader["XeTrongBai"]) : 0;
                                stats.XeTonQuaNgay = reader["XeTonQuaNgay"] != DBNull.Value ? Convert.ToInt32(reader["XeTonQuaNgay"]) : 0;
                            }
                        }
                    }
                    return stats;
                }
            ) ?? new TodayStats();
        }

        public async Task UpdateTodayStatsCacheOfflineAsync(int? siteId, bool isEntry, double revenueChange, bool isOvernightExit = false)
        {
            try
            {
                string cacheKey = $"STATS_TODAY_{siteId}_{DateTime.Today:yyyyMMdd}";
                var stats = await OfflineCacheService.Instance.GetCacheAsync<TodayStats>(cacheKey) ?? new TodayStats();

                if (isEntry)
                {
                    stats.LuotVao++;
                    stats.XeTrongBai++;
                }
                else
                {
                    stats.LuotRa++;
                    stats.XeTrongBai = Math.Max(0, stats.XeTrongBai - 1);
                    stats.DoanhThu += revenueChange;
                    if (isOvernightExit)
                    {
                        stats.XeTonQuaNgay = Math.Max(0, stats.XeTonQuaNgay - 1);
                    }
                }

                await OfflineCacheService.Instance.SaveCacheAsync(cacheKey, stats);

                // Also update the global stats cache (siteId = null)
                if (siteId != null)
                {
                    string globalCacheKey = $"STATS_TODAY_{null}_{DateTime.Today:yyyyMMdd}";
                    var globalStats = await OfflineCacheService.Instance.GetCacheAsync<TodayStats>(globalCacheKey) ?? new TodayStats();
                    if (isEntry)
                    {
                        globalStats.LuotVao++;
                        globalStats.XeTrongBai++;
                    }
                    else
                    {
                        globalStats.LuotRa++;
                        globalStats.XeTrongBai = Math.Max(0, globalStats.XeTrongBai - 1);
                        globalStats.DoanhThu += revenueChange;
                        if (isOvernightExit)
                        {
                            globalStats.XeTonQuaNgay = Math.Max(0, globalStats.XeTonQuaNgay - 1);
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync(globalCacheKey, globalStats);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OFFLINE_CACHE", "UpdateStatsOffline", "Failed to update offline stats cache", ex);
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
                        if (loaiVe.CoTheGiaHan) return 0.0; // Monthly/Renewable ticket

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

                    var (defs, ps) = QuanLyGiuXe.ViewModels.TimeSlotCalculator.MapFromDb(khungs, prices);
                    var calcResult = QuanLyGiuXe.ViewModels.TimeSlotCalculator.Calculate(checkIn, checkOut, defs, ps);
                    
                    return (double)calcResult.FinalPrice;
                }

                // Fallback if no BangGia configured
                var duration = checkOut - checkIn;
                double hours = Math.Ceiling(duration.TotalHours <= 0 ? 1 : duration.TotalHours);
                return defaultRate * hours;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("TinhTienError", "DatabaseService", $"Lỗi tính tiền (LoaiXe: {loaiXeId}, LoaiVe: {loaiVeId}): {ex.Message}", ex);
                // On any failure, fallback to simple rule to preserve compatibility
                var duration = checkOut - checkIn;
                double hours = Math.Ceiling(duration.TotalHours <= 0 ? 1 : duration.TotalHours);
                return 5000.0 * hours;
            }
        }

        // Return active KhungGio entries
        public List<QuanLyGiuXe.Models.KhungGio> GetKhungGio()
        {
            return Task.Run(() => GetKhungGioAsync()).GetAwaiter().GetResult();
        }

        public async Task<List<QuanLyGiuXe.Models.KhungGio>> GetKhungGioAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<QuanLyGiuXe.Models.KhungGio>>(
                "LOOKUP_KHUNGGIO",
                async conn =>
                {
                    var repo = new KhungGioRepository();
                    return repo.GetAll(); // Note: Repository internals should also be async eventually
                }
            ) ?? new List<QuanLyGiuXe.Models.KhungGio>();
        }

        // Return BangGiaKhungGio entries for a BangGia id
        public List<QuanLyGiuXe.Models.BangGiaKhungGio> GetBangGiaKhungGioByBangGiaId(int bangGiaId)
        {
            return Task.Run(() => GetBangGiaKhungGioByBangGiaIdAsync(bangGiaId)).GetAwaiter().GetResult();
        }

        public async Task<List<QuanLyGiuXe.Models.BangGiaKhungGio>> GetBangGiaKhungGioByBangGiaIdAsync(int bangGiaId)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<QuanLyGiuXe.Models.BangGiaKhungGio>>(
                $"LOOKUP_BANGGIA_KHUNGGIO_{bangGiaId}",
                async conn =>
                {
                    var repo = new BangGiaKhungGioRepository();
                    return repo.GetByBangGiaId(bangGiaId);
                }
            ) ?? new List<QuanLyGiuXe.Models.BangGiaKhungGio>();
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
                using (var cmd = new SqlCommand( @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table AND COLUMN_NAME = @col", sql))
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
            return Task.Run(() => GetRFIDCardByUidAsync(uid)).GetAwaiter().GetResult();
        }

        public async Task<RFIDCard?> GetRFIDCardByUidAsync(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            if (_cachedCardsByUid.TryGetValue(uid, out var cachedCard))
            {
                return cachedCard;
            }

            var card = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<RFIDCard>(
                $"RFID_UID_{uid}",
                async conn =>
                {
                    string sql = @"
                        SELECT rc.Id, rc.CardUID, rc.BienSo, rc.CardName, rc.LoaiVeId, rc.LoaiXeId, rc.TrangThai, rc.NgayDangKy, rc.NgayHetHan, rc.GroupId, rc.EmployeeId,
                               e.FullName AS EmployeeName, e.EmployeeCode AS EmployeeCode
                        FROM RFIDCards rc
                        LEFT JOIN Employees e ON rc.EmployeeId = e.Id AND e.IsDeleted = 0
                        WHERE rc.CardUID = @uid";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return new RFIDCard
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    UID = r["CardUID"]?.ToString() ?? string.Empty,
                                    BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                                    CardName = r["CardName"]?.ToString() ?? string.Empty,
                                    LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                    LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                    TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                                    NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue,
                                    NgayHetHan = r["NgayHetHan"] != DBNull.Value ? Convert.ToDateTime(r["NgayHetHan"]) : (DateTime?)null,
                                    GroupId = r["GroupId"] != DBNull.Value ? Convert.ToInt32(r["GroupId"]) : (int?)null,
                                    EmployeeId = r["EmployeeId"] != DBNull.Value ? Convert.ToInt32(r["EmployeeId"]) : (int?)null,
                                    EmployeeName = r["EmployeeName"] == DBNull.Value ? null : r["EmployeeName"].ToString(),
                                    EmployeeCode = r["EmployeeCode"] == DBNull.Value ? null : r["EmployeeCode"].ToString()
                                };
                            }
                        }
                    }
                    return null;
                }
            );

            // 🟢 OFFLINE FALLBACK: Nếu cache bị miss khi mất mạng, ta lấy từ LIST_RFID_CARDS
            if (card == null && (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline))
            {
                var list = await QuanLyGiuXe.Services.OfflineCache.OfflineCacheService.Instance.GetCacheAsync<List<RFIDCard>>("LIST_RFID_CARDS");
                if (list != null)
                {
                    card = list.FirstOrDefault(c => c.UID == uid);
                }
            }

            _cachedCardsByUid[uid] = card;
            return card;
        }

        public async Task<RFIDCard?> GetRFIDCardByIdAsync(int id)
        {
            var card = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<RFIDCard>(
                $"RFID_ID_{id}",
                async conn =>
                {
                    string sql = @"
                        SELECT rc.Id, rc.CardUID, rc.BienSo, rc.CardName, rc.LoaiVeId, rc.LoaiXeId, rc.TrangThai, rc.NgayDangKy, rc.NgayHetHan, rc.GroupId, rc.EmployeeId,
                               e.FullName AS EmployeeName, e.EmployeeCode AS EmployeeCode
                        FROM RFIDCards rc
                        LEFT JOIN Employees e ON rc.EmployeeId = e.Id AND e.IsDeleted = 0
                        WHERE rc.Id = @id";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return new RFIDCard
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    UID = r["CardUID"]?.ToString() ?? string.Empty,
                                    BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                                    CardName = r["CardName"]?.ToString() ?? string.Empty,
                                    LoaiVeId = r["LoaiVeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiVeId"]) : 0,
                                    LoaiXeId = r["LoaiXeId"] != DBNull.Value ? Convert.ToInt32(r["LoaiXeId"]) : 0,
                                    TrangThai = r["TrangThai"]?.ToString() ?? string.Empty,
                                    NgayTao = r["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(r["NgayDangKy"]) : DateTime.MinValue,
                                    NgayHetHan = r["NgayHetHan"] != DBNull.Value ? Convert.ToDateTime(r["NgayHetHan"]) : (DateTime?)null,
                                    GroupId = r["GroupId"] != DBNull.Value ? Convert.ToInt32(r["GroupId"]) : (int?)null,
                                    EmployeeId = r["EmployeeId"] != DBNull.Value ? Convert.ToInt32(r["EmployeeId"]) : (int?)null,
                                    EmployeeName = r["EmployeeName"] == DBNull.Value ? null : r["EmployeeName"].ToString(),
                                    EmployeeCode = r["EmployeeCode"] == DBNull.Value ? null : r["EmployeeCode"].ToString()
                                };
                            }
                        }
                    }
                    return null;
                }
            );

            // 🟢 OFFLINE FALLBACK: Nếu cache bị miss khi mất mạng, ta lấy từ LIST_RFID_CARDS
            if (card == null && (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline))
            {
                var list = await QuanLyGiuXe.Services.OfflineCache.OfflineCacheService.Instance.GetCacheAsync<List<RFIDCard>>("LIST_RFID_CARDS");
                if (list != null)
                {
                    card = list.FirstOrDefault(c => c.Id == id);
                }
            }

            return card;
        }

        /// <summary>
        /// Get entry time for a vehicle currently in the lot by its plate.
        /// Returns null if not found.
        /// </summary>
        public DateTime? GetXeVaoTimeByBienSo(string bienSo)
        {
            return Task.Run(() => GetXeVaoTimeByBienSoAsync(bienSo)).GetAwaiter().GetResult();
        }

        public async Task<DateTime?> GetXeVaoTimeByBienSoAsync(string bienSo)
        {
            if (!string.IsNullOrEmpty(bienSo))
            {
                bienSo = new string(bienSo.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
            }
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<DateTime?>(
                "GET_XE_VAO_TIME",
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"SELECT ThoiGianVao FROM XeTrongBai WHERE BienSo = @bs AND ThoiGianRa IS NULL", conn))
                    {
                        cmd.Parameters.AddWithValue("@bs", bienSo ?? string.Empty);
                        var v = await cmd.ExecuteScalarAsync();
                        if (v != null && v != DBNull.Value)
                        {
                            return Convert.ToDateTime(v);
                        }
                    }
                    return null;
                }
            );
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
            return Task.Run(() => GetLoaiXeAsync()).GetAwaiter().GetResult();
        }

        public async Task<List<LoaiXe>> GetLoaiXeAsync()
        {
            lock (_loaiXeLock)
            {
                if (_cachedLoaiXe != null) return _cachedLoaiXe;
            }

            var list = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<LoaiXe>>(
                "LIST_LOAI_XE",
                async conn =>
                {
                    var list = new List<LoaiXe>();
                    using (SqlCommand cmd = new SqlCommand( @"SELECT Id, TenLoai, TrangThai FROM LoaiXe", conn))
                    using (SqlDataReader r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new LoaiXe
                            {
                                Id = (int)r["Id"],
                                TenLoai = r["TenLoai"].ToString(),
                                TrangThai = r["TrangThai"].ToString()
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<LoaiXe>();

            lock (_loaiXeLock)
            {
                _cachedLoaiXe = list;
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
            InvalidateRFIDCardCache();
        }

        public List<BangGia> LayBangGia()
        {
            return Task.Run(() => LayBangGiaAsync()).GetAwaiter().GetResult();
        }

        public async Task<List<BangGia>> LayBangGiaAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<BangGia>>(
                "LOOKUP_BANGGIA",
                async conn =>
                {
                    var list = new List<BangGia>();
                    using (SqlCommand cmd = new SqlCommand( @"SELECT Id, LoaiXeId, LoaiVeId, GiaThang, TrangThai FROM dbo.BangGia", conn))
                    using (SqlDataReader r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
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
                    return list;
                }
            ) ?? new List<BangGia>();
        }

        // Update BangGia: only update GiaThang and TrangThai in the new model. Legacy per-slot prices are managed
        // via BangGiaKhungGio and should not be written here.
        public void UpdateBangGia(int id, decimal? giaThang = null, string trangThai = null)
        {
            try
            {
                string conn_string = GetWorkingConnection();
                using (SqlConnection conn = new SqlConnection(conn_string))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand( @"UPDATE dbo.BangGia SET GiaThang=@gt, TrangThai=@tt WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@gt", (object?)giaThang ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@tt", (object?)trangThai ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                // audit log
                try { LoggingService.Instance.LogCrud("UPDATE_BANGGIA", "BangGia", id.ToString(), null, new { GiaThang = giaThang, TrangThai = trangThai }, "DatabaseService"); } catch { }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("UpdateError", "DatabaseService.BangGia", $"Lỗi cập nhật bảng giá (Id: {id}): {ex.Message}", ex);
                throw;
            }
        }

        public void InsertLoaiXe(string tenLoai, string trangThai)
        {
            Task.Run(() => InsertLoaiXeAsync(tenLoai, trangThai)).GetAwaiter().GetResult();
        }

        public async Task<bool> InsertLoaiXeAsync(string tenLoai, string trangThai)
        {
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "CREATE_LOAIXE",
                new { TenLoai = tenLoai, TrangThai = trangThai },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"INSERT INTO LoaiXe (TenLoai, TrangThai) VALUES (@ten, @tt)", conn))
                    {
                        cmd.Parameters.AddWithValue("@ten", tenLoai);
                        cmd.Parameters.AddWithValue("@tt", trangThai ?? "Active");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateLoaiXeCache();
            return success;
        }

        public void UpdateLoaiXe(int id, string tenLoai, string trangThai)
        {
            Task.Run(() => UpdateLoaiXeAsync(id, tenLoai, trangThai)).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateLoaiXeAsync(int id, string tenLoai, string trangThai)
        {
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_LOAIXE",
                new { Id = id, TenLoai = tenLoai, TrangThai = trangThai },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"UPDATE LoaiXe SET TenLoai=@ten, TrangThai=@tt WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@ten", tenLoai);
                        cmd.Parameters.AddWithValue("@tt", trangThai ?? "Active");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateLoaiXeCache();
            return success;
        }

        public void DeleteLoaiXe(int id)
        {
            Task.Run(() => DeleteLoaiXeAsync(id)).GetAwaiter().GetResult();
        }

        public async Task<bool> DeleteLoaiXeAsync(int id)
        {
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_LOAIXE",
                new { Id = id },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"DELETE FROM LoaiXe WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateLoaiXeCache();
            return success;
        }
        // -----------------------
        // LOAI THE CRUD (mapped to LoaiVe table in DB)
        // -----------------------

        public List<LoaiThe> GetLoaiThe()
        {
            return Task.Run(() => GetLoaiTheAsync()).GetAwaiter().GetResult();
        }

        public async Task<List<LoaiThe>> GetLoaiTheAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<LoaiThe>>(
                "LIST_LOAI_VE",
                async conn =>
                {
                    var list = new List<LoaiThe>();
                    using (SqlCommand cmd = new SqlCommand( @"SELECT Id, TenLoai, TrangThai, Detail FROM LoaiVe", conn))
                    using (SqlDataReader r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
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
                    return list;
                }
            ) ?? new List<LoaiThe>();
        }

        public void InsertLoaiThe(string tenLoaiThe, decimal giaTien, string trangThai)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand( @"INSERT INTO LoaiVe (TenLoai, TrangThai) VALUES (@ten, @trang)", conn))
                    {
                        cmd.Parameters.AddWithValue("@ten", tenLoaiThe ?? string.Empty);
                        cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoggingService.Instance.LogInfo("Insert", "DatabaseService.LoaiThe", $"Thêm loại thẻ thành công (Tên: {tenLoaiThe})");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("InsertError", "DatabaseService.LoaiThe", $"Lỗi thêm loại thẻ: {ex.Message}", ex);
                throw;
            }
        }

        public void UpdateLoaiThe(int id, string tenLoaiThe, decimal giaTien, string trangThai)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand( @"UPDATE LoaiVe SET TenLoai=@ten, TrangThai=@trang WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@ten", tenLoaiThe ?? string.Empty);
                        cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoggingService.Instance.LogInfo("Update", "DatabaseService.LoaiThe", $"Cập nhật loại thẻ thành công (Id: {id}, Tên: {tenLoaiThe})");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("UpdateError", "DatabaseService.LoaiThe", $"Lỗi cập nhật loại thẻ (Id: {id}): {ex.Message}", ex);
                throw;
            }
        }

        public void DeleteLoaiThe(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand( @"DELETE FROM LoaiVe WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoggingService.Instance.LogInfo("Delete", "DatabaseService.LoaiThe", $"Xóa loại thẻ thành công (Id: {id})");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeleteError", "DatabaseService.LoaiThe", $"Lỗi xóa loại thẻ (Id: {id}): {ex.Message}", ex);
                throw;
            }
        }


        // LOAI VE CRUD
        public List<LoaiVe> GetLoaiVe()
        {
            return new LoaiVeRepository().GetAll();
        }

        public void InsertLoaiVe(string tenLoaiVe, string trangThai, string detail = null)
        {
            Task.Run(() => InsertLoaiVeAsync(tenLoaiVe, trangThai, detail)).GetAwaiter().GetResult();
        }

        public async Task<bool> InsertLoaiVeAsync(string tenLoaiVe, string trangThai, string detail = null)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "CREATE_LOAIVE",
                new { TenLoai = tenLoaiVe, TrangThai = trangThai, Detail = detail },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"INSERT INTO LoaiVe (TenLoai, TrangThai, Detail) VALUES (@ten, @trang, @detail)", conn))
                    {
                        cmd.Parameters.AddWithValue("@ten", tenLoaiVe ?? string.Empty);
                        cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@detail", (object?)detail ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public void UpdateLoaiVe(int id, string tenLoaiVe, string trangThai, string detail = null)
        {
            Task.Run(() => UpdateLoaiVeAsync(id, tenLoaiVe, trangThai, detail)).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateLoaiVeAsync(int id, string tenLoaiVe, string trangThai, string detail = null)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_LOAIVE",
                new { Id = id, TenLoai = tenLoaiVe, TrangThai = trangThai, Detail = detail },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"UPDATE LoaiVe SET TenLoai=@ten, TrangThai=@trang, Detail=@detail WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@ten", tenLoaiVe ?? string.Empty);
                        cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@detail", (object?)detail ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public void DeleteLoaiVe(int id)
        {
            Task.Run(() => DeleteLoaiVeAsync(id)).GetAwaiter().GetResult();
        }

        public async Task<bool> DeleteLoaiVeAsync(int id)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_LOAIVE",
                new { Id = id },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"DELETE FROM LoaiVe WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        // RFID CARD CRUD (strongly-typed RFIDCard model)
        public List<RFIDCard> GetRFIDCards()
        {
            return Task.Run(() => GetRFIDCardsAsync()).GetAwaiter().GetResult();
        }

        public async Task<List<RFIDCard>> GetRFIDCardsAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<RFIDCard>>(
                "LIST_RFID_CARDS",
                async conn =>
                {
                    var list = new List<RFIDCard>();
                    string sql = @"
                        SELECT rc.Id, rc.CardUID, rc.BienSo, rc.CardName, rc.LoaiVeId, rc.LoaiXeId, rc.TrangThai, rc.NgayDangKy, rc.NgayHetHan, rc.GroupId, rc.EmployeeId,
                               e.FullName AS EmployeeName, e.EmployeeCode AS EmployeeCode
                        FROM RFIDCards rc
                        LEFT JOIN Employees e ON rc.EmployeeId = e.Id AND e.IsDeleted = 0";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
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
                                NgayHetHan = r["NgayHetHan"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(r["NgayHetHan"]) : null,
                                GroupId = r["GroupId"] != DBNull.Value ? Convert.ToInt32(r["GroupId"]) : (int?)null,
                                EmployeeId = r["EmployeeId"] != DBNull.Value ? Convert.ToInt32(r["EmployeeId"]) : (int?)null,
                                EmployeeName = r["EmployeeName"] == DBNull.Value ? null : r["EmployeeName"].ToString(),
                                EmployeeCode = r["EmployeeCode"] == DBNull.Value ? null : r["EmployeeCode"].ToString()
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<RFIDCard>();
        }

        public void InsertRFIDCard(string uid, string bienSo, string cardName, int loaiVeId, int loaiXeId, string trangThai, DateTime ngayTao, DateTime? ngayHetHan, int? employeeId = null)
        {
            Task.Run(() => InsertRFIDCardAsync(uid, bienSo, cardName, loaiVeId, loaiXeId, trangThai, ngayTao, ngayHetHan, employeeId)).GetAwaiter().GetResult();
        }

        public async Task<bool> InsertRFIDCardAsync(string uid, string bienSo, string cardName, int loaiVeId, int loaiXeId, string trangThai, DateTime ngayTao, DateTime? ngayHetHan, int? employeeId = null)
        {
            string normalizedUid = RFIDService.ChuanHoaUID(uid);
            if (await CheckCardActiveExistsAsync(normalizedUid))
            {
                throw new InvalidOperationException("Thẻ này đang hoạt động trong hệ thống!");
            }

            bool exists = await CheckCardExistsAsync(normalizedUid);
            if (exists)
            {
                int cardId = await GetCardIdFromUidAsync(normalizedUid);
                if (cardId > 0)
                {
                    await XoaXeByCardIdAsync(cardId);
                }
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "CREATE_RFID_CARD",
                new { UID = normalizedUid, BienSo = bienSo, CardName = cardName, LoaiVeId = loaiVeId, LoaiXeId = loaiXeId, TrangThai = trangThai, NgayTao = ngayTao, NgayHetHan = ngayHetHan, EmployeeId = employeeId },
                async conn =>
                {
                    string sql = exists
                        ? @"UPDATE RFIDCards SET BienSo=@bien, CardName=@card, LoaiVeId=@loaive, LoaiXeId=@loaixe, TrangThai=@trang, NgayDangKy=@ngay, NgayHetHan=@ngayhh, EmployeeId=@empid WHERE CardUID=@uid"
                        : @"INSERT INTO RFIDCards (CardUID, BienSo, CardName, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy, NgayHetHan, EmployeeId)
                                   VALUES (@uid, @bien, @card, @loaive, @loaixe, @trang, @ngay, @ngayhh, @empid)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", normalizedUid);
                        cmd.Parameters.AddWithValue("@bien", bienSo ?? string.Empty);
                        cmd.Parameters.AddWithValue("@card", (object?)cardName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@loaive", loaiVeId);
                        cmd.Parameters.AddWithValue("@loaixe", loaiXeId);
                        cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@ngay", ngayTao);
                        cmd.Parameters.AddWithValue("@ngayhh", (object?)ngayHetHan ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@empid", (object?)employeeId ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateRFIDCardCache();
            return success;
        }

        public void UpdateRFIDCard(int id, string uid, string bienSo, string cardName, int loaiVeId, int loaiXeId, string trangThai, DateTime? ngayDangKy, DateTime? ngayHetHan, int? employeeId = null)
        {
            Task.Run(() => UpdateRFIDCardAsync(id, uid, bienSo, cardName, loaiVeId, loaiXeId, trangThai, ngayDangKy, ngayHetHan, employeeId)).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateRFIDCardAsync(int id, string uid, string bienSo, string cardName, int loaiVeId, int loaiXeId, string trangThai, DateTime? ngayDangKy, DateTime? ngayHetHan, int? employeeId = null)
        {
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "UPDATE_RFID_CARD",
                new { Id = id, UID = uid, BienSo = bienSo, CardName = cardName, LoaiVeId = loaiVeId, LoaiXeId = loaiXeId, TrangThai = trangThai, NgayDangKy = ngayDangKy, NgayHetHan = ngayHetHan, EmployeeId = employeeId },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"UPDATE RFIDCards SET CardUID=@uid, BienSo=@bien, CardName=@name, LoaiVeId=@loaive, LoaiXeId=@loaixe, TrangThai=@trang, NgayDangKy=@ngay, NgayHetHan=@ngayhh, EmployeeId=@empid WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                        cmd.Parameters.AddWithValue("@bien", bienSo ?? string.Empty);
                        cmd.Parameters.AddWithValue("@name", (object?)cardName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@loaive", loaiVeId);
                        cmd.Parameters.AddWithValue("@loaixe", loaiXeId);
                        cmd.Parameters.AddWithValue("@trang", trangThai ?? string.Empty);
                        cmd.Parameters.AddWithValue("@ngay", (object?)ngayDangKy ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ngayhh", (object?)ngayHetHan ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@empid", (object?)employeeId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateRFIDCardCache();
            return success;
        }

        public void DeleteRFIDCard(int id)
        {
            Task.Run(() => DeleteRFIDCardAsync(id)).GetAwaiter().GetResult();
        }

        public async Task<bool> DeleteRFIDCardAsync(int id)
        {
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_RFID_CARD",
                new { Id = id },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"DELETE FROM RFIDCards WHERE Id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateRFIDCardCache();
            return success;
        }

        /// <summary>
        /// Gia hạn thẻ RFID theo số tháng.
        /// Cập nhật NgayHetHan = DATEADD(MONTH, SoThang, CurrentNgayHetHan)
        /// Set TrangThai = 'Active'
        /// </summary>
        public void GiaHanRFIDCard(int id, int soThang)
        {
            Task.Run(() => GiaHanRFIDCardAsync(id, soThang)).GetAwaiter().GetResult();
        }

        public async Task<bool> GiaHanRFIDCardAsync(int id, int soThang)
        {
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "RENEW_RFID_CARD",
                new { Id = id, Months = soThang },
                async conn =>
                {
                    DateTime newExpiry;
                    using (SqlCommand cmd = new SqlCommand( @"
                        UPDATE r 
                        SET r.NgayHetHan = DATEADD(MONTH, @months, CASE WHEN r.NgayHetHan < GETDATE() OR r.NgayHetHan IS NULL THEN GETDATE() ELSE r.NgayHetHan END),
                            r.TrangThai = 'Active'
                        OUTPUT INSERTED.NgayHetHan
                        FROM RFIDCards r
                        INNER JOIN LoaiVe lv ON r.LoaiVeId = lv.Id
                        WHERE r.Id = @id AND lv.CoTheGiaHan = 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@months", soThang);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result == null || result == DBNull.Value) return;
                        newExpiry = (DateTime)result;
                    }
                    using (SqlCommand cmd = new SqlCommand( @"
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
                        VALUES (@cardId, @soThang, GETDATE(), @newExpiry)", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", id);
                        cmd.Parameters.AddWithValue("@soThang", soThang);
                        cmd.Parameters.AddWithValue("@newExpiry", newExpiry);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            if (success) InvalidateRFIDCardCache();
            return success;
        }

        public List<GiaHanRFIDLog> GetGiaHanHistory(string searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var list = new List<GiaHanRFIDLog>();
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                
                // Ensure table exists
                using (SqlCommand checkCmd = new SqlCommand( @"
                    IF OBJECT_ID('dbo.GiaHanRFIDLog') IS NULL
                    BEGIN
                        CREATE TABLE dbo.GiaHanRFIDLog (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            CardId INT,
                            SoThang INT,
                            NgayGiaHan DATETIME DEFAULT GETDATE(),
                            NgayHetHanMoi DATETIME
                        )
                    END", conn)) checkCmd.ExecuteNonQuery();

                using (SqlCommand cmd = new SqlCommand( @"
                    SELECT l.Id, l.CardId, l.SoThang, l.NgayGiaHan, l.NgayHetHanMoi,
                           c.CardUID, c.CardName, c.BienSo
                    FROM GiaHanRFIDLog l
                    JOIN RFIDCards c ON l.CardId = c.Id
                    WHERE (@searchTerm IS NULL OR c.CardUID LIKE @searchTerm OR c.BienSo LIKE @searchTerm OR CAST(l.CardId AS NVARCHAR) LIKE @searchTerm)
                      AND (@fromDate IS NULL OR l.NgayGiaHan >= @fromDate)
                      AND (@toDate IS NULL OR l.NgayGiaHan <= @toDate)
                    ORDER BY l.NgayGiaHan DESC", conn))
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
                using (SqlCommand cmd = new SqlCommand( @"SELECT COUNT(1) FROM RFIDCards WHERE CardUID = @uid", conn))
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
            _ = InsertButtonPressLogAsync(timestamp, door, eventType, inOutState, cardNo, pin, rawData, action, barrierResult, plateImagePath, fullImagePath, operatorName, sourceIp, notes);
        }

        public async Task<bool> InsertButtonPressLogAsync(DateTime timestamp, byte? door, int? eventType, int? inOutState,
            string? cardNo, int? pin, string? rawData, string? action,
            byte? barrierResult, string? plateImagePath, string? fullImagePath,
            string? operatorName, string? sourceIp, string? notes)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "BUTTON_PRESS",
                new { Timestamp = timestamp, Action = action, Door = door },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"INSERT INTO dbo.ButtonPressLog
                        (Timestamp, Door, EventType, InOutState, CardNo, Pin, RawData, Action, BarrierResult, PlateImagePath, FullImagePath, Operator, SourceIp, Notes)
                        VALUES
                        (@ts, @door, @evt, @inout, @card, @pin, @raw, @action, @barrier, @plate, @full, @op, @srcip, @notes)", conn))
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
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        /// <summary>
        /// Reconcile a recent manual open record: if there's a MANUAL_OPEN row for the same door
        /// with null or 0 BarrierResult within +/- secondsWindow seconds of rtTimestamp, update it.
        /// </summary>
        public void ReconcileManualOpen(DateTime rtTimestamp, byte door, byte barrierResult, int secondsWindow = 5, string appendNote = "")
        {
            _ = ReconcileManualOpenAsync(rtTimestamp, door, barrierResult, secondsWindow, appendNote);
        }

        public async Task<bool> ReconcileManualOpenAsync(DateTime rtTimestamp, byte door, byte barrierResult, int secondsWindow = 5, string appendNote = "")
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "RECONCILE_OPEN",
                new { Timestamp = rtTimestamp, Door = door, Result = barrierResult },
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"UPDATE dbo.ButtonPressLog
                                    SET BarrierResult = @barrier, Notes = COALESCE(Notes,'') + @append
                                    WHERE Door = @door AND Action = 'MANUAL_OPEN' AND (BarrierResult IS NULL OR BarrierResult = 0)
                                      AND ABS(DATEDIFF(SECOND, Timestamp, @ts)) <= @window", conn))
                    {
                        cmd.Parameters.AddWithValue("@barrier", (object?)barrierResult ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@append", appendNote ?? string.Empty);
                        cmd.Parameters.AddWithValue("@door", door);
                        cmd.Parameters.AddWithValue("@ts", rtTimestamp);
                        cmd.Parameters.AddWithValue("@window", secondsWindow);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        public async Task<int?> GetZoneBySiteAndVehicleTypeAsync(int siteId, int loaiXeId)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<int?>(
                $"GET_ZONE_SITE_{siteId}_TYPE_{loaiXeId}",
                async conn =>
                {
                    string sql = @"
                        SELECT TOP 1 Id 
                        FROM dbo.ParkingZones 
                        WHERE SiteId = @siteId 
                          AND LoaiXeId = @loaiXeId 
                          AND IsActive = 1";
                          
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@siteId", siteId);
                        cmd.Parameters.AddWithValue("@loaiXeId", loaiXeId);
                        var result = await cmd.ExecuteScalarAsync();
                        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : null;
                    }
                }
            );
        }

        // Add an entry when a vehicle enters. Primary key for identification is uid (CardUID).
        // Parameters: uid (CardUID), bienSo (nullable), anhXe (nullable)
        public void ThemXe(int cardId, string bienSo, string anhXe, int? siteId = null, int? zoneId = null, int? entryLaneId = null, string? entryWorkstationId = null, int? entryControllerId = null)
        {
            Task.Run(() => ThemXeAsync(cardId, bienSo, anhXe, siteId, zoneId, entryLaneId, entryWorkstationId, entryControllerId)).GetAwaiter().GetResult();
        }

        public async Task<bool> ThemXeAsync(int cardId, string bienSo, string anhXe, int? siteId = null, int? zoneId = null, int? entryLaneId = null, string? entryWorkstationId = null, int? entryControllerId = null)
        {
            if (!string.IsNullOrEmpty(bienSo))
            {
                bienSo = new string(bienSo.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
            }
            if (cardId <= 0) return false;

            if (string.IsNullOrEmpty(entryWorkstationId))
            {
                entryWorkstationId = AppConfig.Load().WorkstationId;
            }
            if (!entryControllerId.HasValue && entryLaneId.HasValue)
            {
                try
                {
                    using (var conn = new SqlConnection(GetConnectionString()))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand("SELECT TOP 1 ControllerId FROM dbo.Barriers WHERE LaneId = @laneId AND IsActive = 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@laneId", entryLaneId.Value);
                            var val = await cmd.ExecuteScalarAsync();
                            if (val != null && val != DBNull.Value)
                            {
                                entryControllerId = Convert.ToInt32(val);
                            }
                        }
                    }
                }
                catch { }
            }

            // 1. Transaction-safe local SQLite Save FIRST (Crash-Safe Session Commit!)
            await OfflineCacheService.Instance.SaveActiveSessionLocalAsync(cardId, bienSo, DateTime.Now, anhXe, siteId, zoneId, entryLaneId, entryWorkstationId, entryControllerId);

            // 2. Perform write to SQL Server or Queue if offline
            var newRecord = (Id: cardId, BienSo: bienSo ?? string.Empty, ThoiGianVao: DateTime.Now);

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "INSERT_XE_VAO",
                new { CardId = cardId, BienSo = bienSo, AnhXe = anhXe, Time = DateTime.Now, SiteId = siteId, ZoneId = zoneId, EntryLaneId = entryLaneId, EntryWorkstationId = entryWorkstationId, EntryControllerId = entryControllerId },
                async conn =>
                {
                    // Check if already in lot
                    using (SqlCommand checkCmd = new SqlCommand( @"SELECT COUNT(1) FROM XeTrongBai WHERE CardId = @cardId AND ThoiGianRa IS NULL", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@cardId", cardId);
                        int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                        if (exists > 0) return;
                    }
                    using (SqlCommand cmd = new SqlCommand( @"INSERT INTO XeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe, SiteId, ZoneId, EntryLaneId, EntryWorkstationId, EntryControllerId) VALUES (@CardId, @BienSo, @Time, @AnhXe, @SiteId, @ZoneId, @EntryLane, @EntryWorkstation, @EntryController)", conn))
                    {
                        cmd.Parameters.AddWithValue("@CardId", cardId);
                        cmd.Parameters.AddWithValue("@BienSo", string.IsNullOrEmpty(bienSo) ? (object)DBNull.Value : bienSo);
                        cmd.Parameters.AddWithValue("@Time", DateTime.Now);
                        cmd.Parameters.AddWithValue("@AnhXe", string.IsNullOrEmpty(anhXe) ? (object)DBNull.Value : anhXe);
                        cmd.Parameters.AddWithValue("@SiteId", (object?)siteId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ZoneId", (object?)zoneId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntryLane", (object?)entryLaneId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntryWorkstation", (object?)entryWorkstationId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@EntryController", (object?)entryControllerId ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                localCacheUpdater: async () =>
                {
                    // Ghi vào cache để IsXeTrongBaiByCardId và GetXeTrongBaiRecordByCardId nhận biết xe đang trong bãi
                    await OfflineCacheService.Instance.SaveCacheAsync($"CHECK_XE_TRONG_BAI", true); // generic key
                    await OfflineCacheService.Instance.SaveCacheAsync($"CHECK_XE_CARD_{cardId}", true);
                    await OfflineCacheService.Instance.SaveCacheAsync($"RECORD_XE_CARD_{cardId}", newRecord);
                    await UpdateTodayStatsCacheOfflineAsync(siteId, isEntry: true, revenueChange: 0);
                }
            );
        }

        public DataTable LayXeTrongBai()
        {
            return Task.Run(() => LayXeTrongBaiAsync()).GetAwaiter().GetResult();
        }

        public async Task<DataTable> LayXeTrongBaiAsync()
        {
            // Note: Returning DataTable for legacy reasons, but this is less efficient for caching.
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<DataTable>(
                "LIST_XE_TRONG_BAI",
                async conn =>
                {
                    using (var adapter = new SqlDataAdapter( @"SELECT * FROM XeTrongBai WHERE ThoiGianRa IS NULL", conn))
                    {
                        var table = new DataTable();
                        adapter.Fill(table);
                        return table;
                    }
                }
            ) ?? new DataTable();
        }

        public void XoaXe(string bienSo)
        {
            Task.Run(() => XoaXeAsync(bienSo)).GetAwaiter().GetResult();
        }

        public async Task<bool> XoaXeAsync(string bienSo)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_XE_BY_PLATE",
                new { BienSo = bienSo },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"DELETE FROM XeTrongBai WHERE BienSo = @bienSo AND ThoiGianRa IS NULL", conn))
                    {
                        cmd.Parameters.AddWithValue("@bienSo", bienSo ?? string.Empty);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
        }

        // Delete active entries by CardId (preferred). Do NOT use CardUID in XeTrongBai queries.
        public void XoaXeByCardId(int cardId)
        {
            Task.Run(() => XoaXeByCardIdAsync(cardId)).GetAwaiter().GetResult();
        }

        public async Task<bool> XoaXeByCardIdAsync(int cardId)
        {
            // 1. Transaction-safe local SQLite delete FIRST (Crash-Safe Session Commit!)
            await OfflineCacheService.Instance.DeleteActiveSessionLocalAsync(cardId);

            // 2. Perform write to SQL Server or Queue if offline
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_XE_TRONG_BAI",
                new { CardId = cardId },
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"DELETE FROM XeTrongBai WHERE CardId = @cardId", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", cardId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                localCacheUpdater: async () =>
                {
                    // Xóa cache xe trong bãi để lần quẹt tiếp theo biết xe đã ra
                    await OfflineCacheService.Instance.SaveCacheAsync($"CHECK_XE_CARD_{cardId}", false);
                    await OfflineCacheService.Instance.SaveCacheAsync($"RECORD_XE_CARD_{cardId}", (object?)null);
                    await OfflineCacheService.Instance.SaveCacheAsync($"ENTITY_XE_CARD_{cardId}", (object?)null);
                }
            );
        }

        public bool IsXeTrongBaiByCardId(int cardId)
        {
            return Task.Run(() => IsXeTrongBaiByCardIdAsync(cardId)).GetAwaiter().GetResult();
        }

        public async Task<bool> IsXeTrongBaiByCardIdAsync(int cardId)
        {
            // Fallback to SQLite Local Active sessions if SQL Server is down
            if (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline)
            {
                return await OfflineCacheService.Instance.IsXeTrongBaiLocalAsync(cardId);
            }

            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<bool>(
                $"CHECK_XE_CARD_{cardId}",
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"
            SELECT COUNT(1)
            FROM XeTrongBai
            WHERE CardId = @cardId
              AND ThoiGianRa IS NULL", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", cardId);
                        var v = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(v) > 0;
                    }
                }
            );
        }

        public int GetXeTrongBaiCountByCardId(int cardId)
        {
            return Task.Run(() => GetXeTrongBaiCountByCardIdAsync(cardId)).GetAwaiter().GetResult();
        }

        public async Task<int> GetXeTrongBaiCountByCardIdAsync(int cardId)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<int>(
                "COUNT_XE_TRONG_BAI",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"
            SELECT COUNT(1)
            FROM XeTrongBai
            WHERE CardId = @cardId
              AND ThoiGianRa IS NULL", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", cardId);
                        var v = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(v);
                    }
                }
            );
        }

        // Get plate from active XeTrongBai by CardId
        public string GetBienSoFromXeTrongBaiByCardId(int cardId)
        {
            return Task.Run(() => GetBienSoFromXeTrongBaiByCardIdAsync(cardId)).GetAwaiter().GetResult();
        }

        public async Task<string> GetBienSoFromXeTrongBaiByCardIdAsync(int cardId)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<string>(
                "GET_BIENSO_TRONG_BAI",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT BienSo FROM XeTrongBai WHERE CardId = @cardId AND ThoiGianRa IS NULL", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", cardId);
                        var v = await cmd.ExecuteScalarAsync();
                        return v?.ToString() ?? string.Empty;
                    }
                }
            ) ?? string.Empty;
        }

        // Return XeTrongBai record for a CardId where ThoiGianRa IS NULL. Returns null if not found.
        public (int Id, string BienSo, DateTime ThoiGianVao)? GetXeTrongBaiRecordByCardId(int cardId)
        {
            return Task.Run(() => GetXeTrongBaiRecordByCardIdAsync(cardId)).GetAwaiter().GetResult();
        }

        public async Task<(int Id, string BienSo, DateTime ThoiGianVao)?> GetXeTrongBaiRecordByCardIdAsync(int cardId)
        {
            if (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline)
            {
                var local = await OfflineCacheService.Instance.GetXeTrongBaiRecordLocalAsync(cardId);
                if (local == null) return null;
                return (local.Value.CardId, local.Value.BienSo, local.Value.ThoiGianVao);
            }

            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<(int Id, string BienSo, DateTime ThoiGianVao)?>(
                $"RECORD_XE_CARD_{cardId}",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT TOP 1 Id, BienSo, ThoiGianVao FROM XeTrongBai WHERE CardId = @cardId AND ThoiGianRa IS NULL ORDER BY ThoiGianVao DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", cardId);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                int id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0;
                                string bs = r["BienSo"]?.ToString() ?? string.Empty;
                                DateTime vao = r["ThoiGianVao"] != DBNull.Value ? Convert.ToDateTime(r["ThoiGianVao"]) : DateTime.MinValue;
                                return (id, bs, vao);
                            }
                        }
                    }
                    return null;
                }
            );
        }

        public async Task<(int Id, int CardId, string BienSo, DateTime ThoiGianVao)?> GetXeTrongBaiRecordByPlateAsync(string bienSo)
        {
            if (!string.IsNullOrEmpty(bienSo))
            {
                bienSo = new string(bienSo.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
            }
            if (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline)
            {
                var local = await OfflineCacheService.Instance.GetXeTrongBaiRecordByPlateLocalAsync(bienSo);
                if (local == null) return null;
                // local returns (CardId, BienSo, ThoiGianVao, SiteId, ZoneId, EntryLaneId), CardId is placed as Id, CardId for mapping
                return (local.Value.CardId, local.Value.CardId, local.Value.BienSo, local.Value.ThoiGianVao);
            }

            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<(int Id, int CardId, string BienSo, DateTime ThoiGianVao)?>(
                $"RECORD_XE_PLATE_{bienSo}",
                async conn =>
                {
                    using (var cmd = new SqlCommand( @"SELECT TOP 1 Id, CardId, BienSo, ThoiGianVao FROM XeTrongBai WHERE BienSo = @bs AND ThoiGianRa IS NULL ORDER BY ThoiGianVao DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@bs", bienSo ?? string.Empty);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                int id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0;
                                int cardId = r["CardId"] != DBNull.Value ? Convert.ToInt32(r["CardId"]) : 0;
                                string bs = r["BienSo"]?.ToString() ?? string.Empty;
                                DateTime vao = r["ThoiGianVao"] != DBNull.Value ? Convert.ToDateTime(r["ThoiGianVao"]) : DateTime.MinValue;
                                return (id, cardId, bs, vao);
                            }
                        }
                    }
                    return null;
                }
            );
        }

        public async Task<XeTrongBai?> GetXeTrongBaiEntityByCardIdAsync(int cardId)
        {
            // Fallback to SQLite Local Active sessions if SQL Server is down
            if (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline)
            {
                var local = await OfflineCacheService.Instance.GetXeTrongBaiRecordLocalAsync(cardId);
                if (local == null) return null;
                return new XeTrongBai
                {
                    CardId = local.Value.CardId,
                    BienSo = local.Value.BienSo,
                    ThoiGianVao = local.Value.ThoiGianVao,
                    SiteId = local.Value.SiteId,
                    ZoneId = local.Value.ZoneId,
                    EntryLaneId = local.Value.EntryLaneId
                };
            }

            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<XeTrongBai?>(
                $"ENTITY_XE_CARD_{cardId}",
                async conn =>
                {
                    using (var cmd = new SqlCommand(@"SELECT TOP 1 Id, CardId, BienSo, ThoiGianVao, AnhXe, SiteId, ZoneId, EntryLaneId FROM XeTrongBai WHERE CardId = @cardId AND ThoiGianRa IS NULL ORDER BY ThoiGianVao DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", cardId);
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return new XeTrongBai
                                {
                                    Id = r["Id"] != DBNull.Value ? Convert.ToInt32(r["Id"]) : 0,
                                    CardId = r["CardId"] != DBNull.Value ? Convert.ToInt32(r["CardId"]) : (int?)null,
                                    BienSo = r["BienSo"]?.ToString() ?? string.Empty,
                                    ThoiGianVao = r["ThoiGianVao"] != DBNull.Value ? Convert.ToDateTime(r["ThoiGianVao"]) : DateTime.MinValue,
                                    AnhXe = r["AnhXe"]?.ToString() ?? string.Empty,
                                    SiteId = r["SiteId"] != DBNull.Value ? Convert.ToInt32(r["SiteId"]) : (int?)null,
                                    ZoneId = r["ZoneId"] != DBNull.Value ? Convert.ToInt32(r["ZoneId"]) : (int?)null,
                                    EntryLaneId = r["EntryLaneId"] != DBNull.Value ? Convert.ToInt32(r["EntryLaneId"]) : (int?)null
                                };
                            }
                        }
                    }
                    return null;
                }
            );
        }



        private bool ColumnExists(SqlConnection conn, string tableName, string columnName)
        {
            using (var cmd = new SqlCommand( @"SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @col", conn))
            {
                cmd.Parameters.AddWithValue("@table", tableName);
                cmd.Parameters.AddWithValue("@col", columnName);
                var v = cmd.ExecuteScalar();
                return v != null && Convert.ToInt32(v) > 0;
            }
        }

        public async Task<int> GetXeTrongBaiCountByZoneAsync(int zoneId)
        {
            if (!ConnectivityStateService.Instance.IsOnline) return 0; // Or fetch from offline cache if needed
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<int>(
                $"COUNT_ZONE_{zoneId}",
                async conn =>
                {
                    string sql = @"
                        SELECT COUNT(*) 
                        FROM XeTrongBai x
                        LEFT JOIN Lanes l ON x.EntryLaneId = l.Id
                        WHERE x.ThoiGianRa IS NULL 
                          AND (x.ZoneId = @zoneId OR (x.ZoneId IS NULL AND l.ZoneId = @zoneId))";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@zoneId", zoneId);
                        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }
                }
            );
        }

        public async Task<string> GetZoneOccupancyBreakdownAsync(int zoneId)
        {
            if (!ConnectivityStateService.Instance.IsOnline) return string.Empty;
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<string>(
                $"ZONE_BREAKDOWN_{zoneId}",
                async conn =>
                {
                    string sql = @"
                        SELECT ISNULL(lx.TenLoai, N'Khác') as TenLoai, COUNT(x.Id) as CountVal
                        FROM XeTrongBai x
                        LEFT JOIN RFIDCards c ON x.CardId = c.Id
                        LEFT JOIN LoaiXe lx ON c.LoaiXeId = lx.Id
                        LEFT JOIN Lanes l ON x.EntryLaneId = l.Id
                        WHERE x.ThoiGianRa IS NULL
                          AND (x.ZoneId = @zoneId OR (x.ZoneId IS NULL AND l.ZoneId = @zoneId))
                        GROUP BY ISNULL(lx.TenLoai, N'Khác')";
                        
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@zoneId", zoneId);
                        var list = new List<string>();
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string typeName = reader.IsDBNull(0) ? "N/A" : reader.GetString(0);
                                int count = reader.GetInt32(1);
                                list.Add($"{count} {typeName}");
                            }
                        }
                        return list.Any() ? "(" + string.Join(", ", list) + ")" : "";
                    }
                }
            );
        }

        public void LuuLichSu(string bienSo, DateTime vao, DateTime ra, double tien, string anhXe, string cardUid = null, int? siteId = null, int? zoneId = null, int? entryLaneId = null, int? exitLaneId = null, string? entryWorkstationId = null, string? exitWorkstationId = null, int? entryControllerId = null, int? exitControllerId = null)
        {
            Task.Run(() => LuuLichSuAsync(bienSo, vao, ra, tien, anhXe, cardUid, siteId, zoneId, entryLaneId, exitLaneId, null, entryWorkstationId, exitWorkstationId, entryControllerId, exitControllerId)).GetAwaiter().GetResult();
        }

        public async Task<bool> LuuLichSuAsync(string bienSo, DateTime vao, DateTime ra, double tien, string anhXe, string cardUid = null, int? siteId = null, int? zoneId = null, int? entryLaneId = null, int? exitLaneId = null, string anhVao = null, string? entryWorkstationId = null, string? exitWorkstationId = null, int? entryControllerId = null, int? exitControllerId = null)
        {
            if (!string.IsNullOrEmpty(bienSo))
            {
                bienSo = new string(bienSo.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
            }
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "INSERT_LICH_SU",
                new { BienSo = bienSo, Vao = vao, Ra = ra, Tien = tien, AnhXe = anhXe, AnhVao = anhVao, CardUid = cardUid, SiteId = siteId, ZoneId = zoneId, EntryLaneId = entryLaneId, ExitLaneId = exitLaneId, EntryWorkstationId = entryWorkstationId, ExitWorkstationId = exitWorkstationId, EntryControllerId = entryControllerId, ExitControllerId = exitControllerId },
                async conn =>
                {
                    int? cardId = null;
                    if (!string.IsNullOrEmpty(cardUid))
                    {
                        using (var lookup = new SqlCommand( @"SELECT Id FROM RFIDCards WHERE CardUID = @uid", conn))
                        {
                            lookup.Parameters.AddWithValue("@uid", cardUid);
                            var v = await lookup.ExecuteScalarAsync();
                            if (v != null && v != DBNull.Value) cardId = Convert.ToInt32(v);
                        }
                    }
                    else if (!string.IsNullOrEmpty(bienSo))
                    { 
                        using (var lookup = new SqlCommand( @"SELECT Id FROM RFIDCards WHERE BienSo = @bs", conn))
                        {
                            lookup.Parameters.AddWithValue("@bs", bienSo);
                            var v = await lookup.ExecuteScalarAsync();
                            if (v != null && v != DBNull.Value) cardId = Convert.ToInt32(v);
                        }
                    }

                    // Auto-resolve workstation/controller IDs
                    if (string.IsNullOrEmpty(entryWorkstationId) && cardId.HasValue)
                    {
                        try
                        {
                            using (var cmd = new SqlCommand("SELECT TOP 1 EntryWorkstationId FROM XeTrongBai WHERE CardId = @cardId", conn))
                            {
                                cmd.Parameters.AddWithValue("@cardId", cardId.Value);
                                var val = await cmd.ExecuteScalarAsync();
                                if (val != null && val != DBNull.Value) entryWorkstationId = val.ToString();
                            }
                        }
                        catch { }
                    }
                    if (!entryControllerId.HasValue && entryLaneId.HasValue)
                    {
                        try
                        {
                            using (var cmd = new SqlCommand("SELECT TOP 1 ControllerId FROM dbo.Barriers WHERE LaneId = @laneId AND IsActive = 1", conn))
                            {
                                cmd.Parameters.AddWithValue("@laneId", entryLaneId.Value);
                                var val = await cmd.ExecuteScalarAsync();
                                if (val != null && val != DBNull.Value) entryControllerId = Convert.ToInt32(val);
                            }
                        }
                        catch { }
                    }
                    if (string.IsNullOrEmpty(exitWorkstationId))
                    {
                        exitWorkstationId = AppConfig.Load().WorkstationId;
                    }
                    if (!exitControllerId.HasValue && exitLaneId.HasValue)
                    {
                        try
                        {
                            using (var cmd = new SqlCommand("SELECT TOP 1 ControllerId FROM dbo.Barriers WHERE LaneId = @laneId AND IsActive = 1", conn))
                            {
                                cmd.Parameters.AddWithValue("@laneId", exitLaneId.Value);
                                var val = await cmd.ExecuteScalarAsync();
                                if (val != null && val != DBNull.Value) exitControllerId = Convert.ToInt32(val);
                            }
                        }
                        catch { }
                    }

                    using (SqlCommand cmd = new SqlCommand( @"INSERT INTO LichSuXe (CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, AnhVao, AnhRa, SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId) VALUES (@cardId, @bs, @vao, @ra, @tien, @anhVao, @anhRa, @siteId, @zoneId, @entryLane, @exitLane, @entryWS, @exitWS, @entryCtrl, @exitCtrl)", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", (object?)cardId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@bs", string.IsNullOrEmpty(bienSo) ? (object?)DBNull.Value : bienSo);
                        cmd.Parameters.AddWithValue("@vao", vao);
                        cmd.Parameters.AddWithValue("@ra", ra);
                        cmd.Parameters.AddWithValue("@tien", tien);
                        cmd.Parameters.AddWithValue("@anhVao", (object?)anhVao ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@anhRa", (object?)anhXe ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@siteId", (object?)siteId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@zoneId", (object?)zoneId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@entryLane", (object?)entryLaneId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@exitLane", (object?)exitLaneId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@entryWS", (object?)entryWorkstationId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@exitWS", (object?)exitWorkstationId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@entryCtrl", (object?)entryControllerId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@exitCtrl", (object?)exitControllerId ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                localCacheUpdater: async () =>
                {
                    bool isOvernight = vao < DateTime.Today;
                    await UpdateTodayStatsCacheOfflineAsync(siteId, isEntry: false, revenueChange: tien, isOvernightExit: isOvernight);
                }
            );
        }

        public List<LichSuXe> LayLichSu()
        {
            return Task.Run(() => LayLichSuAsync()).GetAwaiter().GetResult();
        }

        public async Task<List<LichSuXe>> LayLichSuAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<LichSuXe>>(
                "LIST_LICH_SU",
                async conn =>
                {
                    var list = new List<LichSuXe>();

                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT TOP 1000 * FROM (
                            SELECT 
                                ls.Id, ls.CardId, ls.BienSo, ls.ThoiGianVao, ls.ThoiGianRa, ls.Tien, 
                                ISNULL(ls.TrangThai, N'Đã ra') AS TrangThai, 
                                ls.AnhVao, ls.AnhRa,
                                ls.SiteId, ls.ZoneId, ls.EntryLaneId, ls.ExitLaneId,
                                s.SiteName, z.ZoneName, el.LaneName AS EntryLaneName, exl.LaneName AS ExitLaneName,
                                lv.TenLoai AS LoaiVeName, lx.TenLoai AS LoaiXeName,
                                emp.FullName AS EmployeeName, emp.EmployeeCode,
                                comp.Name AS CompanyName, dept.DepartmentName
                            FROM LichSuXe ls
                            LEFT JOIN ParkingSites s ON ls.SiteId = s.Id
                            LEFT JOIN ParkingZones z ON ls.ZoneId = z.Id
                            LEFT JOIN Lanes el ON ls.EntryLaneId = el.Id
                            LEFT JOIN Lanes exl ON ls.ExitLaneId = exl.Id
                            LEFT JOIN RFIDCards r ON ls.CardId = r.Id
                            LEFT JOIN LoaiVe lv ON r.LoaiVeId = lv.Id
                            LEFT JOIN LoaiXe lx ON r.LoaiXeId = lx.Id
                            LEFT JOIN Employees emp ON r.EmployeeId = emp.Id AND emp.IsDeleted = 0
                            LEFT JOIN Companies comp ON emp.CompanyId = comp.Id
                            LEFT JOIN Departments dept ON emp.DepartmentId = dept.Id

                            UNION ALL

                            SELECT 
                                -xtb.Id AS Id, xtb.CardId, xtb.BienSo, xtb.ThoiGianVao, NULL AS ThoiGianRa, NULL AS Tien, 
                                N'Trong bãi' AS TrangThai, 
                                xtb.AnhXe AS AnhVao, NULL AS AnhRa,
                                xtb.SiteId, xtb.ZoneId, xtb.EntryLaneId, NULL AS ExitLaneId,
                                s.SiteName, z.ZoneName, el.LaneName AS EntryLaneName, NULL AS ExitLaneName,
                                lv.TenLoai AS LoaiVeName, lx.TenLoai AS LoaiXeName,
                                emp.FullName AS EmployeeName, emp.EmployeeCode,
                                comp.Name AS CompanyName, dept.DepartmentName
                            FROM XeTrongBai xtb
                            LEFT JOIN ParkingSites s ON xtb.SiteId = s.Id
                            LEFT JOIN ParkingZones z ON xtb.ZoneId = z.Id
                            LEFT JOIN Lanes el ON xtb.EntryLaneId = el.Id
                            LEFT JOIN RFIDCards r ON xtb.CardId = r.Id
                            LEFT JOIN LoaiVe lv ON r.LoaiVeId = lv.Id
                            LEFT JOIN LoaiXe lx ON r.LoaiXeId = lx.Id
                            LEFT JOIN Employees emp ON r.EmployeeId = emp.Id AND emp.IsDeleted = 0
                            LEFT JOIN Companies comp ON emp.CompanyId = comp.Id
                            LEFT JOIN Departments dept ON emp.DepartmentId = dept.Id
                            WHERE xtb.ThoiGianRa IS NULL
                        ) AS combined
                        ORDER BY ThoiGianVao DESC", conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new LichSuXe
                            {
                                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                                CardId = reader["CardId"] != DBNull.Value ? Convert.ToInt32(reader["CardId"]) : 0,
                                BienSo = reader["BienSo"]?.ToString(),
                                ThoiGianVao = reader["ThoiGianVao"] != DBNull.Value ? Convert.ToDateTime(reader["ThoiGianVao"]) : DateTime.MinValue,
                                ThoiGianRa = reader["ThoiGianRa"] != DBNull.Value ? Convert.ToDateTime(reader["ThoiGianRa"]) : (DateTime?)null,
                                Tien = reader["Tien"] != DBNull.Value ? Convert.ToDouble(reader["Tien"]) : (double?)null,
                                TrangThai = reader["TrangThai"]?.ToString(),
                                AnhVao = reader["AnhVao"]?.ToString(),
                                AnhRa = reader["AnhRa"]?.ToString(),

                                SiteId = reader["SiteId"] != DBNull.Value ? Convert.ToInt32(reader["SiteId"]) : (int?)null,
                                ZoneId = reader["ZoneId"] != DBNull.Value ? Convert.ToInt32(reader["ZoneId"]) : (int?)null,
                                EntryLaneId = reader["EntryLaneId"] != DBNull.Value ? Convert.ToInt32(reader["EntryLaneId"]) : (int?)null,
                                ExitLaneId = reader["ExitLaneId"] != DBNull.Value ? Convert.ToInt32(reader["ExitLaneId"]) : (int?)null,

                                SiteName = reader["SiteName"]?.ToString() ?? string.Empty,
                                ZoneName = reader["ZoneName"]?.ToString() ?? string.Empty,
                                EntryLaneName = reader["EntryLaneName"]?.ToString() ?? string.Empty,
                                ExitLaneName = reader["ExitLaneName"]?.ToString() ?? string.Empty,
                                LoaiVeName = reader["LoaiVeName"]?.ToString() ?? string.Empty,
                                LoaiXeName = reader["LoaiXeName"]?.ToString() ?? string.Empty,

                                EmployeeName = reader["EmployeeName"]?.ToString() ?? string.Empty,
                                EmployeeCode = reader["EmployeeCode"]?.ToString() ?? string.Empty,
                                CompanyName = reader["CompanyName"]?.ToString() ?? string.Empty,
                                DepartmentName = reader["DepartmentName"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<LichSuXe>();
        }

        public bool CheckCardExists(string uid)
        {
            return Task.Run(() => CheckCardExistsAsync(uid)).GetAwaiter().GetResult();
        }

        public async Task<bool> CheckCardExistsAsync(string uid)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<bool>(
                $"CHECK_CARD_{uid}",
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"SELECT COUNT(*) FROM RFIDCards WHERE CardUID = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                        var v = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(v) > 0;
                    }
                }
            );
        }

        public bool CheckCardActiveExists(string uid)
        {
            return Task.Run(() => CheckCardActiveExistsAsync(uid)).GetAwaiter().GetResult();
        }

        public async Task<bool> CheckCardActiveExistsAsync(string uid)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<bool>(
                $"CHECK_CARD_ACTIVE_{uid}",
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"SELECT COUNT(*) FROM RFIDCards WHERE CardUID = @uid AND TrangThai = 'Active'", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                        var v = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(v) > 0;
                    }
                }
            );
        }

        public int GetCardIdFromUid(string uid)
        {
            return Task.Run(() => GetCardIdFromUidAsync(uid)).GetAwaiter().GetResult();
        }

        public async Task<int> GetCardIdFromUidAsync(string uid)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<int>(
                $"GET_CARD_ID_{uid}",
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand(@"SELECT Id FROM RFIDCards WHERE CardUID = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                        var result = await cmd.ExecuteScalarAsync();
                        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }
                }
            );
        }

        public string GetBienSoFromUID(string uid)
        {
            return Task.Run(() => GetBienSoFromUIDAsync(uid)).GetAwaiter().GetResult();
        }

        public async Task<string> GetBienSoFromUIDAsync(string uid)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<string>(
                $"BIENSO_FROM_UID_{uid}",
                async conn =>
                {
                    using (SqlCommand cmd = new SqlCommand( @"SELECT BienSo FROM RFIDCards WHERE CardUID = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", uid ?? string.Empty);
                        var result = await cmd.ExecuteScalarAsync();
                        return result?.ToString() ?? string.Empty;
                    }
                }
            ) ?? string.Empty;
        }

        public bool AddRFIDCards(string uid, string bienSo, string loaiThe, int? employeeId = null)
        {
            string normalizedUid = RFIDService.ChuanHoaUID(uid);
            if (CheckCardActiveExists(normalizedUid))
            {
                throw new InvalidOperationException("Thẻ này đang hoạt động trong hệ thống!");
            }

            bool exists = CheckCardExists(normalizedUid);
            if (exists)
            {
                int cardId = GetCardIdFromUid(normalizedUid);
                if (cardId > 0)
                {
                    XoaXeByCardId(cardId);
                }
            }

            string conn_string = GetWorkingConnection();
            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                int loaiVeId = 1; // Default to Vãng lai
                int loaiXeId = 1; // Default to Xe máy
                
                if (loaiThe != null)
                {
                    string normalized = loaiThe.ToLowerInvariant();
                    if (normalized.Contains("thang") || normalized.Contains("tháng") || normalized.Contains("monthly"))
                    {
                        loaiVeId = 2; // Tháng
                    }
                }

                string sql = exists
                    ? @"UPDATE RFIDCards SET BienSo = @bs, LoaiVeId = @lvId, LoaiXeId = @lxId, TrangThai = 'Active', NgayDangKy = GETDATE(), EmployeeId = @empid WHERE CardUID = @uid"
                    : @"INSERT INTO RFIDCards (CardUID, BienSo, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy, EmployeeId)
                        VALUES (@uid, @bs, @lvId, @lxId, 'Active', GETDATE(), @empid)";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", normalizedUid);
                    cmd.Parameters.AddWithValue("@bs", bienSo);
                    cmd.Parameters.AddWithValue("@lvId", loaiVeId);
                    cmd.Parameters.AddWithValue("@lxId", loaiXeId);
                    cmd.Parameters.AddWithValue("@empid", (object?)employeeId ?? DBNull.Value);

                    try
                    {
                        cmd.ExecuteNonQuery();
                        InvalidateRFIDCardCache();
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

        public List<RFIDCards> LayDanhSachRFIDCards()
        {
            var list = new List<RFIDCards>();
            string conn_string = GetWorkingConnection();

            using (SqlConnection conn = new SqlConnection(conn_string))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand( @"
                    SELECT 
                        r.Id,
                        r.CardUID,
                        r.BienSo,
                        r.TrangThai,
                        r.EmployeeId,
                        e.FullName AS EmployeeName,
                        e.EmployeeCode AS EmployeeCode,
                        lx.TenLoai AS LoaiXe,
                        lv.TenLoai AS LoaiVe
                    FROM RFIDCards r
                    LEFT JOIN LoaiXe lx ON r.LoaiXeId = lx.Id
                    LEFT JOIN LoaiVe lv ON r.LoaiVeId = lv.Id
                    LEFT JOIN Employees e ON r.EmployeeId = e.Id AND e.IsDeleted = 0
                    ", conn))
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
                            LoaiVe = reader["LoaiVe"]?.ToString(),
                            EmployeeId = reader["EmployeeId"] != DBNull.Value ? Convert.ToInt32(reader["EmployeeId"]) : (int?)null,
                            EmployeeName = reader["EmployeeName"] == DBNull.Value ? null : reader["EmployeeName"].ToString(),
                            EmployeeCode = reader["EmployeeCode"] == DBNull.Value ? null : reader["EmployeeCode"].ToString()
                        });
                    }
                }
            }

            return list;
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
                    using (SqlCommand cmd = new SqlCommand( @"SELECT TimestampUtc, [Level], EventType, Source, UserId, Plate, Details, Exception, 
                                          Username, [Action], EntityName, EntityId, OldValues, NewValues, IpAddress, MachineName, DeviceName, SessionId, CorrelationId,
                                          DurationMs, RetryCount, FileSize, TestName, IsRecovered, AdditionalData
                                   FROM dbo.AppLogs
                                   WHERE (@from IS NULL OR TimestampUtc >= @from)
                                     AND (@to IS NULL OR TimestampUtc <= @to)
                                   ORDER BY TimestampUtc DESC", conn))
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
                                        Exception = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                        
                                        Username = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                                        Action = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                                        EntityName = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                                        EntityId = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                                        OldValues = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                                        NewValues = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                                        IpAddress = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                                        MachineName = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                                        DeviceName = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                                        SessionId = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                                        CorrelationId = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),

                                        DurationMs = reader.IsDBNull(19) ? (long?)null : reader.GetInt64(19),
                                        RetryCount = reader.IsDBNull(20) ? (int?)null : reader.GetInt32(20),
                                        FileSize = reader.IsDBNull(21) ? (long?)null : reader.GetInt64(21),
                                        TestName = reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                                        IsRecovered = reader.IsDBNull(23) ? (bool?)null : reader.GetBoolean(23),
                                        AdditionalData = reader.IsDBNull(24) ? string.Empty : reader.GetString(24)  
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
