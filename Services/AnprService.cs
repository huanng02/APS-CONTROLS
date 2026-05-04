using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace QuanLyGiuXe.Services
{
    public class AnprResult
    {
        public string Plate { get; set; }
        public BitmapSource RoiImage { get; set; }
        public string SavedPath { get; set; }
    }

    public class AnprService
    {
        public event Action<AnprResult> OnDetectionCompleted;
        public async Task<AnprResult> RecognizeAsync(Bitmap bitmap)
        {
            // 1. Gửi API
            string plate = await ApiService.SendImageAsync(bitmap);

            if (string.IsNullOrEmpty(plate) || plate.Contains("Lỗi"))
                return null;

            // 2. Lấy ảnh ROI
            BitmapSource roi = await ApiService.DownloadRoiImageAsync();

            // 3. Lưu ảnh cục bộ
            string path = SaveRoiToLocal(roi, plate);

            return new AnprResult
            {
                Plate = plate.Trim().ToUpper(),
                RoiImage = roi,
                SavedPath = path
            };
        }

        public string SaveRoiToLocal(BitmapSource image, string plate)
        {
            try
            {
                // Tạo folder lưu theo ngày
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedROI", DateTime.Now.ToString("yyyyMMdd"));
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                // Tên file: BienSo_GioPhutGiay.jpg
                string fileName = $"{plate}_{DateTime.Now:HHmmss}.jpg";
                string filePath = Path.Combine(folderPath, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(fileStream);
                }
                return filePath; // Trả về đường dẫn để lưu vào DB sau này
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi lưu ảnh: " + ex.Message);
                return null;
            }
        }
        public async Task ProcessAutoDetectionAsync(Bitmap originalBitmap)
        {
            try
            {
                Bitmap bmpToProcess;
                // Clone ảnh ngay tại đây để an toàn đa luồng
                lock (originalBitmap)
                {
                    bmpToProcess = new Bitmap(originalBitmap);
                }

                // Gọi hàm recognize bạn đã viết
                var result = await RecognizeAsync(bmpToProcess);

                if (result != null)
                {
                    // Báo kết quả về cho MainWindow qua Event
                    OnDetectionCompleted?.Invoke(result);
                }

                bmpToProcess.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi AnprService: " + ex.Message);
            }
        }
    }
}
