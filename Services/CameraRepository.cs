using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.Services
{
    public class CameraRepository
    {
        private static readonly Lazy<CameraRepository> _instance = new(() => new CameraRepository());
        public static CameraRepository Instance => _instance.Value;

        private CameraRepository() { }

        public async Task<List<CameraEntity>> GetAllAsync()
        {
            return await ConnectivityAwareRepository.Instance.ExecuteReadAsync<List<CameraEntity>>(
                "LIST_CAMERAS_MGMT",
                async conn =>
                {
                    var list = new List<CameraEntity>();
                    string sql = @"
                        SELECT c.Id, c.CameraName, c.CameraKey, c.IpAddress, c.Port, c.Protocol, 
                               c.Username, c.Password, c.RtspUrl, c.LaneId, c.Direction, c.IsActive, 
                               c.CreatedUtc, l.LaneName, c.ResolutionWidth, c.ResolutionHeight
                        FROM dbo.Cameras c
                        LEFT JOIN dbo.Lanes l ON c.LaneId = l.Id
                        ORDER BY c.CameraName";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(new CameraEntity
                            {
                                Id = r.GetInt32(0),
                                CameraName = r.GetString(1),
                                CameraKey = r.GetString(2),
                                IpAddress = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                                Port = r.IsDBNull(4) ? null : (int?)r.GetInt32(4),
                                Protocol = r.IsDBNull(5) ? "RTSP" : r.GetString(5),
                                Username = r.IsDBNull(6) ? string.Empty : r.GetString(6),
                                Password = r.IsDBNull(7) ? string.Empty : r.GetString(7),
                                RtspUrl = r.IsDBNull(8) ? string.Empty : r.GetString(8),
                                LaneId = r.IsDBNull(9) ? null : (int?)r.GetInt32(9),
                                Direction = r.IsDBNull(10) ? "Overview" : r.GetString(10),
                                IsActive = r.GetBoolean(11),
                                CreatedUtc = r.GetDateTime(12),
                                LaneName = r.IsDBNull(13) ? string.Empty : r.GetString(13),
                                ResolutionWidth = r.IsDBNull(14) ? null : (int?)r.GetInt32(14),
                                ResolutionHeight = r.IsDBNull(15) ? null : (int?)r.GetInt32(15)
                            });
                        }
                    }
                    return list;
                }
            ) ?? new List<CameraEntity>();
        }

        public async Task<CameraEntity?> GetByIdAsync(int id)
        {
            var list = await GetAllAsync();
            return list.FirstOrDefault(c => c.Id == id);
        }

        public async Task<bool> InsertAsync(CameraEntity camera)
        {
            if (!await CameraService.Instance.IsIpAddressUniqueAsync(camera.IpAddress, null))
            {
                throw new InvalidOperationException("A camera with this IP address already exists.");
            }

            try
            {
                var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                    "CREATE_CAMERA_MGMT",
                    camera,
                    async conn =>
                    {
                        string sql = @"
                            INSERT INTO dbo.Cameras (CameraName, CameraKey, IpAddress, Port, Protocol, Username, Password, RtspUrl, LaneId, Direction, IsActive, CreatedUtc, ResolutionWidth, ResolutionHeight)
                            OUTPUT INSERTED.Id
                            VALUES (@name, @key, @ip, @port, @protocol, @username, @password, @rtsp, @laneId, @dir, @active, @created, @width, @height)";

                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@name", camera.CameraName);
                            cmd.Parameters.AddWithValue("@key", camera.CameraKey);
                            cmd.Parameters.AddWithValue("@ip", camera.IpAddress ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@port", camera.Port ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@protocol", camera.Protocol ?? "RTSP");
                            cmd.Parameters.AddWithValue("@username", camera.Username ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@password", camera.Password ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@rtsp", camera.RtspUrl ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@laneId", camera.LaneId ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@dir", camera.Direction ?? "Overview");
                            cmd.Parameters.AddWithValue("@active", camera.IsActive);
                            cmd.Parameters.AddWithValue("@created", camera.CreatedUtc);
                            cmd.Parameters.AddWithValue("@width", camera.ResolutionWidth ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@height", camera.ResolutionHeight ?? (object)DBNull.Value);

                            var newId = await cmd.ExecuteScalarAsync();
                            if (newId != null) camera.Id = Convert.ToInt32(newId);
                        }
                    }
                );
                return success;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                throw new InvalidOperationException("A camera with this IP address already exists.", ex);
            }
        }

        public async Task<bool> UpdateAsync(CameraEntity camera)
        {
            if (!await CameraService.Instance.IsIpAddressUniqueAsync(camera.IpAddress, camera.Id))
            {
                throw new InvalidOperationException("A camera with this IP address already exists.");
            }

            try
            {
                var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                    "UPDATE_CAMERA_MGMT",
                    camera,
                    async conn =>
                    {
                        string sql = @"
                            UPDATE dbo.Cameras 
                            SET CameraName = @name, CameraKey = @key, IpAddress = @ip, Port = @port, 
                                Protocol = @protocol, Username = @username, Password = @password, 
                                RtspUrl = @rtsp, LaneId = @laneId, Direction = @dir, IsActive = @active,
                                ResolutionWidth = @width, ResolutionHeight = @height
                            WHERE Id = @id";

                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", camera.Id);
                            cmd.Parameters.AddWithValue("@name", camera.CameraName);
                            cmd.Parameters.AddWithValue("@key", camera.CameraKey);
                            cmd.Parameters.AddWithValue("@ip", camera.IpAddress ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@port", camera.Port ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@protocol", camera.Protocol ?? "RTSP");
                            cmd.Parameters.AddWithValue("@username", camera.Username ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@password", camera.Password ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@rtsp", camera.RtspUrl ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@laneId", camera.LaneId ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@dir", camera.Direction ?? "Overview");
                            cmd.Parameters.AddWithValue("@active", camera.IsActive);
                            cmd.Parameters.AddWithValue("@width", camera.ResolutionWidth ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@height", camera.ResolutionHeight ?? (object)DBNull.Value);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                );
                return success;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                throw new InvalidOperationException("A camera with this IP address already exists.", ex);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var success = await ConnectivityAwareRepository.Instance.ExecuteWriteAsync(
                "DELETE_CAMERA_MGMT",
                id,
                async conn =>
                {
                    string sql = "DELETE FROM dbo.Cameras WHERE Id = @id";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            );
            return success;
        }

        public async Task<bool> IsAssignedToAnyLaneAsync(int id, string cameraName, string rtspUrl)
        {
            // 1. Check database lane assignments
            bool dbAssigned = await ConnectivityAwareRepository.Instance.ExecuteReadAsync<bool>(
                "CHECK_CAMERA_ASSIGNMENT",
                async conn =>
                {
                    string sql = "SELECT COUNT(*) FROM dbo.Cameras WHERE Id = @id AND LaneId IS NOT NULL";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        var count = (int?)await cmd.ExecuteScalarAsync() ?? 0;
                        return count > 0;
                    }
                }
            );

            if (dbAssigned) return true;

            // 2. Check config.json lane mapping references
            try
            {
                var cfg = AppConfig.Load().Cameras;
                if (cfg != null)
                {
                    // Check legacy configs
                    if (cfg.VaoToanCanh == cameraName || cfg.VaoToanCanh == rtspUrl ||
                        cfg.VaoBienSo == cameraName || cfg.VaoBienSo == rtspUrl ||
                        cfg.RaToanCanh == cameraName || cfg.RaToanCanh == rtspUrl ||
                        cfg.RaBienSo == cameraName || cfg.RaBienSo == rtspUrl)
                    {
                        return true;
                    }

                    // Check dynamic lane configs
                    if (cfg.LaneCameras != null)
                    {
                        foreach (var lc in cfg.LaneCameras)
                        {
                            if (lc.ToanCanh == cameraName || lc.ToanCanh == rtspUrl ||
                                lc.BienSo == cameraName || lc.BienSo == rtspUrl)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
