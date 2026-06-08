using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
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
        private bool _isProcessing = false;

        // Giữ nguyên Event của bạn để MainWindow đăng ký nhận kết quả
        public event Action<AnprResult> OnDetectionCompleted;

        public AnprService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Thiết lập Timeout hợp lý một lần duy nhất trong Constructor
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Hàm xử lý nhận diện tự động từ file ảnh tạm được chụp bởi MediaPlayer
        /// </summary>
        public async Task ProcessAutoDetectionAsync(string imagePath)
        {
            if (_isProcessing) return;

            try
            {
                _isProcessing = true;

                if (!File.Exists(imagePath)) return;

                // Đọc file ảnh ra mảng byte mà không khóa file (để VLC có thể ghi đè ảnh tiếp theo)
                byte[] byteData;
                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    byteData = ms.ToArray();
                }

                // Thực hiện gọi sang Server Flask AI
                var result = await RecognizeBytesAsync(byteData);

                if (result != null)
                {
                    // AN TOÀN LUỒNG: Ép Event chạy trên Dispatcher của UI Thread để MainWindow cập nhật giao diện không bị crash
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnDetectionCompleted?.Invoke(result);
                    });
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

        /// <summary>
        /// Hàm lõi gửi mảng byte ảnh lên Flask API nhận diện
        /// </summary>
        private async Task<AnprResult> RecognizeBytesAsync(byte[] byteData)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(byteData);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "image", "frame.jpg");

                // Gọi tới Endpoint xử lý biển số của Flask
                var response = await _httpClient.PostAsync("http://127.0.0.1:5000/process_plate", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                    if (result.results != null && result.results.Count > 0)
                    {
                        string plate = result.results[0].plate;

                        // TỐI ƯU: Lấy đường dẫn ảnh ROI động do Flask trả về (nếu có), nếu không có thì lấy file tạm cấu hình
                        string roiPath = result.results[0].roi_path ?? @"D:\APS\AI_Plate_Recognition\static\debug\last_roi.jpg";

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
                System.Diagnostics.Debug.WriteLine("Lỗi kết nối Flask API: " + ex.Message);
            }
            return null;
        }

        public BitmapSource LoadImageNoLock(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bitmap = new BitmapImage();
                using (var stream = File.OpenRead(path))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze(); // Cực kỳ quan trọng để chia sẻ luồng đồ họa
                return bitmap;
            }
            catch { return null; }
        }

        /// <summary>
        /// Lưu ảnh bãi xe cuối cùng vào thư mục D:\APS\SavedImages theo ngày để nạp SQL Server
        /// </summary>
        public string SaveFinalImage(BitmapSource image, string plate)
        {
            try
            {
                if (image == null) return null;

                string baseFolder = @"D:\APS\SavedImages";
                string dailyFolder = Path.Combine(baseFolder, DateTime.Now.ToString("yyyyMMdd"));

                if (!Directory.Exists(dailyFolder))
                    Directory.CreateDirectory(dailyFolder);

                string fileName = $"{plate}_{DateTime.Now:HHmmss}.jpg";
                string fullPath = Path.Combine(dailyFolder, fileName);

                using (var fileStream = new FileStream(fullPath, FileMode.Create))
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.QualityLevel = 80;
                    encoder.Save(fileStream);
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi lưu ảnh cuối: " + ex.Message);
                return null;
            }
        }
    }
}