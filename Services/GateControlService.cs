using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services
{
    public class GateControlService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public async Task ProcessGateActionAsync(int physicalDoor, Dictionary<string, Bitmap> currentFrames, string actionType, string? note = null)
        {
            try
            {
                // 1. Chụp và lưu ảnh ngay lập tức để tránh trễ hình
                var (platePath, fullPath) = SaveCurrentImages(physicalDoor, currentFrames, actionType);

                // 2. Gọi lệnh mở cổng (Hardware)
                bool opened = await C3200Service.Instance.OpenBarrierAsync(physicalDoor);

                // 3. Ghi vào Database
                _db.InsertButtonPressLog(
                    DateTime.Now,
                    (byte?)physicalDoor,
                    null, // EventType
                    null, // InOutState
                    actionType == "MANUAL" ? "MANUAL" : "BUTTON", // CardNo/Pin
                    null,
                    null, // RawData
                    actionType, // Ghi chú loại hành động (MANUAL_OPEN, BUTTON_PRESS)
                    opened ? (byte)1 : (byte)0,
                    platePath,
                    fullPath,
                    Environment.UserName, // Người thực hiện (nếu là manual)
                    null,
                    note
                );
            }
            catch (Exception ex)
            {
                // Log lỗi vào file để debug
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GateServiceError.txt"),
                    $"{DateTime.Now:O}: {ex.Message}\n");
            }
        }

        private (string? PlatePath, string? FullPath) SaveCurrentImages(int door, Dictionary<string, Bitmap> frames, string prefix)
        {
            string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GateImages");
            if (!Directory.Exists(imagesDir)) Directory.CreateDirectory(imagesDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string? platePath = null;
            string? fullPath = null;

            // Xác định key camera dựa trên số cổng
            string fullKey = door == 1 ? "Vao1" : "Ra1";
            string plateKey = door == 1 ? "Vao2" : "Ra2";

            lock (frames) // Đảm bảo an toàn luồng khi truy cập Dictionary
            {
                if (frames.TryGetValue(fullKey, out var fullBmp))
                {
                    fullPath = Path.Combine(imagesDir, $"{stamp}_{prefix}_door{door}_full.jpg");
                    using var clone = (Bitmap)fullBmp.Clone();
                    clone.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }

                if (frames.TryGetValue(plateKey, out var plateBmp))
                {
                    platePath = Path.Combine(imagesDir, $"{stamp}_{prefix}_door{door}_plate.jpg");
                    using var clone = (Bitmap)plateBmp.Clone();
                    clone.Save(platePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
            }

            return (platePath, fullPath);
        }
    }
}