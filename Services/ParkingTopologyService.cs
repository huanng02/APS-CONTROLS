using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public class ParkingTopologyService
    {
        private static readonly Lazy<ParkingTopologyService> _lazy = new(() => new ParkingTopologyService());
        public static ParkingTopologyService Instance => _lazy.Value;

        private ParkingTopologyService() { }

        private List<LaneConfig>? _cachedLanes;
        private readonly object _lanesLock = new();

        public void InvalidateLanesCache()
        {
            lock (_lanesLock)
            {
                _cachedLanes = null;
            }
        }

        // ──────────────────────────────────────────────
        // READ Methods (Connection-Aware with Fallback)
        // ──────────────────────────────────────────────

        public List<ParkingSite> GetSites() => Task.Run(() => GetSitesAsync()).GetAwaiter().GetResult();

        public async Task<List<ParkingSite>> GetSitesAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<ParkingSite>>(
                "LIST_SITES",
                async conn =>
                {
                    var list = new List<ParkingSite>();
                    using (var cmd = new SqlCommand(@"SELECT Id, SiteCode, SiteName, Description, IsActive, CreatedUtc FROM dbo.ParkingSites ORDER BY SiteCode", conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new ParkingSite
                            {
                                Id = r.GetInt32(0),
                                SiteCode = r.GetString(1),
                                SiteName = r.GetString(2),
                                Description = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                                IsActive = r.GetBoolean(4),
                                CreatedUtc = r.GetDateTime(5)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<ParkingSite>();
        }

        public List<ParkingZone> GetZones() => Task.Run(() => GetZonesAsync()).GetAwaiter().GetResult();

        public async Task<List<ParkingZone>> GetZonesAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<ParkingZone>>(
                "LIST_ZONES",
                async conn =>
                {
                    var list = new List<ParkingZone>();
                    using (var cmd = new SqlCommand(@"
                        SELECT z.Id, z.SiteId, z.ZoneCode, z.ZoneName, z.Description, z.MaxCapacity, z.IsActive, z.CreatedUtc,
                               s.SiteCode, s.SiteName
                        FROM dbo.ParkingZones z
                        JOIN dbo.ParkingSites s ON z.SiteId = s.Id
                        ORDER BY z.ZoneCode", conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new ParkingZone
                            {
                                Id = r.GetInt32(0),
                                SiteId = r.GetInt32(1),
                                ZoneCode = r.GetString(2),
                                ZoneName = r.GetString(3),
                                Description = r.IsDBNull(4) ? string.Empty : r.GetString(4),
                                MaxCapacity = r.GetInt32(5),
                                IsActive = r.GetBoolean(6),
                                CreatedUtc = r.GetDateTime(7),
                                SiteCode = r.GetString(8),
                                SiteName = r.GetString(9)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<ParkingZone>();
        }

        public List<ParkingGate> GetGates() => Task.Run(() => GetGatesAsync()).GetAwaiter().GetResult();

        public async Task<List<ParkingGate>> GetGatesAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<ParkingGate>>(
                "LIST_GATES",
                async conn =>
                {
                    var list = new List<ParkingGate>();
                    using (var cmd = new SqlCommand(@"
                        SELECT g.Id, g.SiteId, g.GateCode, g.GateName, g.Description, g.IsActive, g.CreatedUtc,
                               s.SiteCode, s.SiteName
                        FROM dbo.ParkingGates g
                        JOIN dbo.ParkingSites s ON g.SiteId = s.Id
                        ORDER BY g.GateCode", conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new ParkingGate
                            {
                                Id = r.GetInt32(0),
                                SiteId = r.GetInt32(1),
                                GateCode = r.GetString(2),
                                GateName = r.GetString(3),
                                Description = r.IsDBNull(4) ? string.Empty : r.GetString(4),
                                IsActive = r.GetBoolean(5),
                                CreatedUtc = r.GetDateTime(6),
                                SiteCode = r.GetString(7),
                                SiteName = r.GetString(8)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<ParkingGate>();
        }

        public List<C3ControllerConfig> GetControllers() => Task.Run(() => GetControllersAsync()).GetAwaiter().GetResult();

        public async Task<List<C3ControllerConfig>> GetControllersAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<C3ControllerConfig>>(
                "LIST_CONTROLLERS",
                async conn =>
                {
                    var list = new List<C3ControllerConfig>();
                    using (var cmd = new SqlCommand(@"
                        SELECT c.Id, c.ControllerName, c.IpAddress, c.GateId, c.IsActive, c.CreatedUtc,
                               g.GateName, c.ServerIp, c.PcIp
                        FROM dbo.C3Controllers c
                        JOIN dbo.ParkingGates g ON c.GateId = g.Id
                        ORDER BY c.ControllerName", conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new C3ControllerConfig
                            {
                                Id = r.GetInt32(0),
                                ControllerName = r.GetString(1),
                                IpAddress = r.GetString(2),
                                GateId = r.IsDBNull(3) ? null : r.GetInt32(3),
                                IsActive = r.GetBoolean(4),
                                CreatedUtc = r.GetDateTime(5),
                                GateName = r.GetString(6),
                                ServerIp = r.IsDBNull(7) ? "127.0.0.1" : r.GetString(7),
                                PcIp = r.IsDBNull(8) ? "127.0.0.1" : r.GetString(8)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<C3ControllerConfig>();
        }

        public List<LaneConfig> GetLanes() => Task.Run(() => GetLanesAsync()).GetAwaiter().GetResult();

        public async Task<List<LaneConfig>> GetLanesAsync()
        {
            lock (_lanesLock)
            {
                if (_cachedLanes != null) return _cachedLanes;
            }

            var list = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<LaneConfig>>(
                "LIST_LANES",
                async conn =>
                {
                    var list = new List<LaneConfig>();
                    using (var cmd = new SqlCommand(@"
                        SELECT l.Id, l.LaneCode, l.LaneName, l.Direction, l.ZoneId, l.GateId, l.IsActive, l.CreatedUtc,
                                z.ZoneName, g.GateName, l.LoaiXeId, lx.TenLoai, l.DisplayIndex
                        FROM dbo.Lanes l
                        LEFT JOIN dbo.ParkingZones z ON l.ZoneId = z.Id
                        LEFT JOIN dbo.ParkingGates g ON l.GateId = g.Id
                        LEFT JOIN dbo.LoaiXe lx ON l.LoaiXeId = lx.Id
                        ORDER BY l.LaneCode", conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new LaneConfig
                            {
                                Id = r.GetInt32(0),
                                LaneCode = r.GetString(1),
                                LaneName = r.GetString(2),
                                Direction = r.GetString(3),
                                ZoneId = r.IsDBNull(4) ? null : r.GetInt32(4),
                                GateId = r.IsDBNull(5) ? null : r.GetInt32(5),
                                IsActive = r.GetBoolean(6),
                                CreatedUtc = r.GetDateTime(7),
                                ZoneName = r.IsDBNull(8) ? string.Empty : r.GetString(8),
                                GateName = r.IsDBNull(9) ? string.Empty : r.GetString(9),
                                LoaiXeId = r.IsDBNull(10) ? null : r.GetInt32(10),
                                LoaiXeName = r.IsDBNull(11) ? string.Empty : r.GetString(11),
                                DisplayIndex = r.IsDBNull(12) ? null : r.GetInt32(12)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<LaneConfig>();

            lock (_lanesLock)
            {
                _cachedLanes = list;
            }
            return list;
        }

        // ──────────────────────────────────────────────
        // TOPOLOGY RESOLUTION Methods
        // ──────────────────────────────────────────────

        public ParkingSite? GetSite(int id) => Task.Run(() => GetSiteAsync(id)).GetAwaiter().GetResult();

        public async Task<ParkingSite?> GetSiteAsync(int id)
        {
            var sites = await GetSitesAsync();
            return sites.FirstOrDefault(s => s.Id == id);
        }

        public ParkingZone? GetZone(int id) => Task.Run(() => GetZoneAsync(id)).GetAwaiter().GetResult();

        public async Task<ParkingZone?> GetZoneAsync(int id)
        {
            var zones = await GetZonesAsync();
            return zones.FirstOrDefault(z => z.Id == id);
        }

        public ParkingGate? GetGate(int id) => Task.Run(() => GetGateAsync(id)).GetAwaiter().GetResult();

        public async Task<ParkingGate?> GetGateAsync(int id)
        {
            var gates = await GetGatesAsync();
            return gates.FirstOrDefault(g => g.Id == id);
        }

        public ParkingZone? GetZoneByLane(int laneId) => Task.Run(() => GetZoneByLaneAsync(laneId)).GetAwaiter().GetResult();

        public async Task<ParkingZone?> GetZoneByLaneAsync(int laneId)
        {
            var lanes = await GetLanesAsync();
            var lane = lanes.FirstOrDefault(l => l.Id == laneId);
            if (lane == null || !lane.ZoneId.HasValue) return null;

            var zones = await GetZonesAsync();
            return zones.FirstOrDefault(z => z.Id == lane.ZoneId.Value);
        }

        public ParkingZone? GetZoneByReader(int readerNo) => Task.Run(() => GetZoneByReaderAsync(readerNo)).GetAwaiter().GetResult();

        public async Task<ParkingZone?> GetZoneByReaderAsync(int readerNo)
        {
            var mapping = ReaderLaneMappingService.Instance.GetMappingByReader(readerNo);

            if (mapping == null || !mapping.IsEnabled)
                return null;

            var lanes = await GetLanesAsync();

            var lane = lanes.FirstOrDefault(l => l.Id == mapping.LaneId);

            if (lane == null || !lane.ZoneId.HasValue)
                return null;

            var zones = await GetZonesAsync();

            return zones.FirstOrDefault(z => z.Id == lane.ZoneId.Value);
        }

        public async Task<LaneConfig> GetLaneByIdAsync(int laneId)
        {
            var lanes = await GetLanesAsync();
            return lanes.FirstOrDefault(x => x.Id == laneId);
        }

        public List<C3ControllerConfig> GetControllersByZone(int zoneId) => Task.Run(() => GetControllersByZoneAsync(zoneId)).GetAwaiter().GetResult();

        public async Task<List<C3ControllerConfig>> GetControllersByZoneAsync(int zoneId)
        {
            var controllers = await GetControllersAsync();
            return controllers.Where(c => c.GateId == zoneId).ToList(); // Map zoneId parameter to GateId for Settings window compatibility
        }

        public List<C3ControllerConfig> GetControllersByGate(int gateId) => Task.Run(() => GetControllersByGateAsync(gateId)).GetAwaiter().GetResult();

        public async Task<List<C3ControllerConfig>> GetControllersByGateAsync(int gateId)
        {
            var controllers = await GetControllersAsync();
            return controllers.Where(c => c.GateId == gateId).ToList();
        }

        public List<LaneConfig> GetLanesByGate(int gateId) => Task.Run(() => GetLanesByGateAsync(gateId)).GetAwaiter().GetResult();

        public async Task<List<LaneConfig>> GetLanesByGateAsync(int gateId)
        {
            var lanes = await GetLanesAsync();
            return lanes.Where(l => l.GateId == gateId).ToList();
        }

        // ──────────────────────────────────────────────
        // WRITE / CRUD Methods
        // ──────────────────────────────────────────────

        public async Task<bool> SaveSiteAsync(ParkingSite site)
        {
            bool isNew = site.Id == 0;
            ParkingSite? previous = null;
            if (!isNew)
            {
                try
                {
                    var sites = await GetSitesAsync();
                    var prevObj = sites.FirstOrDefault(s => s.Id == site.Id);
                    if (prevObj != null)
                    {
                        previous = new ParkingSite
                        {
                            Id = prevObj.Id,
                            SiteCode = prevObj.SiteCode,
                            SiteName = prevObj.SiteName,
                            Description = prevObj.Description,
                            IsActive = prevObj.IsActive
                        };
                    }
                }
                catch { }
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_SITE" : "UPDATE_SITE",
                site,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.ParkingSites (SiteCode, SiteName, Description, IsActive, CreatedUtc) OUTPUT INSERTED.Id VALUES (@code, @name, @desc, @active, @created)";
                    }
                    else
                    {
                        sql = "UPDATE dbo.ParkingSites SET SiteCode = @code, SiteName = @name, Description = @desc, IsActive = @active WHERE Id = @id";
                    }

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", site.Id);
                        cmd.Parameters.AddWithValue("@code", site.SiteCode);
                        cmd.Parameters.AddWithValue("@name", site.SiteName);
                        cmd.Parameters.AddWithValue("@desc", site.Description ?? string.Empty);
                        cmd.Parameters.AddWithValue("@active", site.IsActive);
                        cmd.Parameters.AddWithValue("@created", site.CreatedUtc);
                        
                        if (isNew)
                        {
                            var newId = await cmd.ExecuteScalarAsync();
                            if (newId != null) site.Id = Convert.ToInt32(newId);
                        }
                        else
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                async () =>
                {
                    // Update SQLite Cache
                    var sites = await GetSitesAsync();
                    if (isNew)
                    {
                        site.Id = sites.Any() ? sites.Max(s => s.Id) + 1 : 1;
                        sites.Add(site);
                    }
                    else
                    {
                        var existing = sites.FirstOrDefault(s => s.Id == site.Id);
                        if (existing != null)
                        {
                            existing.SiteCode = site.SiteCode;
                            existing.SiteName = site.SiteName;
                            existing.Description = site.Description;
                            existing.IsActive = site.IsActive;
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_SITES", sites);
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Site", previous, site); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        isNew ? "CREATE_SITE" : "UPDATE_SITE",
                        "ParkingSite",
                        site.Id.ToString(),
                        oldValues: previous,
                        newValues: site,
                        details: isNew ? $"Thêm site mới: {site.SiteName} ({site.SiteCode})" : $"Cập nhật site ID {site.Id}: {site.SiteName} ({site.SiteCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> DeleteSiteAsync(int id)
        {
            var zones = await GetZonesAsync();
            if (zones.Any(z => z.SiteId == id))
            {
                throw new Exception("Không thể xóa Site này vì vẫn còn Zone (khu vực) trực thuộc. Vui lòng xóa các Zone trước.");
            }

            ParkingSite? previous = null;
            try
            {
                var sites = await GetSitesAsync();
                var prevObj = sites.FirstOrDefault(s => s.Id == id);
                if (prevObj != null)
                {
                    previous = new ParkingSite
                    {
                        Id = prevObj.Id,
                        SiteCode = prevObj.SiteCode,
                        SiteName = prevObj.SiteName,
                        Description = prevObj.Description,
                        IsActive = prevObj.IsActive
                    };
                }
            }
            catch { }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_SITE",
                new { Id = id },
                async conn =>
                {
                    // 1. Clean up legacy ParkingTopologies table if it exists
                    using (var cmd = new SqlCommand(@"
                        IF OBJECT_ID('dbo.ParkingTopologies', 'U') IS NOT NULL
                        BEGIN
                            BEGIN TRY
                                DROP TABLE dbo.ParkingTopologies;
                            END TRY
                            BEGIN CATCH
                                -- If drop fails (e.g. referenced by other tables), try dropping the FK and updating
                                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ParkingTopologies_Site')
                                BEGIN
                                    ALTER TABLE dbo.ParkingTopologies DROP CONSTRAINT FK_ParkingTopologies_Site;
                                END
                                
                                BEGIN TRY
                                    EXEC sp_executesql N'UPDATE dbo.ParkingTopologies SET SiteId = NULL WHERE SiteId = @id', N'@id INT', @id;
                                END TRY
                                BEGIN CATCH
                                END CATCH
                            END CATCH
                        END", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Nullify references in transaction tables
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.VehicleSessions SET SiteId = NULL WHERE SiteId = @id;
                        UPDATE dbo.XeTrongBai SET SiteId = NULL WHERE SiteId = @id;
                        UPDATE dbo.LichSuXe SET SiteId = NULL WHERE SiteId = @id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 3. Delete the site
                    using (var cmd = new SqlCommand(@"DELETE FROM dbo.ParkingSites WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    // Nullify references in SQLite VehicleSessions
                    string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
                    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Default Timeout=5;"))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(@"
                            UPDATE VehicleSessions SET SiteId = NULL WHERE SiteId = @id;
                            UPDATE XeTrongBai SET SiteId = NULL WHERE SiteId = @id;
                            UPDATE LichSuXe SET SiteId = NULL WHERE SiteId = @id;", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    var sites = await GetSitesAsync();
                    var existing = sites.FirstOrDefault(s => s.Id == id);
                    if (existing != null)
                    {
                        sites.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_SITES", sites);
                    }
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Site", previous, null); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        "DELETE_SITE",
                        "ParkingSite",
                        id.ToString(),
                        oldValues: previous,
                        newValues: null,
                        details: $"Xóa site ID {id}: {previous?.SiteName} ({previous?.SiteCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> SaveZoneAsync(ParkingZone zone)
        {
            bool isNew = zone.Id == 0;
            ParkingZone? previous = null;
            if (!isNew)
            {
                try
                {
                    var zones = await GetZonesAsync();
                    var prevObj = zones.FirstOrDefault(z => z.Id == zone.Id);
                    if (prevObj != null)
                    {
                        previous = new ParkingZone
                        {
                            Id = prevObj.Id,
                            SiteId = prevObj.SiteId,
                            ZoneCode = prevObj.ZoneCode,
                            ZoneName = prevObj.ZoneName,
                            Description = prevObj.Description,
                            MaxCapacity = prevObj.MaxCapacity,
                            IsActive = prevObj.IsActive
                        };
                    }
                }
                catch { }
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_ZONE" : "UPDATE_ZONE",
                zone,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.ParkingZones (SiteId, ZoneCode, ZoneName, Description, MaxCapacity, IsActive, CreatedUtc) OUTPUT INSERTED.Id VALUES (@siteId, @code, @name, @desc, @cap, @active, @created)";
                    }
                    else
                    {
                        sql = "UPDATE dbo.ParkingZones SET SiteId = @siteId, ZoneCode = @code, ZoneName = @name, Description = @desc, MaxCapacity = @cap, IsActive = @active WHERE Id = @id";
                    }

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", zone.Id);
                        cmd.Parameters.AddWithValue("@siteId", zone.SiteId);
                        cmd.Parameters.AddWithValue("@code", zone.ZoneCode);
                        cmd.Parameters.AddWithValue("@name", zone.ZoneName);
                        cmd.Parameters.AddWithValue("@desc", zone.Description ?? string.Empty);
                        cmd.Parameters.AddWithValue("@cap", zone.MaxCapacity);
                        cmd.Parameters.AddWithValue("@active", zone.IsActive);
                        cmd.Parameters.AddWithValue("@created", zone.CreatedUtc);
                        
                        if (isNew)
                        {
                            var newId = await cmd.ExecuteScalarAsync();
                            LoggingService.Instance.LogInfo("SaveZone", "Insert", $"ExecuteScalarAsync returned: {newId}");
                            if (newId != null && newId != DBNull.Value) zone.Id = Convert.ToInt32(newId);
                        }
                        else
                        {
                            int rows = await cmd.ExecuteNonQueryAsync();
                            LoggingService.Instance.LogInfo("SaveZone", "Update", $"ExecuteNonQueryAsync updated rows: {rows}");
                        }
                    }
                },
                async () =>
                {
                    var zones = await GetZonesAsync();
                    if (isNew)
                    {
                        zone.Id = zones.Any() ? zones.Max(z => z.Id) + 1 : 1;
                        var site = await GetSiteAsync(zone.SiteId);
                        if (site != null)
                        {
                            zone.SiteCode = site.SiteCode;
                            zone.SiteName = site.SiteName;
                        }
                        zones.Add(zone);
                    }
                    else
                    {
                        var existing = zones.FirstOrDefault(z => z.Id == zone.Id);
                        if (existing != null)
                        {
                            existing.SiteId = zone.SiteId;
                            existing.ZoneCode = zone.ZoneCode;
                            existing.ZoneName = zone.ZoneName;
                            existing.Description = zone.Description;
                            existing.MaxCapacity = zone.MaxCapacity;
                            existing.IsActive = zone.IsActive;
                            var site = await GetSiteAsync(zone.SiteId);
                            if (site != null)
                            {
                                existing.SiteCode = site.SiteCode;
                                existing.SiteName = site.SiteName;
                            }
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_ZONES", zones);
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Zone", previous, zone); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        isNew ? "CREATE_ZONE" : "UPDATE_ZONE",
                        "ParkingZone",
                        zone.Id.ToString(),
                        oldValues: previous,
                        newValues: zone,
                        details: isNew ? $"Thêm zone mới: {zone.ZoneName} ({zone.ZoneCode})" : $"Cập nhật zone ID {zone.Id}: {zone.ZoneName} ({zone.ZoneCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> DeleteZoneAsync(int id)
        {
            var lanes = await GetLanesAsync();
            if (lanes.Any(l => l.ZoneId == id))
            {
                throw new Exception("Không thể xóa Zone này vì vẫn còn Làn (Lane) trực thuộc.");
            }
            var controllers = await GetControllersAsync();
            if (controllers.Any(c => c.ZoneId == id))
            {
                throw new Exception("Không thể xóa Zone này vì vẫn còn Tủ điều khiển (Controller) trực thuộc.");
            }

            ParkingZone? previous = null;
            try
            {
                var zones = await GetZonesAsync();
                var prevObj = zones.FirstOrDefault(z => z.Id == id);
                if (prevObj != null)
                {
                    previous = new ParkingZone
                    {
                        Id = prevObj.Id,
                        SiteId = prevObj.SiteId,
                        ZoneCode = prevObj.ZoneCode,
                        ZoneName = prevObj.ZoneName,
                        Description = prevObj.Description,
                        MaxCapacity = prevObj.MaxCapacity,
                        IsActive = prevObj.IsActive
                    };
                }
            }
            catch { }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_ZONE",
                new { Id = id },
                async conn =>
                {
                    // 1. Clean up legacy ParkingTopologies table if it exists (for 'BaiXe' DB)
                    using (var cmd = new SqlCommand(@"
                        IF OBJECT_ID('dbo.ParkingTopologies', 'U') IS NOT NULL
                        BEGIN
                            BEGIN TRY
                                DROP TABLE dbo.ParkingTopologies;
                            END TRY
                            BEGIN CATCH
                                -- If drop fails (e.g. referenced by other tables), try dropping the FK and updating
                                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ParkingTopologies_Zone')
                                BEGIN
                                    ALTER TABLE dbo.ParkingTopologies DROP CONSTRAINT FK_ParkingTopologies_Zone;
                                END
                                
                                BEGIN TRY
                                    EXEC sp_executesql N'UPDATE dbo.ParkingTopologies SET ZoneId = NULL WHERE ZoneId = @id', N'@id INT', @id;
                                END TRY
                                BEGIN CATCH
                                END CATCH
                            END CATCH
                        END", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Nullify references in transaction tables
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.VehicleSessions SET ZoneId = NULL WHERE ZoneId = @id;
                        UPDATE dbo.XeTrongBai SET ZoneId = NULL WHERE ZoneId = @id;
                        UPDATE dbo.LichSuXe SET ZoneId = NULL WHERE ZoneId = @id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 3. Delete the zone
                    using (var cmd = new SqlCommand(@"DELETE FROM dbo.ParkingZones WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    // Nullify references in SQLite VehicleSessions
                    string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
                    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Default Timeout=5;"))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(@"
                            UPDATE VehicleSessions SET ZoneId = NULL WHERE ZoneId = @id;
                            UPDATE XeTrongBai SET ZoneId = NULL WHERE ZoneId = @id;
                            UPDATE LichSuXe SET ZoneId = NULL WHERE ZoneId = @id;", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    var zones = await GetZonesAsync();
                    var existing = zones.FirstOrDefault(z => z.Id == id);
                    if (existing != null)
                    {
                        zones.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_ZONES", zones);
                    }
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Zone", previous, null); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        "DELETE_ZONE",
                        "ParkingZone",
                        id.ToString(),
                        oldValues: previous,
                        newValues: null,
                        details: $"Xóa zone ID {id}: {previous?.ZoneName} ({previous?.ZoneCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> SaveGateAsync(ParkingGate gate)
        {
            bool isNew = gate.Id == 0;
            ParkingGate? previous = null;
            if (!isNew)
            {
                try
                {
                    var gates = await GetGatesAsync();
                    var prevObj = gates.FirstOrDefault(g => g.Id == gate.Id);
                    if (prevObj != null)
                    {
                        previous = new ParkingGate
                        {
                            Id = prevObj.Id,
                            SiteId = prevObj.SiteId,
                            GateCode = prevObj.GateCode,
                            GateName = prevObj.GateName,
                            Description = prevObj.Description,
                            IsActive = prevObj.IsActive
                        };
                    }
                }
                catch { }
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_GATE" : "UPDATE_GATE",
                gate,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.ParkingGates (SiteId, GateCode, GateName, Description, IsActive, CreatedUtc) OUTPUT INSERTED.Id VALUES (@siteId, @code, @name, @desc, @active, @created)";
                    }
                    else
                    {
                        sql = "UPDATE dbo.ParkingGates SET SiteId = @siteId, GateCode = @code, GateName = @name, Description = @desc, IsActive = @active WHERE Id = @id";
                    }

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", gate.Id);
                        cmd.Parameters.AddWithValue("@siteId", gate.SiteId);
                        cmd.Parameters.AddWithValue("@code", gate.GateCode);
                        cmd.Parameters.AddWithValue("@name", gate.GateName);
                        cmd.Parameters.AddWithValue("@desc", gate.Description ?? string.Empty);
                        cmd.Parameters.AddWithValue("@active", gate.IsActive);
                        cmd.Parameters.AddWithValue("@created", gate.CreatedUtc);
                        
                        if (isNew)
                        {
                            var newId = await cmd.ExecuteScalarAsync();
                            if (newId != null && newId != DBNull.Value) gate.Id = Convert.ToInt32(newId);
                        }
                        else
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                async () =>
                {
                    var gates = await GetGatesAsync();
                    if (isNew)
                    {
                        gate.Id = gates.Any() ? gates.Max(g => g.Id) + 1 : 1;
                        var site = await GetSiteAsync(gate.SiteId);
                        if (site != null)
                        {
                            gate.SiteCode = site.SiteCode;
                            gate.SiteName = site.SiteName;
                        }
                        gates.Add(gate);
                    }
                    else
                    {
                        var existing = gates.FirstOrDefault(g => g.Id == gate.Id);
                        if (existing != null)
                        {
                            existing.SiteId = gate.SiteId;
                            existing.GateCode = gate.GateCode;
                            existing.GateName = gate.GateName;
                            existing.Description = gate.Description;
                            existing.IsActive = gate.IsActive;
                            var site = await GetSiteAsync(gate.SiteId);
                            if (site != null)
                            {
                                existing.SiteCode = site.SiteCode;
                                existing.SiteName = site.SiteName;
                            }
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_GATES", gates);
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Gate", previous, gate); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        isNew ? "CREATE_GATE" : "UPDATE_GATE",
                        "ParkingGate",
                        gate.Id.ToString(),
                        oldValues: previous,
                        newValues: gate,
                        details: isNew ? $"Thêm cổng mới: {gate.GateName} ({gate.GateCode})" : $"Cập nhật cổng ID {gate.Id}: {gate.GateName} ({gate.GateCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> DeleteGateAsync(int id)
        {
            var lanes = await GetLanesAsync();
            if (lanes.Any(l => l.GateId == id))
            {
                throw new Exception("Không thể xóa Cổng này vì vẫn còn Làn (Lane) trực thuộc.");
            }
            var controllers = await GetControllersAsync();
            if (controllers.Any(c => c.GateId == id))
            {
                throw new Exception("Không thể xóa Cổng này vì vẫn còn Tủ điều khiển (Controller) trực thuộc.");
            }

            ParkingGate? previous = null;
            try
            {
                var gates = await GetGatesAsync();
                var prevObj = gates.FirstOrDefault(g => g.Id == id);
                if (prevObj != null)
                {
                    previous = new ParkingGate
                    {
                        Id = prevObj.Id,
                        SiteId = prevObj.SiteId,
                        GateCode = prevObj.GateCode,
                        GateName = prevObj.GateName,
                        Description = prevObj.Description,
                        IsActive = prevObj.IsActive
                    };
                }
            }
            catch { }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_GATE",
                new { Id = id },
                async conn =>
                {
                    string sql = "DELETE FROM dbo.ParkingGates WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var gates = await GetGatesAsync();
                    var existing = gates.FirstOrDefault(g => g.Id == id);
                    if (existing != null)
                    {
                        gates.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_GATES", gates);
                    }
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Gate", previous, null); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        "DELETE_GATE",
                        "ParkingGate",
                        id.ToString(),
                        oldValues: previous,
                        newValues: null,
                        details: $"Xóa cổng ID {id}: {previous?.GateName} ({previous?.GateCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> SaveControllerAsync(C3ControllerConfig controller)
        {
            bool isNew = controller.Id == 0;
            C3ControllerConfig? previous = null;
            if (!isNew)
            {
                try
                {
                    var controllers = await GetControllersAsync();
                    var prevObj = controllers.FirstOrDefault(c => c.Id == controller.Id);
                    if (prevObj != null)
                    {
                        previous = new C3ControllerConfig
                        {
                            Id = prevObj.Id,
                            ControllerName = prevObj.ControllerName,
                            IpAddress = prevObj.IpAddress,
                            ServerIp = prevObj.ServerIp,
                            PcIp = prevObj.PcIp,
                            GateId = prevObj.GateId,
                            ZoneId = prevObj.ZoneId,
                            IsActive = prevObj.IsActive
                        };
                    }
                }
                catch { }
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_CONTROLLER" : "UPDATE_CONTROLLER",
                controller,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.C3Controllers (ControllerName, IpAddress, ServerIp, PcIp, GateId, IsActive, CreatedUtc) OUTPUT INSERTED.Id VALUES (@name, @ip, @serverIp, @pcIp, @gateId, @active, @created)";
                    }
                    else
                    {
                        sql = "UPDATE dbo.C3Controllers SET ControllerName = @name, IpAddress = @ip, ServerIp = @serverIp, PcIp = @pcIp, GateId = @gateId, IsActive = @active WHERE Id = @id";
                    }

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", controller.Id);
                        cmd.Parameters.AddWithValue("@name", controller.ControllerName);
                        cmd.Parameters.AddWithValue("@ip", controller.IpAddress);
                        cmd.Parameters.AddWithValue("@serverIp", controller.ServerIp ?? "127.0.0.1");
                        cmd.Parameters.AddWithValue("@pcIp", controller.PcIp ?? "127.0.0.1");
                        cmd.Parameters.AddWithValue("@gateId", (object?)controller.GateId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@active", controller.IsActive);
                        cmd.Parameters.AddWithValue("@created", controller.CreatedUtc);
                        
                        if (isNew)
                        {
                            var newId = await cmd.ExecuteScalarAsync();
                            if (newId != null) controller.Id = Convert.ToInt32(newId);
                        }
                        else
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                async () =>
                {
                    var controllers = await GetControllersAsync();
                    if (isNew)
                    {
                        controller.Id = controllers.Any() ? controllers.Max(c => c.Id) + 1 : 1;
                        if (controller.GateId.HasValue)
                        {
                            var gate = await GetGateAsync(controller.GateId.Value);
                            if (gate != null)
                            {
                                controller.GateName = gate.GateName;
                            }
                        }
                        controllers.Add(controller);
                    }
                    else
                    {
                        var existing = controllers.FirstOrDefault(c => c.Id == controller.Id);
                        if (existing != null)
                        {
                            existing.ControllerName = controller.ControllerName;
                            existing.IpAddress = controller.IpAddress;
                            existing.ServerIp = controller.ServerIp;
                            existing.PcIp = controller.PcIp;
                            existing.GateId = controller.GateId;
                            existing.IsActive = controller.IsActive;
                            if (controller.GateId.HasValue)
                            {
                                var gate = await GetGateAsync(controller.GateId.Value);
                                if (gate != null)
                                {
                                    existing.GateName = gate.GateName;
                                }
                            }
                            else
                            {
                                existing.GateName = string.Empty;
                            }
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_CONTROLLERS", controllers);
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Controller", previous, controller); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        isNew ? "CREATE_CONTROLLER" : "UPDATE_CONTROLLER",
                        "C3Controller",
                        controller.Id.ToString(),
                        oldValues: previous,
                        newValues: controller,
                        details: isNew ? $"Thêm bộ điều khiển mới: {controller.ControllerName} ({controller.IpAddress})" : $"Cập nhật bộ điều khiển ID {controller.Id}: {controller.ControllerName} ({controller.IpAddress})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> DeleteControllerAsync(int id)
        {
            C3ControllerConfig? previous = null;
            try
            {
                var controllers = await GetControllersAsync();
                var prevObj = controllers.FirstOrDefault(c => c.Id == id);
                if (prevObj != null)
                {
                    previous = new C3ControllerConfig
                    {
                        Id = prevObj.Id,
                        ControllerName = prevObj.ControllerName,
                        IpAddress = prevObj.IpAddress,
                        ServerIp = prevObj.ServerIp,
                        PcIp = prevObj.PcIp,
                        GateId = prevObj.GateId,
                        ZoneId = prevObj.ZoneId,
                        IsActive = prevObj.IsActive
                    };
                }
            }
            catch { }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_CONTROLLER",
                new { Id = id },
                async conn =>
                {
                    string sql = "DELETE FROM dbo.C3Controllers WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var controllers = await GetControllersAsync();
                    var existing = controllers.FirstOrDefault(c => c.Id == id);
                    if (existing != null)
                    {
                        controllers.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_CONTROLLERS", controllers);
                    }
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Controller", previous, null); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        "DELETE_CONTROLLER",
                        "C3Controller",
                        id.ToString(),
                        oldValues: previous,
                        newValues: null,
                        details: $"Xóa bộ điều khiển ID {id}: {previous?.ControllerName} ({previous?.IpAddress})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> SaveLaneAsync(LaneConfig lane)
        {
            if (lane == null) return false;

            // Enforce direction consistency rules
            var analysis = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(lane.Id);
            var targetDir = lane.Direction.ToLaneDirection();
            if ((lane.Direction == "IN" || lane.Direction == "OUT") && !analysis.AllowedDirections.Contains(targetDir))
            {
                throw new InvalidOperationException($"Không thể lưu làn ở trạng thái {lane.Direction} do cấu hình đầu đọc không nhất quán (tất cả đầu đọc của làn này đều là chiều ngược lại).");
            }

            bool isNew = lane.Id == 0;
            LaneConfig? previous = null;
            if (!isNew)
            {
                try
                {
                    var lanes = await GetLanesAsync();
                    var prevObj = lanes.FirstOrDefault(l => l.Id == lane.Id);
                    if (prevObj != null)
                    {
                        previous = new LaneConfig
                        {
                            Id = prevObj.Id,
                            LaneCode = prevObj.LaneCode,
                            LaneName = prevObj.LaneName,
                            Direction = prevObj.Direction,
                            ZoneId = prevObj.ZoneId,
                            GateId = prevObj.GateId,
                            LoaiXeId = prevObj.LoaiXeId,
                            IsActive = prevObj.IsActive
                        };
                    }
                }
                catch { }
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_LANE" : "UPDATE_LANE",
                lane,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.Lanes (LaneCode, LaneName, Direction, ZoneId, GateId, LoaiXeId, IsActive, CreatedUtc, DisplayIndex) OUTPUT INSERTED.Id VALUES (@code, @name, @dir, @zoneId, @gateId, @loaiXeId, @active, @created, @displayIndex)";
                    }
                    else
                    {
                        sql = "UPDATE dbo.Lanes SET LaneCode = @code, LaneName = @name, Direction = @dir, ZoneId = @zoneId, GateId = @gateId, LoaiXeId = @loaiXeId, IsActive = @active, DisplayIndex = @displayIndex WHERE Id = @id";
                    }

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", lane.Id);
                        cmd.Parameters.AddWithValue("@code", lane.LaneCode);
                        cmd.Parameters.AddWithValue("@name", lane.LaneName);
                        cmd.Parameters.AddWithValue("@dir", lane.Direction);
                        cmd.Parameters.AddWithValue("@zoneId", (object?)lane.ZoneId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@gateId", (object?)lane.GateId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@loaiXeId", (object?)lane.LoaiXeId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@active", lane.IsActive);
                        cmd.Parameters.AddWithValue("@created", lane.CreatedUtc);
                        cmd.Parameters.AddWithValue("@displayIndex", (object?)lane.DisplayIndex ?? DBNull.Value);
                        
                        if (isNew)
                        {
                            var newId = await cmd.ExecuteScalarAsync();
                            if (newId != null) lane.Id = Convert.ToInt32(newId);
                        }
                        else
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                async () =>
                {
                    if (lane.LoaiXeId.HasValue)
                    {
                        var vehicleTypes = await new DatabaseService().GetLoaiXeAsync();
                        var vt = vehicleTypes?.FirstOrDefault(x => x.Id == lane.LoaiXeId.Value);
                        lane.LoaiXeName = vt?.TenLoai ?? string.Empty;
                    }
                    else
                    {
                        lane.LoaiXeName = string.Empty;
                    }

                    var lanes = await GetLanesAsync();
                    if (isNew)
                    {
                        lane.Id = lanes.Any() ? lanes.Max(l => l.Id) + 1 : 1;
                        if (lane.ZoneId.HasValue)
                        {
                            var zone = await GetZoneAsync(lane.ZoneId.Value);
                            if (zone != null) lane.ZoneName = zone.ZoneName;
                        }
                        if (lane.GateId.HasValue)
                        {
                            var gate = await GetGateAsync(lane.GateId.Value);
                            if (gate != null) lane.GateName = gate.GateName;
                        }
                        lanes.Add(lane);
                    }
                    else
                    {
                        var existing = lanes.FirstOrDefault(l => l.Id == lane.Id);
                        if (existing != null)
                        {
                            existing.LaneCode = lane.LaneCode;
                            existing.LaneName = lane.LaneName;
                            existing.Direction = lane.Direction;
                            existing.ZoneId = lane.ZoneId;
                            existing.GateId = lane.GateId;
                            existing.LoaiXeId = lane.LoaiXeId;
                            existing.IsActive = lane.IsActive;
                            if (lane.ZoneId.HasValue)
                            {
                                var zone = await GetZoneAsync(lane.ZoneId.Value);
                                if (zone != null) existing.ZoneName = zone.ZoneName;
                            }
                            else
                            {
                                existing.ZoneName = string.Empty;
                            }
                            if (lane.GateId.HasValue)
                            {
                                var gate = await GetGateAsync(lane.GateId.Value);
                                if (gate != null) existing.GateName = gate.GateName;
                            }
                            else
                            {
                                existing.GateName = string.Empty;
                            }
                            existing.LoaiXeName = lane.LoaiXeName;
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_LANES", lanes);
                    try
                    {
                        LaneRuntimeManager.Instance.SetLaneDirection(lane.Id, lane.Direction);
                    }
                    catch { }
                }
            );

            if (success)
            {
                InvalidateLanesCache();
                try
                {
                    EventBus.Instance.InvalidateTopologyCache();
                }
                catch { }

                try
                {
                    LaneRuntimeManager.Instance.SetLaneDirection(lane.Id, lane.Direction);
                }
                catch { }

                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Lane", previous, lane); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        isNew ? "CREATE_LANE" : "UPDATE_LANE",
                        "Lane",
                        lane.Id.ToString(),
                        oldValues: previous,
                        newValues: lane,
                        details: isNew ? $"Thêm làn mới: {lane.LaneName} ({lane.LaneCode})" : $"Cập nhật làn ID {lane.Id}: {lane.LaneName} ({lane.LaneCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> DeleteLaneAsync(int id)
        {
            LaneConfig? previous = null;
            try
            {
                var lanes = await GetLanesAsync();
                var prevObj = lanes.FirstOrDefault(l => l.Id == id);
                if (prevObj != null)
                {
                    previous = new LaneConfig
                    {
                        Id = prevObj.Id,
                        LaneCode = prevObj.LaneCode,
                        LaneName = prevObj.LaneName,
                        Direction = prevObj.Direction,
                        ZoneId = prevObj.ZoneId,
                        GateId = prevObj.GateId,
                        LoaiXeId = prevObj.LoaiXeId,
                        IsActive = prevObj.IsActive
                    };
                }
            }
            catch { }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_LANE",
                new { Id = id },
                async conn =>
                {
                    // 1. Nullify references in transaction tables
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.VehicleSessions SET EntryLaneId = NULL WHERE EntryLaneId = @id;
                        UPDATE dbo.VehicleSessions SET ExitLaneId = NULL WHERE ExitLaneId = @id;
                        UPDATE dbo.XeTrongBai SET EntryLaneId = NULL WHERE EntryLaneId = @id;
                        UPDATE dbo.LichSuXe SET EntryLaneId = NULL WHERE EntryLaneId = @id;
                        UPDATE dbo.LichSuXe SET ExitLaneId = NULL WHERE ExitLaneId = @id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Delete the lane
                    using (var cmd = new SqlCommand("DELETE FROM dbo.Lanes WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    // Nullify references in SQLite VehicleSessions
                    string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
                    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Default Timeout=5;"))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(@"
                            UPDATE VehicleSessions SET EntryLaneId = NULL WHERE EntryLaneId = @id;
                            UPDATE VehicleSessions SET ExitLaneId = NULL WHERE ExitLaneId = @id;
                            UPDATE XeTrongBai SET EntryLaneId = NULL WHERE EntryLaneId = @id;
                            UPDATE LichSuXe SET EntryLaneId = NULL WHERE EntryLaneId = @id;
                            UPDATE LichSuXe SET ExitLaneId = NULL WHERE ExitLaneId = @id;", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    var lanes = await GetLanesAsync();
                    var existing = lanes.FirstOrDefault(l => l.Id == id);
                    if (existing != null)
                    {
                        lanes.Remove(existing);
                        // Update cache
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_LANES", lanes);
                    }

                    // Remove from mappings
                    ReaderLaneMappingService.Instance.RemoveMappingsByLane(id);
                }
            );

            if (success)
            {
                InvalidateLanesCache();
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Lane", previous, null); } catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        "DELETE_LANE",
                        "Lane",
                        id.ToString(),
                        oldValues: previous,
                        newValues: null,
                        details: $"Xóa làn ID {id}: {previous?.LaneName} ({previous?.LaneCode})",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> AssignLaneToZoneAsync(int laneId, int? zoneId)
        {
            var lanes = await GetLanesAsync();
            var prevLane = lanes.FirstOrDefault(l => l.Id == laneId);
            string laneName = prevLane?.LaneName ?? $"Làn ID {laneId}";
            string oldZoneName = prevLane?.ZoneName ?? "Chưa gán";

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "ASSIGN_LANE_ZONE",
                new { LaneId = laneId, ZoneId = zoneId },
                async conn =>
                {
                    using (var cmd = new SqlCommand(@"UPDATE dbo.Lanes SET ZoneId = @zoneId WHERE Id = @laneId", conn))
                    {
                        cmd.Parameters.AddWithValue("@laneId", laneId);
                        cmd.Parameters.AddWithValue("@zoneId", (object?)zoneId ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var lanesList = await GetLanesAsync();
                    var lane = lanesList.FirstOrDefault(l => l.Id == laneId);
                    if (lane != null)
                    {
                        lane.ZoneId = zoneId;
                        if (zoneId.HasValue)
                        {
                            var zone = await GetZoneAsync(zoneId.Value);
                            lane.ZoneName = zone?.ZoneName ?? string.Empty;
                        }
                        else
                        {
                            lane.ZoneName = string.Empty;
                        }
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_LANES", lanesList);
                    }
                }
            );

            if (success)
            {
                InvalidateLanesCache();
                try
                {
                    string newZoneName = zoneId.HasValue ? (await GetZoneAsync(zoneId.Value))?.ZoneName ?? "Không xác định" : "Chưa gán";
                    await ConfigurationAuditService.Instance.RecordChangeAsync("Lane", laneName, "Zone", oldZoneName, newZoneName);
                }
                catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        "ASSIGN_LANE_ZONE",
                        "Lane",
                        laneId.ToString(),
                        oldValues: null,
                        newValues: new { ZoneId = zoneId },
                        details: $"Gán làn ID {laneId} vào Zone ID: {zoneId}",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        public async Task<bool> AssignLaneToGateAsync(int laneId, int? gateId)
        {
            var lanes = await GetLanesAsync();
            var prevLane = lanes.FirstOrDefault(l => l.Id == laneId);
            string laneName = prevLane?.LaneName ?? $"Làn ID {laneId}";
            string oldGateName = prevLane?.GateName ?? "Chưa gán";

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "ASSIGN_LANE_GATE",
                new { LaneId = laneId, GateId = gateId },
                async conn =>
                {
                    using (var cmd = new SqlCommand(@"UPDATE dbo.Lanes SET GateId = @gateId WHERE Id = @laneId", conn))
                    {
                        cmd.Parameters.AddWithValue("@laneId", laneId);
                        cmd.Parameters.AddWithValue("@gateId", (object?)gateId ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var lanesList = await GetLanesAsync();
                    var lane = lanesList.FirstOrDefault(l => l.Id == laneId);
                    if (lane != null)
                    {
                        lane.GateId = gateId;
                        if (gateId.HasValue)
                        {
                            var gate = await GetGateAsync(gateId.Value);
                            lane.GateName = gate?.GateName ?? string.Empty;
                        }
                        else
                        {
                            lane.GateName = string.Empty;
                        }
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_LANES", lanesList);
                    }
                }
            );

            if (success)
            {
                InvalidateLanesCache();
                try
                {
                    string newGateName = gateId.HasValue ? (await GetGateAsync(gateId.Value))?.GateName ?? "Không xác định" : "Chưa gán";
                    await ConfigurationAuditService.Instance.RecordChangeAsync("Lane", laneName, "Gate", oldGateName, newGateName);
                }
                catch { }
                try
                {
                    LoggingService.Instance.LogCrud(
                        "ASSIGN_LANE_GATE",
                        "Lane",
                        laneId.ToString(),
                        oldValues: null,
                        newValues: new { GateId = gateId },
                        details: $"Gán làn ID {laneId} vào Cổng ID: {gateId}",
                        source: "ParkingTopologyService"
                    );
                }
                catch { }
            }

            return success;
        }

        // ──────────────────────────────────────────────
        // QA Simulation Helper Methods
        // ──────────────────────────────────────────────

        public async Task<bool> SimulateVehicleEntryAsync(int cardId, string plate, int laneId)
        {
            var lanes = await GetLanesAsync();
            var lane = lanes.FirstOrDefault(l => l.Id == laneId || l.LaneCode == $"LANE-{laneId}");
            if (lane == null) return false;

            int? zoneId = lane.ZoneId;
            int? siteId = null;

            if (zoneId.HasValue)
            {
                var zone = await GetZoneAsync(zoneId.Value);
                if (zone != null)
                {
                    siteId = zone.SiteId;
                }
            }
            else if (lane.GateId.HasValue)
            {
                var gates = await GetGatesAsync();
                var gate = gates.FirstOrDefault(g => g.Id == lane.GateId.Value);
                if (gate != null)
                {
                    siteId = gate.SiteId;
                    var zones = await GetZonesAsync();
                    var defaultZone = zones.FirstOrDefault(z => z.SiteId == siteId);
                    if (defaultZone != null)
                    {
                        zoneId = defaultZone.Id;
                    }
                }
            }

            if (!zoneId.HasValue || !siteId.HasValue) return false;

            var session = new VehicleSession
            {
                CardId = cardId,
                BienSo = plate,
                ThoiGianVao = DateTime.Now,
                SiteId = siteId,
                ZoneId = zoneId,
                EntryLaneId = lane.Id,
                TrangThai = "Active"
            };

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "VEHICLE_ENTRY",
                session,
                async conn =>
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.VehicleSessions (CardId, BienSo, ThoiGianVao, SiteId, ZoneId, EntryLaneId, TrangThai)
                        VALUES (@cardId, @plate, @time, @siteId, @zoneId, @laneId, 'Active')", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", session.CardId);
                        cmd.Parameters.AddWithValue("@plate", session.BienSo);
                        cmd.Parameters.AddWithValue("@time", session.ThoiGianVao);
                        cmd.Parameters.AddWithValue("@siteId", session.SiteId);
                        cmd.Parameters.AddWithValue("@zoneId", session.ZoneId);
                        cmd.Parameters.AddWithValue("@laneId", session.EntryLaneId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    // Write strictly to SQLite
                    string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
                    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Default Timeout=5;"))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(@"
                            INSERT INTO VehicleSessions (CardId, BienSo, ThoiGianVao, SiteId, ZoneId, EntryLaneId, TrangThai)
                            VALUES (@cardId, @plate, @time, @siteId, @zoneId, @laneId, 'Active')", conn))
                        {
                            cmd.Parameters.AddWithValue("@cardId", session.CardId);
                            cmd.Parameters.AddWithValue("@plate", session.BienSo);
                            cmd.Parameters.AddWithValue("@time", session.ThoiGianVao.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@siteId", session.SiteId);
                            cmd.Parameters.AddWithValue("@zoneId", session.ZoneId);
                            cmd.Parameters.AddWithValue("@laneId", session.EntryLaneId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            );
        }

        public async Task<bool> SimulateVehicleExitAsync(int cardId, int laneId)
        {
            var lanes = await GetLanesAsync();
            var lane = lanes.FirstOrDefault(l => l.Id == laneId || l.LaneCode == $"LANE-{laneId}");
            if (lane == null) return false;

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "VEHICLE_EXIT",
                new { CardId = cardId, ExitLaneId = lane.Id },
                async conn =>
                {
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.VehicleSessions 
                        SET ThoiGianRa = GETUTCDATE(), ExitLaneId = @laneId, TrangThai = 'Closed'
                        WHERE CardId = @cardId AND ThoiGianRa IS NULL", conn))
                    {
                        cmd.Parameters.AddWithValue("@cardId", cardId);
                        cmd.Parameters.AddWithValue("@laneId", lane.Id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    // Update SQLite
                    string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aps_offline.db");
                    using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Default Timeout=5;"))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(@"
                            UPDATE VehicleSessions 
                            SET ThoiGianRa = CURRENT_TIMESTAMP, ExitLaneId = @laneId, TrangThai = 'Closed'
                            WHERE CardId = @cardId AND ThoiGianRa IS NULL", conn))
                        {
                            cmd.Parameters.AddWithValue("@cardId", cardId);
                            cmd.Parameters.AddWithValue("@laneId", lane.Id);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            );
        }

        // ──────────────────────────────────────────────
        // CAMERAS CRUD
        // ──────────────────────────────────────────────

        public List<QuanLyGiuXe.Models.CameraConfig> GetCameras() => Task.Run(() => GetCamerasAsync()).GetAwaiter().GetResult();

        public async Task<List<QuanLyGiuXe.Models.CameraConfig>> GetCamerasAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<QuanLyGiuXe.Models.CameraConfig>>(
                "LIST_CAMERAS",
                async conn =>
                {
                    var list = new List<QuanLyGiuXe.Models.CameraConfig>();
                    try
                    {
                        using (var cmd = new SqlCommand(@"
                            SELECT c.Id, c.CameraName, c.CameraKey, c.IpAddress, c.RtspUrl, c.LaneId, c.Direction, c.IsActive, c.CreatedUtc,
                                   l.LaneName, c.ResolutionWidth, c.ResolutionHeight
                            FROM dbo.Cameras c
                            LEFT JOIN dbo.Lanes l ON c.LaneId = l.Id
                            ORDER BY c.CameraName", conn))
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            while (await r.ReadAsync())
                            {
                                list.Add(new QuanLyGiuXe.Models.CameraConfig
                                {
                                    Id = r.GetInt32(0),
                                    CameraName = r.GetString(1),
                                    CameraKey = r.GetString(2),
                                    IpAddress = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                                    RtspUrl = r.IsDBNull(4) ? string.Empty : r.GetString(4),
                                    LaneId = r.IsDBNull(5) ? null : r.GetInt32(5),
                                    Direction = r.IsDBNull(6) ? "IN" : r.GetString(6),
                                    IsActive = r.GetBoolean(7),
                                    CreatedUtc = r.GetDateTime(8),
                                    LaneName = r.IsDBNull(9) ? string.Empty : r.GetString(9),
                                    ResolutionWidth = r.IsDBNull(10) ? null : (int?)r.GetInt32(10),
                                    ResolutionHeight = r.IsDBNull(11) ? null : (int?)r.GetInt32(11)
                                });
                            }
                        }
                    }
                    catch (SqlException ex) when (ex.Message.Contains("Invalid object name 'Cameras'") || ex.Message.Contains("Invalid object name 'dbo.Cameras'"))
                    {
                        LoggingService.Instance.LogInfo("TOPOLOGY", "GetCameras", "Cameras table does not exist yet.");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("TOPOLOGY_ERROR", "GetCameras", "Failed to query Cameras table", ex);
                    }
                    return list;
                }
            ) ?? new List<QuanLyGiuXe.Models.CameraConfig>();
        }

        public async Task<bool> SaveCameraAsync(QuanLyGiuXe.Models.CameraConfig camera)
        {
            bool isNew = camera.Id == 0;
            QuanLyGiuXe.Models.CameraConfig? previous = null;
            if (!isNew)
            {
                var list = await GetCamerasAsync();
                previous = list.FirstOrDefault(x => x.Id == camera.Id);
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_CAMERA" : "UPDATE_CAMERA",
                camera,
                async conn =>
                {
                    string sql = isNew
                        ? @"INSERT INTO dbo.Cameras (CameraName, CameraKey, IpAddress, RtspUrl, LaneId, Direction, IsActive, CreatedUtc, ResolutionWidth, ResolutionHeight)
                            OUTPUT INSERTED.Id
                            VALUES (@name, @key, @ip, @rtsp, @laneId, @dir, @active, @created, @width, @height)"
                        : @"UPDATE dbo.Cameras 
                            SET CameraName = @name, CameraKey = @key, IpAddress = @ip, RtspUrl = @rtsp, 
                                LaneId = @laneId, Direction = @dir, IsActive = @active,
                                ResolutionWidth = @width, ResolutionHeight = @height
                            WHERE Id = @id";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", camera.Id);
                        cmd.Parameters.AddWithValue("@name", camera.CameraName);
                        cmd.Parameters.AddWithValue("@key", camera.CameraKey);
                        cmd.Parameters.AddWithValue("@ip", camera.IpAddress ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@rtsp", camera.RtspUrl ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@laneId", camera.LaneId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@dir", camera.Direction ?? "IN");
                        cmd.Parameters.AddWithValue("@active", camera.IsActive);
                        cmd.Parameters.AddWithValue("@created", camera.CreatedUtc);
                        cmd.Parameters.AddWithValue("@width", camera.ResolutionWidth ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@height", camera.ResolutionHeight ?? (object)DBNull.Value);

                        if (isNew)
                        {
                            var newId = await cmd.ExecuteScalarAsync();
                            if (newId != null) camera.Id = Convert.ToInt32(newId);
                        }
                        else
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                async () =>
                {
                    var list = await GetCamerasAsync();
                    if (isNew)
                    {
                        camera.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                        var lanes = await GetLanesAsync();
                        var lane = lanes.FirstOrDefault(l => l.Id == camera.LaneId);
                        if (lane != null) camera.LaneName = lane.LaneName;
                        list.Add(camera);
                    }
                    else
                    {
                        var existing = list.FirstOrDefault(x => x.Id == camera.Id);
                        if (existing != null)
                        {
                            existing.CameraName = camera.CameraName;
                            existing.CameraKey = camera.CameraKey;
                            existing.IpAddress = camera.IpAddress;
                            existing.RtspUrl = camera.RtspUrl;
                            existing.LaneId = camera.LaneId;
                            existing.Direction = camera.Direction;
                            existing.IsActive = camera.IsActive;
                            existing.ResolutionWidth = camera.ResolutionWidth;
                            existing.ResolutionHeight = camera.ResolutionHeight;
                            var lanes = await GetLanesAsync();
                            var lane = lanes.FirstOrDefault(l => l.Id == camera.LaneId);
                            existing.LaneName = lane?.LaneName ?? string.Empty;
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_CAMERAS", list);
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Camera", previous, camera); } catch { }
            }
            return success;
        }

        public async Task<bool> DeleteCameraAsync(int id)
        {
            var list = await GetCamerasAsync();
            var previous = list.FirstOrDefault(x => x.Id == id);
            if (previous == null) return false;

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_CAMERA",
                new { Id = id },
                async conn =>
                {
                    string sql = "DELETE FROM dbo.Cameras WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var list = await GetCamerasAsync();
                    var existing = list.FirstOrDefault(x => x.Id == id);
                    if (existing != null)
                    {
                        list.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_CAMERAS", list);
                    }
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Camera", previous, null); } catch { }
            }
            return success;
        }

        // ──────────────────────────────────────────────
        // BARRIERS CRUD
        // ──────────────────────────────────────────────

        public List<QuanLyGiuXe.Models.BarrierConfig> GetBarriers() => Task.Run(() => GetBarriersAsync()).GetAwaiter().GetResult();

        public async Task<List<QuanLyGiuXe.Models.BarrierConfig>> GetBarriersAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<QuanLyGiuXe.Models.BarrierConfig>>(
                "LIST_BARRIERS",
                async conn =>
                {
                    var list = new List<QuanLyGiuXe.Models.BarrierConfig>();
                    try
                    {
                        using (var cmd = new SqlCommand(@"
                            SELECT b.Id, b.BarrierName, b.ControllerId, b.RelayNumber, b.LaneId, b.Direction, b.IsActive, b.CreatedUtc,
                                   c.ControllerName, l.LaneName
                            FROM dbo.Barriers b
                            LEFT JOIN dbo.C3Controllers c ON b.ControllerId = c.Id
                            LEFT JOIN dbo.Lanes l ON b.LaneId = l.Id
                            ORDER BY b.BarrierName", conn))
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            while (await r.ReadAsync())
                            {
                                list.Add(new QuanLyGiuXe.Models.BarrierConfig
                                {
                                    Id = r.GetInt32(0),
                                    BarrierName = r.GetString(1),
                                    ControllerId = r.IsDBNull(2) ? null : r.GetInt32(2),
                                    RelayNumber = r.GetInt32(3),
                                    LaneId = r.IsDBNull(4) ? null : r.GetInt32(4),
                                    Direction = r.IsDBNull(5) ? "IN" : r.GetString(5),
                                    IsActive = r.GetBoolean(6),
                                    CreatedUtc = r.GetDateTime(7),
                                    ControllerName = r.IsDBNull(8) ? string.Empty : r.GetString(8),
                                    LaneName = r.IsDBNull(9) ? string.Empty : r.GetString(9)
                                });
                            }
                        }
                    }
                    catch (SqlException ex) when (ex.Message.Contains("Invalid object name 'Barriers'") || ex.Message.Contains("Invalid object name 'dbo.Barriers'"))
                    {
                        LoggingService.Instance.LogInfo("TOPOLOGY", "GetBarriers", "Barriers table does not exist yet.");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("TOPOLOGY_ERROR", "GetBarriers", "Failed to query Barriers table", ex);
                    }
                    return list;
                }
            ) ?? new List<QuanLyGiuXe.Models.BarrierConfig>();
        }

        public async Task<bool> SaveBarrierAsync(QuanLyGiuXe.Models.BarrierConfig barrier)
        {
            bool isNew = barrier.Id == 0;
            QuanLyGiuXe.Models.BarrierConfig? previous = null;
            if (!isNew)
            {
                var list = await GetBarriersAsync();
                previous = list.FirstOrDefault(x => x.Id == barrier.Id);
            }

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_BARRIER" : "UPDATE_BARRIER",
                barrier,
                async conn =>
                {
                    string sql = isNew
                        ? @"INSERT INTO dbo.Barriers (BarrierName, ControllerId, RelayNumber, LaneId, Direction, IsActive, CreatedUtc)
                            OUTPUT INSERTED.Id
                            VALUES (@name, @controllerId, @relayNo, @laneId, @dir, @active, @created)"
                        : @"UPDATE dbo.Barriers 
                            SET BarrierName = @name, ControllerId = @controllerId, RelayNumber = @relayNo, 
                                LaneId = @laneId, Direction = @dir, IsActive = @active 
                            WHERE Id = @id";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", barrier.Id);
                        cmd.Parameters.AddWithValue("@name", barrier.BarrierName);
                        cmd.Parameters.AddWithValue("@controllerId", barrier.ControllerId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@relayNo", barrier.RelayNumber);
                        cmd.Parameters.AddWithValue("@laneId", barrier.LaneId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@dir", barrier.Direction ?? "IN");
                        cmd.Parameters.AddWithValue("@active", barrier.IsActive);
                        cmd.Parameters.AddWithValue("@created", barrier.CreatedUtc);

                        if (isNew)
                        {
                            var newId = await cmd.ExecuteScalarAsync();
                            if (newId != null) barrier.Id = Convert.ToInt32(newId);
                        }
                        else
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                async () =>
                {
                    var list = await GetBarriersAsync();
                    if (isNew)
                    {
                        barrier.Id = list.Any() ? list.Max(x => x.Id) + 1 : 1;
                        var controllers = await GetControllersAsync();
                        var controller = controllers.FirstOrDefault(c => c.Id == barrier.ControllerId);
                        if (controller != null) barrier.ControllerName = controller.ControllerName;
                        var lanes = await GetLanesAsync();
                        var lane = lanes.FirstOrDefault(l => l.Id == barrier.LaneId);
                        if (lane != null) barrier.LaneName = lane.LaneName;
                        list.Add(barrier);
                    }
                    else
                    {
                        var existing = list.FirstOrDefault(x => x.Id == barrier.Id);
                        if (existing != null)
                        {
                            existing.BarrierName = barrier.BarrierName;
                            existing.ControllerId = barrier.ControllerId;
                            existing.RelayNumber = barrier.RelayNumber;
                            existing.LaneId = barrier.LaneId;
                            existing.Direction = barrier.Direction;
                            existing.IsActive = barrier.IsActive;
                            var controllers = await GetControllersAsync();
                            var controller = controllers.FirstOrDefault(c => c.Id == barrier.ControllerId);
                            existing.ControllerName = controller?.ControllerName ?? string.Empty;
                            var lanes = await GetLanesAsync();
                            var lane = lanes.FirstOrDefault(l => l.Id == barrier.LaneId);
                            existing.LaneName = lane?.LaneName ?? string.Empty;
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_BARRIERS", list);
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Barrier", previous, barrier); } catch { }
            }
            return success;
        }

        public async Task<bool> DeleteBarrierAsync(int id)
        {
            var list = await GetBarriersAsync();
            var previous = list.FirstOrDefault(x => x.Id == id);
            if (previous == null) return false;

            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_BARRIER",
                new { Id = id },
                async conn =>
                {
                    string sql = "DELETE FROM dbo.Barriers WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var list = await GetBarriersAsync();
                    var existing = list.FirstOrDefault(x => x.Id == id);
                    if (existing != null)
                    {
                        list.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_BARRIERS", list);
                    }
                }
            );

            if (success)
            {
                try { await ConfigurationAuditService.Instance.AuditChangesAsync("Barrier", previous, null); } catch { }
            }
            return success;
        }
    }
}
