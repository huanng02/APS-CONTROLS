using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
            // Software-triggered gate open action requires strict permission and scope validation
            if (actionType == "MANUAL_OPEN" || actionType == "MANUAL")
            {
                AuthorizationGuard.Protect("OPEN_BARRIER", "Manual Barrier Opening");
                int readerNo = (physicalDoor == 1) ? 1 : 3;
                var mapping = ReaderLaneMappingService.Instance.GetMappingByReader(readerNo);
                int laneId = mapping?.LaneId ?? physicalDoor;
                AuthorizationGuard.ProtectLane(laneId, "Manual Barrier Opening");
            }

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
                    QuanLyGiuXe.Models.CurrentUserContext.Instance.Username ?? "Operator", // Người thực hiện (nếu là manual)
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
            var config = AppConfig.Load();
            int jpegQuality = config?.Cameras?.SaveJpegQuality ?? 70;
            int maxWidth = config?.Cameras?.SaveMaxWidth ?? 1280;

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
                    SaveCompressedJpeg(fullBmp, fullPath, jpegQuality, maxWidth);
                }

                if (frames.TryGetValue(plateKey, out var plateBmp))
                {
                    platePath = Path.Combine(imagesDir, $"{stamp}_{prefix}_door{door}_plate.jpg");
                    SaveCompressedJpeg(plateBmp, platePath, jpegQuality, maxWidth);
                }
            }

            return (platePath, fullPath);
        }

        private void SaveCompressedJpeg(Bitmap bmp, string path, int quality, int maxWidth)
        {
            if (bmp == null) return;
            Bitmap? processedBmp = null;
            try
            {
                if (bmp.Width > maxWidth)
                {
                    double scale = (double)maxWidth / bmp.Width;
                    int newHeight = (int)(bmp.Height * scale);
                    processedBmp = new Bitmap(maxWidth, newHeight);
                    using (Graphics g = Graphics.FromImage(processedBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, 0, 0, maxWidth, newHeight);
                    }
                }
                else
                {
                    processedBmp = (Bitmap)bmp.Clone();
                }

                // Save with custom Jpeg Quality
                ImageCodecInfo? jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                if (jpegEncoder != null)
                {
                    using var encoderParameters = new EncoderParameters(1);
                    using var encoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                    encoderParameters.Param[0] = encoderParameter;
                    processedBmp.Save(path, jpegEncoder, encoderParameters);
                }
                else
                {
                    processedBmp.Save(path, ImageFormat.Jpeg);
                }
            }
            catch (Exception ex)
            {
                // Fallback to basic saving if anything fails
                try { bmp.Save(path, ImageFormat.Jpeg); } catch { }
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GateServiceError.txt"),
                        $"{DateTime.Now:O}: Lỗi nén ảnh: {ex.Message}\n");
                }
                catch { }
            }
            finally
            {
                processedBmp?.Dispose();
            }
        }

        private ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}