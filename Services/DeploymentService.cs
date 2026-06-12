using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class DeploymentRecord
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public DateTime DeployTime { get; set; }
        public string DeployBy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // SUCCESS, FAILED
        public string Notes { get; set; } = string.Empty;
    }

    public class DeploymentService
    {
        private static readonly DeploymentService _instance = new();
        public static DeploymentService Instance => _instance;

        private readonly DatabaseService _db = new();

        public TopologyValidationResult ValidateDraft()
        {
            return TopologyValidationService.Instance.ValidateTopology();
        }

        public async Task<bool> HasPendingChangesAsync()
        {
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return false;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"SELECT HasPendingChanges FROM dbo.ConfigurationState WHERE Id = 1", conn))
                    {
                        var res = await cmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value)
                        {
                            return Convert.ToBoolean(res);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentService", "HasPendingChangesAsync", "Error checking pending changes", ex);
            }
            return false;
        }

        /// <summary>
        /// Marks that there are pending (draft) configuration changes that need to be deployed.
        /// </summary>
        public async Task MarkPendingChangesAsync()
        {
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"UPDATE dbo.ConfigurationState SET HasPendingChanges = 1 WHERE Id = 1", conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                LoggingService.Instance.LogInfo("DeploymentService", "MarkPendingChangesAsync", "Marked configuration as having pending changes.");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentService", "MarkPendingChangesAsync", "Error marking pending changes", ex);
            }
        }

        public async Task<string> GetCurrentVersionAsync()
        {
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return "V0";

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    string sql = "SELECT ActiveVersion FROM dbo.ConfigurationState WHERE Id = 1";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        var res = await cmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value)
                        {
                            return $"V{res}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentService", "GetCurrentVersionAsync", "Error getting current version", ex);
            }
            return "V1";
        }

        public async Task<DeploymentRecord?> GetLastDeploymentAsync()
        {
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return null;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"SELECT TOP 1 Id, ActiveVersion, LastDeployedAt, LastDeployedBy FROM dbo.ConfigurationState WHERE Id = 1", conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                if (reader["LastDeployedAt"] != DBNull.Value)
                                {
                                    return new DeploymentRecord
                                    {
                                        Version = Convert.ToInt32(reader["ActiveVersion"]),
                                        DeployTime = Convert.ToDateTime(reader["LastDeployedAt"]),
                                        DeployBy = reader["LastDeployedBy"]?.ToString() ?? string.Empty
                                    };
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentService", "GetLastDeploymentAsync", "Error fetching last deployment", ex);
            }
            return null;
        }

        public async Task<bool> DeployAsync(string deployBy, string notes)
        {
            var validation = ValidateDraft();
            bool isValid = validation.IsValid;

            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return false;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // Get current version to either increment or reference
                    int currentVersion = 1;
                    using (var cmd = new SqlCommand(@"SELECT ActiveVersion FROM dbo.ConfigurationState WHERE Id = 1", conn))
                    {
                        var res = await cmd.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value)
                        {
                            currentVersion = Convert.ToInt32(res);
                        }
                    }

                    if (!isValid)
                    {
                        // 1. Save FAILED DeploymentRecord
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO dbo.DeploymentHistory (Version, DeployTime, DeployBy, Status, Notes)
                            VALUES (@version, @deployTime, @deployBy, @status, @notes)", conn))
                        {
                            cmd.Parameters.AddWithValue("@version", currentVersion);
                            cmd.Parameters.AddWithValue("@deployTime", DateTime.Now);
                            cmd.Parameters.AddWithValue("@deployBy", deployBy);
                            cmd.Parameters.AddWithValue("@status", "FAILED");
                            cmd.Parameters.AddWithValue("@notes", "Deployment blocked by validation errors. " + notes);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        return false;
                    }

                    // On success:
                    int newVersion = currentVersion + 1;

                    // 1. Update configuration state
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.ConfigurationState 
                        SET ActiveVersion = @newVersion, 
                            HasPendingChanges = 0, 
                            LastDeployedAt = @deployTime, 
                            LastDeployedBy = @deployBy
                        WHERE Id = 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@newVersion", newVersion);
                        cmd.Parameters.AddWithValue("@deployTime", DateTime.Now);
                        cmd.Parameters.AddWithValue("@deployBy", deployBy);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Add success deployment history log
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.DeploymentHistory (Version, DeployTime, DeployBy, Status, Notes)
                        VALUES (@version, @deployTime, @deployBy, @status, @notes)", conn))
                    {
                        cmd.Parameters.AddWithValue("@version", newVersion);
                        cmd.Parameters.AddWithValue("@deployTime", DateTime.Now);
                        cmd.Parameters.AddWithValue("@deployBy", deployBy);
                        cmd.Parameters.AddWithValue("@status", "SUCCESS");
                        cmd.Parameters.AddWithValue("@notes", notes ?? string.Empty);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Copy draft config files to active config files
                    try
                    {
                        var activeConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                        var draftConfigPath = Path.Combine(AppContext.BaseDirectory, "config_draft.json");
                        if (File.Exists(draftConfigPath))
                        {
                            File.Copy(draftConfigPath, activeConfigPath, true);
                        }

                        var activeMappingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reader_mappings.json");
                        var draftMappingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reader_mappings_draft.json");
                        if (File.Exists(draftMappingsPath))
                        {
                            File.Copy(draftMappingsPath, activeMappingsPath, true);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        LoggingService.Instance.LogError("DeploymentService", "DeployAsync", "Error copying configuration draft files", fileEx);
                    }

                    LoggingService.Instance.LogInfo("DEPLOY", "DeployAsync", $"Configuration successfully deployed to version V{newVersion} by {deployBy}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentService", "DeployAsync", "Error during deployment", ex);
                return false;
            }
        }

        public async Task<List<DeploymentRecord>> GetDeploymentHistoryAsync()
        {
            var list = new List<DeploymentRecord>();
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return list;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"
                        SELECT Id, Version, DeployTime, DeployBy, Status, Notes 
                        FROM dbo.DeploymentHistory 
                        ORDER BY DeployTime DESC, Id DESC", conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new DeploymentRecord
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Version = Convert.ToInt32(reader["Version"]),
                                    DeployTime = Convert.ToDateTime(reader["DeployTime"]),
                                    DeployBy = reader["DeployBy"].ToString() ?? string.Empty,
                                    Status = reader["Status"].ToString() ?? string.Empty,
                                    Notes = reader["Notes"] == DBNull.Value ? string.Empty : reader["Notes"].ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentService", "GetDeploymentHistoryAsync", "Error fetching deployment history", ex);
            }

            return list;
        }

        public async Task<List<ConfigurationAuditRecord>> GetChangesForDeploymentAsync(DeploymentRecord record)
        {
            var history = await GetDeploymentHistoryAsync();
            
            // Find the previous successful deployment before this record's DeployTime
            DateTime? start = null;
            foreach (var h in history)
            {
                if (string.Equals(h.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase) && h.DeployTime < record.DeployTime)
                {
                    start = h.DeployTime;
                    break; // history is ordered by DeployTime DESC, so the first one we find is the latest before record
                }
            }
            
            DateTime end = record.DeployTime;
            return await ConfigurationAuditService.Instance.GetChangesBetweenAsync(start, end);
        }
    }
}
