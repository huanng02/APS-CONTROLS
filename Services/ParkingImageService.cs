using System;
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Lưu ảnh xe vào/ra theo cấu trúc thư mục topology:
    /// {Root}\{SiteName}\{ZoneName}\{GateName}\{LaneName}\{YYYY-MM-DD}\{HHmmss}_{BienSo}_{IN|OUT}\
    ///   ├── full.jpg        → ảnh toàn cảnh (camera tổng quan)
    ///   ├── plate_raw.jpg   → ảnh biển số chưa cắt (frame gốc camera biển)
    ///   └── plate_crop.jpg  → ảnh biển số đã cắt (ROI từ YOLO)
    /// DB lưu đường dẫn thư mục (relative từ Root hoặc absolute)
    /// </summary>
    public class ParkingImageService
    {
        private static readonly Lazy<ParkingImageService> _instance = new(() => new ParkingImageService());
        public static ParkingImageService Instance => _instance.Value;

        // Thư mục gốc lưu ảnh — có thể cấu hình qua AppConfig sau
        private string _rootDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "APS", "ParkingImages"
        );

        public string RootDirectory
        {
            get => _rootDir;
            set => _rootDir = value;
        }

        private ParkingImageService() { }

        /// <summary>
        /// Xây dựng đường dẫn thư mục theo topology (không tạo thư mục).
        /// </summary>
        public string BuildFolderPath(
            string siteName,
            string zoneName,
            string gateName,
            string laneName,
            string bienSo,
            string direction,
            DateTime timestamp)
        {
            // Làm sạch tên để dùng làm tên thư mục
            string site  = SanitizeName(siteName)  ?? "Site";
            string zone  = SanitizeName(zoneName)  ?? "Zone";
            string gate  = SanitizeName(gateName)  ?? "Gate";
            string lane  = SanitizeName(laneName)  ?? "Lane";
            string plate = SanitizeName(bienSo)    ?? "NO_PLATE";
            string dir   = direction?.ToUpper() == "OUT" ? "OUT" : "IN";

            // Ví dụ: 20240608_143025_51A12345_IN
            string sessionFolder = $"{timestamp:yyyyMMdd_HHmmss}_{plate}_{dir}";

            return Path.Combine(
                _rootDir,
                site,
                zone,
                gate,
                lane,
                timestamp.ToString("yyyy-MM-dd"),
                sessionFolder
            );
        }

        /// <summary>
        /// Lưu 3 ảnh (full, plate_raw, plate_crop) vào thư mục theo topology.
        /// Trả về đường dẫn thư mục đã lưu, hoặc null nếu thất bại.
        /// </summary>
        public async Task<string?> SaveSnapshotsAsync(
            string siteName,
            string zoneName,
            string gateName,
            string laneName,
            string bienSo,
            string direction,
            DateTime timestamp,
            Mat? fullFrame,         // Ảnh toàn cảnh từ camera tổng quan
            Mat? plateRawFrame,     // Ảnh biển số từ camera biển (chưa cắt)
            byte[]? plateCropBytes  // Ảnh biển số đã cắt (ROI từ server LPR) — nullable
        )
        {
            try
            {
                string folderPath = BuildFolderPath(siteName, zoneName, gateName, laneName, bienSo, direction, timestamp);
                Directory.CreateDirectory(folderPath);

                var tasks = new System.Collections.Generic.List<Task>();

                // 1. Ảnh toàn cảnh
                if (fullFrame != null && !fullFrame.Empty())
                {
                    string fullPath = Path.Combine(folderPath, "full.jpg");
                    tasks.Add(Task.Run(() => Cv2.ImWrite(fullPath, fullFrame, new ImageEncodingParam(ImwriteFlags.JpegQuality, 85))));
                }

                // 2. Ảnh biển số chưa cắt (raw frame từ camera biển)
                if (plateRawFrame != null && !plateRawFrame.Empty())
                {
                    string rawPath = Path.Combine(folderPath, "plate_raw.jpg");
                    tasks.Add(Task.Run(() => Cv2.ImWrite(rawPath, plateRawFrame, new ImageEncodingParam(ImwriteFlags.JpegQuality, 85))));
                }

                // 3. Ảnh biển số đã cắt (ROI từ LPR server)
                if (plateCropBytes != null && plateCropBytes.Length > 0)
                {
                    string cropPath = Path.Combine(folderPath, "plate_crop.jpg");
                    tasks.Add(File.WriteAllBytesAsync(cropPath, plateCropBytes));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                    LoggingService.Instance.LogInfo("ImageSave", "ParkingImageService",
                        $"Đã lưu {tasks.Count} ảnh → {folderPath}");
                }

                return folderPath;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ImageSaveError", "ParkingImageService",
                    $"Lỗi lưu ảnh: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin topology đầy đủ từ các ID.
        /// Trả về (siteName, zoneName, gateName, laneName).
        /// </summary>
        public static async Task<(string site, string zone, string gate, string lane)> ResolveTopologyNamesAsync(
            int? siteId, int? zoneId, int? laneId)
        {
            string siteName = "Site";
            string zoneName = "Zone";
            string gateName = "Gate";
            string laneName = "Lane";

            try
            {
                var topoSvc = ParkingTopologyService.Instance;

                if (siteId.HasValue)
                {
                    var sites = await topoSvc.GetSitesAsync();
                    var site = sites?.Find(s => s.Id == siteId.Value);
                    if (site != null) siteName = site.SiteName ?? siteName;
                }

                if (zoneId.HasValue)
                {
                    var zones = await topoSvc.GetZonesAsync();
                    var zone = zones?.Find(z => z.Id == zoneId.Value);
                    if (zone != null) zoneName = zone.ZoneName ?? zoneName;
                }

                if (laneId.HasValue)
                {
                    var laneConfig = await topoSvc.GetLaneByIdAsync(laneId.Value);
                    if (laneConfig != null)
                    {
                        laneName = laneConfig.LaneName ?? laneName;

                        if (laneConfig.GateId.HasValue)
                        {
                            var gates = await topoSvc.GetGatesAsync();
                            var gate = gates?.Find(g => g.Id == laneConfig.GateId.Value);
                            if (gate != null) gateName = gate.GateName ?? gateName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { LoggingService.Instance.LogError("TopoResolve", "ParkingImageService", "Lỗi resolve topology", ex); } catch { }
            }

            return (siteName, zoneName, gateName, laneName);
        }

        private static string? SanitizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            // Xóa ký tự không hợp lệ trong tên thư mục Windows
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim().Replace(' ', '_');
        }
    }
}
