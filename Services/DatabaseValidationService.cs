using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class DatabaseValidationService
    {
        public const int TargetSchemaVersion = 2; // Phiên bản ứng dụng yêu cầu

        public static async Task<DatabaseStatus> ValidateDatabaseAsync(ConnectionConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = await SqlConnectionService.GetConnectionAsync(config, timeout: 5, cancellationToken);

                // 1. Kiểm tra xem database có trống hoàn toàn không (không có bảng nào)
                var tables = await GetDatabaseTablesAsync(conn, cancellationToken);
                if (tables.Count == 0)
                {
                    return DatabaseStatus.Empty;
                }

                // 2. Kiểm tra các bảng bắt buộc (required tables)
                var requiredBases = new List<List<string>>
                {
                    new List<string> { "ParkingSites", "Sites" },
                    new List<string> { "ParkingZones", "Zones" },
                    new List<string> { "Lanes" },
                    new List<string> { "ParkingGates", "Gates" },
                    new List<string> { "C3Controllers", "Controllers" },
                    new List<string> { "RFIDCards" },
                    new List<string> { "NhanVien", "Users" },
                    new List<string> { "Roles" },
                    new List<string> { "XeTrongBai", "Vehicles" },
                    new List<string> { "VehicleSessions", "ParkingSessions" },
                    new List<string> { "LichSuXe", "Transactions" }
                };

                bool hasRequiredTables = true;
                foreach (var baseGroup in requiredBases)
                {
                    if (cancellationToken.IsCancellationRequested) return DatabaseStatus.Invalid;
                    
                    bool groupFound = false;
                    foreach (var tableName in baseGroup)
                    {
                        if (tables.Contains(tableName.ToLower()))
                        {
                            groupFound = true;
                            break;
                        }
                    }
                    if (!groupFound)
                    {
                        hasRequiredTables = false;
                        break;
                    }
                }

                if (!hasRequiredTables)
                {
                    return DatabaseStatus.Invalid;
                }

                // 3. Kiểm tra Schema Version từ bảng SystemInfo
                if (!tables.Contains("systeminfo"))
                {
                    return DatabaseStatus.NeedMigration;
                }

                int currentVersion = await GetSchemaVersionAsync(conn, cancellationToken);
                if (currentVersion < TargetSchemaVersion)
                {
                    return DatabaseStatus.NeedMigration;
                }

                return DatabaseStatus.Valid;
            }
            catch (SqlException ex)
            {
                LoggingService.Instance.LogError("DB_VALIDATION", "Validate", $"SQL Error: {ex.Number}", ex);
                return DatabaseStatus.Invalid;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DB_VALIDATION", "Validate", "Lỗi xác thực database", ex);
                return DatabaseStatus.Invalid;
            }
        }

        private static async Task<HashSet<string>> GetDatabaseTablesAsync(SqlConnection conn, CancellationToken cancellationToken)
        {
            var tables = new HashSet<string>();
            string query = "SELECT LOWER(TABLE_NAME) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';";
            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
            return tables;
        }

        private static async Task<int> GetSchemaVersionAsync(SqlConnection conn, CancellationToken cancellationToken)
        {
            try
            {
                string query = "SELECT TOP 1 SchemaVersion FROM dbo.SystemInfo ORDER BY CreatedDate DESC;";
                using var cmd = new SqlCommand(query, conn);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
