using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class ConfigurationAuditRecord
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
    }

    public class ConfigurationAuditService
    {
        private static readonly ConfigurationAuditService _instance = new();
        public static ConfigurationAuditService Instance => _instance;

        private readonly DatabaseService _db = new();

        public async Task RecordChangeAsync(string entityType, string entityName, string propertyName, string oldValue, string newValue, string? username = null)
        {
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return;

            string user = username ?? CurrentUser.Username ?? "System";

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    
                    // 1. Insert audit record
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.ConfigurationAudit (Timestamp, UserName, EntityType, EntityName, PropertyName, OldValue, NewValue)
                        VALUES (@timestamp, @username, @entityType, @entityName, @propertyName, @oldValue, @newValue)", conn))
                    {
                        cmd.Parameters.AddWithValue("@timestamp", DateTime.Now);
                        cmd.Parameters.AddWithValue("@username", user);
                        cmd.Parameters.AddWithValue("@entityType", entityType);
                        cmd.Parameters.AddWithValue("@entityName", entityName);
                        cmd.Parameters.AddWithValue("@propertyName", propertyName);
                        cmd.Parameters.AddWithValue("@oldValue", (object?)oldValue ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@newValue", (object?)newValue ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Mark HasPendingChanges = 1
                    using (var cmd = new SqlCommand(@"UPDATE dbo.ConfigurationState SET HasPendingChanges = 1 WHERE Id = 1", conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConfigurationAuditService", "RecordChangeAsync", "Error recording configuration change", ex);
            }
        }

        public async Task AuditChangesAsync<T>(string entityType, T? oldValue, T? newValue, string? username = null) where T : class
        {
            try
            {
                string entityName = oldValue != null ? GetEntityName(oldValue) : (newValue != null ? GetEntityName(newValue) : "Unknown");

                if (oldValue == null && newValue != null)
                {
                    // Addition: record all properties that have non-default/non-null values
                    var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        if (prop.Name == "Id" || prop.Name == "CreatedUtc" || prop.Name == "CreatedDate") continue;
                        if (!IsPrimitiveOrSimpleType(prop.PropertyType)) continue;

                        var val = prop.GetValue(newValue);
                        if (val != null && !string.IsNullOrEmpty(val.ToString()))
                        {
                            await RecordChangeAsync(entityType, entityName, prop.Name, string.Empty, val.ToString()!, username);
                        }
                    }
                }
                else if (oldValue != null && newValue == null)
                {
                    // Deletion
                    await RecordChangeAsync(entityType, entityName, "Action", "Active", "Deleted", username);
                }
                else if (oldValue != null && newValue != null)
                {
                    // Modification: compare properties
                    var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        if (prop.Name == "Id" || prop.Name == "CreatedUtc" || prop.Name == "CreatedDate") continue;
                        if (!IsPrimitiveOrSimpleType(prop.PropertyType)) continue;

                        var oldVal = prop.GetValue(oldValue);
                        var newVal = prop.GetValue(newValue);

                        string oldStr = oldVal?.ToString() ?? string.Empty;
                        string newStr = newVal?.ToString() ?? string.Empty;

                        if (oldStr != newStr)
                        {
                            await RecordChangeAsync(entityType, entityName, prop.Name, oldStr, newStr, username);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConfigurationAuditService", "AuditChangesAsync", "Error auditing entity changes", ex);
            }
        }

        private string GetEntityName(object entity)
        {
            if (entity == null) return string.Empty;
            var type = entity.GetType();
            var nameProps = new[] { "SiteName", "ZoneName", "GateName", "LaneName", "ControllerName", "Name", "SiteCode", "ZoneCode", "GateCode", "LaneCode", "IpAddress" };
            foreach (var propName in nameProps)
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(entity);
                    if (val != null && !string.IsNullOrEmpty(val.ToString()))
                        return val.ToString()!;
                }
            }
            // Fallback to Id
            var idProp = type.GetProperty("Id");
            if (idProp != null)
            {
                return $"ID {idProp.GetValue(entity)}";
            }
            return entity.ToString() ?? string.Empty;
        }

        private bool IsPrimitiveOrSimpleType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying.IsPrimitive || 
                   underlying == typeof(string) || 
                   underlying == typeof(decimal) || 
                   underlying == typeof(DateTime) || 
                   underlying.IsEnum;
        }

        public async Task<List<ConfigurationAuditRecord>> GetRecentChangesAsync(int count = 50)
        {
            var list = new List<ConfigurationAuditRecord>();
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return list;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    string query = $@"
                        SELECT TOP ({count}) Id, Timestamp, UserName, EntityType, EntityName, PropertyName, OldValue, NewValue
                        FROM dbo.ConfigurationAudit
                        ORDER BY Timestamp DESC, Id DESC";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ConfigurationAuditRecord
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                                    UserName = reader["UserName"].ToString() ?? string.Empty,
                                    EntityType = reader["EntityType"].ToString() ?? string.Empty,
                                    EntityName = reader["EntityName"].ToString() ?? string.Empty,
                                    PropertyName = reader["PropertyName"].ToString() ?? string.Empty,
                                    OldValue = reader["OldValue"] == DBNull.Value ? string.Empty : reader["OldValue"].ToString() ?? string.Empty,
                                    NewValue = reader["NewValue"] == DBNull.Value ? string.Empty : reader["NewValue"].ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConfigurationAuditService", "GetRecentChangesAsync", "Error fetching recent changes", ex);
            }

            return list;
        }

        public Task<List<ConfigurationAuditRecord>> GetAuditHistoryAsync()
        {
            return GetRecentChangesAsync(500);
        }

        public async Task<List<ConfigurationAuditRecord>> GetChangesSinceAsync(DateTime? since)
        {
            var list = new List<ConfigurationAuditRecord>();
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return list;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"
                        SELECT Id, Timestamp, UserName, EntityType, EntityName, PropertyName, OldValue, NewValue
                        FROM dbo.ConfigurationAudit
                        WHERE Timestamp > @since
                        ORDER BY Timestamp DESC, Id DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@since", since ?? new DateTime(1970, 1, 1));
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ConfigurationAuditRecord
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                                    UserName = reader["UserName"].ToString() ?? string.Empty,
                                    EntityType = reader["EntityType"].ToString() ?? string.Empty,
                                    EntityName = reader["EntityName"].ToString() ?? string.Empty,
                                    PropertyName = reader["PropertyName"].ToString() ?? string.Empty,
                                    OldValue = reader["OldValue"] == DBNull.Value ? string.Empty : reader["OldValue"].ToString() ?? string.Empty,
                                    NewValue = reader["NewValue"] == DBNull.Value ? string.Empty : reader["NewValue"].ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConfigurationAuditService", "GetChangesSinceAsync", "Error fetching changes since last deploy", ex);
            }

            return list;
        }

        public async Task<List<ConfigurationAuditRecord>> GetChangesBetweenAsync(DateTime? start, DateTime end)
        {
            var list = new List<ConfigurationAuditRecord>();
            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return list;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"
                        SELECT Id, Timestamp, UserName, EntityType, EntityName, PropertyName, OldValue, NewValue
                        FROM dbo.ConfigurationAudit
                        WHERE Timestamp > @start AND Timestamp <= @end
                        ORDER BY Timestamp DESC, Id DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@start", start ?? new DateTime(1970, 1, 1));
                        cmd.Parameters.AddWithValue("@end", end);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ConfigurationAuditRecord
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                                    UserName = reader["UserName"].ToString() ?? string.Empty,
                                    EntityType = reader["EntityType"].ToString() ?? string.Empty,
                                    EntityName = reader["EntityName"].ToString() ?? string.Empty,
                                    PropertyName = reader["PropertyName"].ToString() ?? string.Empty,
                                    OldValue = reader["OldValue"] == DBNull.Value ? string.Empty : reader["OldValue"].ToString() ?? string.Empty,
                                    NewValue = reader["NewValue"] == DBNull.Value ? string.Empty : reader["NewValue"].ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConfigurationAuditService", "GetChangesBetweenAsync", "Error fetching changes between dates", ex);
            }

            return list;
        }

        public async Task<Dictionary<string, int>> GetPendingChangesCountsAsync(DateTime? since)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Site", 0 },
                { "Zone", 0 },
                { "Gate", 0 },
                { "Lane", 0 },
                { "Reader Mapping", 0 },
                { "Controller", 0 }
            };

            string connStr = _db.GetConnectionString();
            if (string.IsNullOrEmpty(connStr)) return counts;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(@"
                        SELECT EntityType, COUNT(DISTINCT EntityName) as ItemCount
                        FROM dbo.ConfigurationAudit
                        WHERE Timestamp > @since
                        GROUP BY EntityType", conn))
                    {
                        cmd.Parameters.AddWithValue("@since", since ?? new DateTime(1970, 1, 1));
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string entityType = reader["EntityType"].ToString() ?? string.Empty;
                                int count = Convert.ToInt32(reader["ItemCount"]);
                                
                                // Map synonyms
                                if (string.Equals(entityType, "Controller Config", StringComparison.OrdinalIgnoreCase))
                                {
                                    entityType = "Controller";
                                }

                                if (counts.ContainsKey(entityType))
                                {
                                    counts[entityType] += count;
                                }
                                else
                                {
                                    counts[entityType] = count;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ConfigurationAuditService", "GetPendingChangesCountsAsync", "Error fetching pending changes counts", ex);
            }

            return counts;
        }
    }
}
