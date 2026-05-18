using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        private readonly HttpClient _httpClient;
        public AnprService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        private bool _isProcessing = false;
        public event Action<AnprResult> OnDetectionCompleted;

        public async Task ProcessAutoDetectionAsync(Bitmap originalBitmap)
        {
            if (_isProcessing) return;

            try
            {
                _isProcessing = true;

                // Thực hiện nhận diện (RecognizeAsync là hàm bạn đã viết gọi API)
                var result = await RecognizeAsync(originalBitmap);

                if (result != null)
                {
                    // Kích hoạt event để MainWindow nhận được
                    OnDetectionCompleted?.Invoke(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi ProcessAutoDetection: " + ex.Message);
            }
            finally
            {
                _isProcessing = false;
            }
        }
        public async Task<AnprResult> RecognizeAsync(Bitmap bmp)
        {
            try
            {
                // 1. Giải phóng CPU: Resize ngay trong bộ nhớ
                using var resizedBmp = new Bitmap(bmp, new Size(800, (bmp.Height * 800) / bmp.Width));
                using var ms = new MemoryStream();
                resizedBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                var byteData = ms.ToArray();

                // 2. Gửi request
                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(byteData), "image", "frame.jpg"); // "image" phải khớp Flask

                // Thiết lập Timeout ngắn để không treo App nếu Flask bị sập
                _httpClient.Timeout = TimeSpan.FromSeconds(10);
                var response = await _httpClient.PostAsync("http://127.0.0.1:5000/process_plate", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                    if (result.results != null && result.results.Count > 0)
                    {
                        string plate = result.results[0].plate;
                        string roiPath = @"D:\APS\AI_Plate_Recognition\static\debug\last_roi.jpg";

                        return new AnprResult
                        {
                            Plate = plate.ToUpper().Replace(" ", ""),
                            RoiImage = LoadImageNoLock(roiPath),
                            SavedPath = roiPath
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi API: " + ex.Message);
            }
            return null;
        }

        private BitmapSource LoadImageNoLock(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                var bitmap = new BitmapImage();
                using (var stream = System.IO.File.OpenRead(path))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        public string SaveFinalImage(BitmapSource image, string plate)
        {
            try
            {
                // 1. Tạo thư mục lưu trữ theo ngày
                string baseFolder = @"D:\APS\SavedImages";
                string dailyFolder = Path.Combine(baseFolder, DateTime.Now.ToString("yyyyMMdd"));

                if (!Directory.Exists(dailyFolder))
                    Directory.CreateDirectory(dailyFolder);

                // 2. Tạo tên file duy nhất
                string fileName = $"{plate}_{DateTime.Now:HHmmss}.jpg";
                string fullPath = Path.Combine(dailyFolder, fileName);

                // 3. Lưu ảnh từ BitmapSource ra file
                using (var fileStream = new FileStream(fullPath, FileMode.Create))
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.QualityLevel = 80; // Giảm nhẹ chất lượng để tiết kiệm ổ cứng
                    encoder.Save(fileStream);
                }

                return fullPath; // Trả về đường dẫn này để lưu vào SQL Server
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi lưu ảnh cuối: " + ex.Message);
                return null;
            }
        }
    }
}
