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
                    string sql = "SELECT Id, SiteCode, SiteName, Description, IsActive, CreatedUtc FROM dbo.ParkingSites ORDER BY SiteCode";
                    using (var cmd = new SqlCommand(sql, conn))
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
                    string sql = @"
                        SELECT z.Id, z.SiteId, z.ZoneCode, z.ZoneName, z.Description, z.MaxCapacity, z.IsActive, z.CreatedUtc,
                               s.SiteCode, s.SiteName
                        FROM dbo.ParkingZones z
                        JOIN dbo.ParkingSites s ON z.SiteId = s.Id
                        ORDER BY z.ZoneCode";
                    using (var cmd = new SqlCommand(sql, conn))
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

        public List<C3ControllerConfig> GetControllers() => Task.Run(() => GetControllersAsync()).GetAwaiter().GetResult();

        public async Task<List<C3ControllerConfig>> GetControllersAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<C3ControllerConfig>>(
                "LIST_CONTROLLERS",
                async conn =>
                {
                    var list = new List<C3ControllerConfig>();
                    string sql = @"
                        SELECT c.Id, c.ControllerName, c.IpAddress, c.ZoneId, c.IsActive, c.CreatedUtc,
                               z.ZoneName
                        FROM dbo.C3Controllers c
                        JOIN dbo.ParkingZones z ON c.ZoneId = z.Id
                        ORDER BY c.ControllerName";
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new C3ControllerConfig
                            {
                                Id = r.GetInt32(0),
                                ControllerName = r.GetString(1),
                                IpAddress = r.GetString(2),
                                ZoneId = r.GetInt32(3),
                                IsActive = r.GetBoolean(4),
                                CreatedUtc = r.GetDateTime(5),
                                ZoneName = r.GetString(6)
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
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<LaneConfig>>(
                "LIST_LANES",
                async conn =>
                {
                    var list = new List<LaneConfig>();
                    string sql = @"
                        SELECT l.Id, l.LaneCode, l.LaneName, l.Direction, l.ZoneId, l.IsActive, l.CreatedUtc,
                               z.ZoneName
                        FROM dbo.Lanes l
                        LEFT JOIN dbo.ParkingZones z ON l.ZoneId = z.Id
                        ORDER BY l.LaneCode";
                    using (var cmd = new SqlCommand(sql, conn))
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
                                IsActive = r.GetBoolean(5),
                                CreatedUtc = r.GetDateTime(6),
                                ZoneName = r.IsDBNull(7) ? string.Empty : r.GetString(7)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<LaneConfig>();
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
            if (mapping == null || !mapping.IsEnabled) return null;

            var lanes = await GetLanesAsync();
            // Resolve by LaneCode (e.g. "LANE-1" matches LaneIndex = 1) or by Id
            var lane = lanes.FirstOrDefault(l => l.Id == mapping.LaneIndex || l.LaneCode == $"LANE-{mapping.LaneIndex}");
            if (lane == null || !lane.ZoneId.HasValue) return null;

            var zones = await GetZonesAsync();
            return zones.FirstOrDefault(z => z.Id == lane.ZoneId.Value);
        }

        public List<C3ControllerConfig> GetControllersByZone(int zoneId) => Task.Run(() => GetControllersByZoneAsync(zoneId)).GetAwaiter().GetResult();

        public async Task<List<C3ControllerConfig>> GetControllersByZoneAsync(int zoneId)
        {
            var controllers = await GetControllersAsync();
            return controllers.Where(c => c.ZoneId == zoneId).ToList();
        }

        // ──────────────────────────────────────────────
        // WRITE / CRUD Methods
        // ──────────────────────────────────────────────

        public async Task<bool> SaveSiteAsync(ParkingSite site)
        {
            bool isNew = site.Id == 0;
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_SITE" : "UPDATE_SITE",
                site,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.ParkingSites (SiteCode, SiteName, Description, IsActive, CreatedUtc) VALUES (@code, @name, @desc, @active, @created)";
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
                        await cmd.ExecuteNonQueryAsync();
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
        }

        public async Task<bool> DeleteSiteAsync(int id)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_SITE",
                new { Id = id },
                async conn =>
                {
                    string sql = "DELETE FROM dbo.ParkingSites WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var sites = await GetSitesAsync();
                    var existing = sites.FirstOrDefault(s => s.Id == id);
                    if (existing != null)
                    {
                        sites.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_SITES", sites);
                    }
                }
            );
        }

        public async Task<bool> SaveZoneAsync(ParkingZone zone)
        {
            bool isNew = zone.Id == 0;
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_ZONE" : "UPDATE_ZONE",
                zone,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.ParkingZones (SiteId, ZoneCode, ZoneName, Description, MaxCapacity, IsActive, CreatedUtc) VALUES (@siteId, @code, @name, @desc, @cap, @active, @created)";
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
                        await cmd.ExecuteNonQueryAsync();
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
        }

        public async Task<bool> DeleteZoneAsync(int id)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_ZONE",
                new { Id = id },
                async conn =>
                {
                    string sql = "DELETE FROM dbo.ParkingZones WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var zones = await GetZonesAsync();
                    var existing = zones.FirstOrDefault(z => z.Id == id);
                    if (existing != null)
                    {
                        zones.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_ZONES", zones);
                    }
                }
            );
        }

        public async Task<bool> SaveControllerAsync(C3ControllerConfig controller)
        {
            bool isNew = controller.Id == 0;
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_CONTROLLER" : "UPDATE_CONTROLLER",
                controller,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.C3Controllers (ControllerName, IpAddress, ZoneId, IsActive, CreatedUtc) VALUES (@name, @ip, @zoneId, @active, @created)";
                    }
                    else
                    {
                        sql = "UPDATE dbo.C3Controllers SET ControllerName = @name, IpAddress = @ip, ZoneId = @zoneId, IsActive = @active WHERE Id = @id";
                    }

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", controller.Id);
                        cmd.Parameters.AddWithValue("@name", controller.ControllerName);
                        cmd.Parameters.AddWithValue("@ip", controller.IpAddress);
                        cmd.Parameters.AddWithValue("@zoneId", controller.ZoneId);
                        cmd.Parameters.AddWithValue("@active", controller.IsActive);
                        cmd.Parameters.AddWithValue("@created", controller.CreatedUtc);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var controllers = await GetControllersAsync();
                    if (isNew)
                    {
                        controller.Id = controllers.Any() ? controllers.Max(c => c.Id) + 1 : 1;
                        var zone = await GetZoneAsync(controller.ZoneId);
                        if (zone != null)
                        {
                            controller.ZoneName = zone.ZoneName;
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
                            existing.ZoneId = controller.ZoneId;
                            existing.IsActive = controller.IsActive;
                            var zone = await GetZoneAsync(controller.ZoneId);
                            if (zone != null)
                            {
                                existing.ZoneName = zone.ZoneName;
                            }
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_CONTROLLERS", controllers);
                }
            );
        }

        public async Task<bool> DeleteControllerAsync(int id)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
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
        }

        public async Task<bool> SaveLaneAsync(LaneConfig lane)
        {
            bool isNew = lane.Id == 0;
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                isNew ? "CREATE_LANE" : "UPDATE_LANE",
                lane,
                async conn =>
                {
                    string sql;
                    if (isNew)
                    {
                        sql = "INSERT INTO dbo.Lanes (LaneCode, LaneName, Direction, ZoneId, IsActive, CreatedUtc) VALUES (@code, @name, @dir, @zoneId, @active, @created)";
                    }
                    else
                    {
                        sql = "UPDATE dbo.Lanes SET LaneCode = @code, LaneName = @name, Direction = @dir, ZoneId = @zoneId, IsActive = @active WHERE Id = @id";
                    }

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        if (!isNew) cmd.Parameters.AddWithValue("@id", lane.Id);
                        cmd.Parameters.AddWithValue("@code", lane.LaneCode);
                        cmd.Parameters.AddWithValue("@name", lane.LaneName);
                        cmd.Parameters.AddWithValue("@dir", lane.Direction);
                        cmd.Parameters.AddWithValue("@zoneId", (object?)lane.ZoneId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@active", lane.IsActive);
                        cmd.Parameters.AddWithValue("@created", lane.CreatedUtc);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var lanes = await GetLanesAsync();
                    if (isNew)
                    {
                        lane.Id = lanes.Any() ? lanes.Max(l => l.Id) + 1 : 1;
                        if (lane.ZoneId.HasValue)
                        {
                            var zone = await GetZoneAsync(lane.ZoneId.Value);
                            if (zone != null) lane.ZoneName = zone.ZoneName;
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
                        }
                    }
                    await OfflineCacheService.Instance.SaveCacheAsync("LIST_LANES", lanes);
                }
            );
        }

        public async Task<bool> DeleteLaneAsync(int id)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_LANE",
                new { Id = id },
                async conn =>
                {
                    string sql = "DELETE FROM dbo.Lanes WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var lanes = await GetLanesAsync();
                    var existing = lanes.FirstOrDefault(l => l.Id == id);
                    if (existing != null)
                    {
                        lanes.Remove(existing);
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_LANES", lanes);
                    }
                }
            );
        }

        public async Task<bool> AssignLaneToZoneAsync(int laneId, int? zoneId)
        {
            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "ASSIGN_LANE_ZONE",
                new { LaneId = laneId, ZoneId = zoneId },
                async conn =>
                {
                    string sql = "UPDATE dbo.Lanes SET ZoneId = @zoneId WHERE Id = @laneId";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@laneId", laneId);
                        cmd.Parameters.AddWithValue("@zoneId", (object?)zoneId ?? DBNull.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                async () =>
                {
                    var lanes = await GetLanesAsync();
                    var lane = lanes.FirstOrDefault(l => l.Id == laneId);
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
                        await OfflineCacheService.Instance.SaveCacheAsync("LIST_LANES", lanes);
                    }
                }
            );
        }

        // ──────────────────────────────────────────────
        // QA Simulation Helper Methods
        // ──────────────────────────────────────────────

        public async Task<bool> SimulateVehicleEntryAsync(int cardId, string plate, int laneId)
        {
            var lanes = await GetLanesAsync();
            var lane = lanes.FirstOrDefault(l => l.Id == laneId || l.LaneCode == $"LANE-{laneId}");
            if (lane == null || !lane.ZoneId.HasValue) return false;
            var zone = await GetZoneAsync(lane.ZoneId.Value);
            if (zone == null) return false;

            var session = new VehicleSession
            {
                CardId = cardId,
                BienSo = plate,
                ThoiGianVao = DateTime.Now,
                SiteId = zone.SiteId,
                ZoneId = zone.Id,
                EntryLaneId = lane.Id,
                TrangThai = "Active"
            };

            return await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "VEHICLE_ENTRY",
                session,
                async conn =>
                {
                    string sql = @"
                        INSERT INTO dbo.VehicleSessions (CardId, BienSo, ThoiGianVao, SiteId, ZoneId, EntryLaneId, TrangThai)
                        VALUES (@cardId, @plate, @time, @siteId, @zoneId, @laneId, 'Active')";
                    using (var cmd = new SqlCommand(sql, conn))
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
                        string sql = @"
                            INSERT INTO VehicleSessions (CardId, BienSo, ThoiGianVao, SiteId, ZoneId, EntryLaneId, TrangThai)
                            VALUES (@cardId, @plate, @time, @siteId, @zoneId, @laneId, 'Active')";
                        using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, conn))
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
                    string sql = @"
                        UPDATE dbo.VehicleSessions 
                        SET ThoiGianRa = GETUTCDATE(), ExitLaneId = @laneId, TrangThai = 'Closed'
                        WHERE CardId = @cardId AND ThoiGianRa IS NULL";
                    using (var cmd = new SqlCommand(sql, conn))
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
                        string sql = @"
                            UPDATE VehicleSessions 
                            SET ThoiGianRa = CURRENT_TIMESTAMP, ExitLaneId = @laneId, TrangThai = 'Closed'
                            WHERE CardId = @cardId AND ThoiGianRa IS NULL";
                        using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@cardId", cardId);
                            cmd.Parameters.AddWithValue("@laneId", lane.Id);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            );
        }
    }
}
