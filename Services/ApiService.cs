using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using QuanLyGiuXe.Services;

public class ApiService
{
    // Tăng timeout lên 15 giây và dùng static để tránh nghẽn socket
    private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<string> SendImageAsync(Bitmap bitmap)
    {
        if (bitmap == null) return "Bitmap Null";

        try
        {
            // Bước 1: Tạo bản sao an toàn (Deep Copy) để tránh lỗi AccessViolation trong .NET 8
            using (Bitmap frameToProcess = new Bitmap(bitmap))
            using (Bitmap resized = new Bitmap(640, 480))
            {
                using (Graphics g = Graphics.FromImage(resized))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(frameToProcess, 0, 0, 640, 480);
                }

                using (var ms = new MemoryStream())
                {
                    // Bước 2: Ép lưu định dạng Jpeg chuẩn
                    resized.Save(ms, ImageFormat.Jpeg);
                    byte[] byteArray = ms.ToArray();

                    if (byteArray.Length == 0) return "Lỗi nén ảnh";

                    using (var content = new MultipartFormDataContent())
                    {
                        var imageContent = new ByteArrayContent(byteArray);
                        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

                        // "image" phải khớp với request.files['image'] bên Python
                        content.Add(imageContent, "image", "frame.jpg");

                        // Bước 3: Gọi API và đợi phản hồi
                        var response = await client.PostAsync("http://127.0.0.1:5000/process_plate", content);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var result = JsonConvert.DeserializeObject<PlateResponse>(json);

                            if (result?.results != null && result.results.Count > 0)
                            {
                                return result.results[0].plate;
                            }
                            return "Không thấy biển";
                        }
                        return $"Lỗi Server: {response.StatusCode}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return $"Lỗi: {ex.Message}";
        }
    }
    public static async Task<BitmapSource> DownloadRoiImageAsync()
    {
        try
        {
            string url = "http://127.0.0.1:5000/static/debug/last_roi.jpg"; // Thay bằng URL thực tế của bạn
            using (HttpClient client = new HttpClient())
            {
                byte[] imageBytes = await client.GetByteArrayAsync(url);
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // Đọc xong nhả bộ nhớ ngay
                    bitmap.EndInit();
                    bitmap.Freeze(); // Cho phép dùng ở nhiều Thread
                    return bitmap;
                }
            }
        }
        catch
        {
            return null;
        }
    }
    // Cấu trúc Class để hứng dữ liệu từ Flask
    public class PlateResponse
    {
        // Thêm JsonProperty để chắc chắn nó khớp với từ "results" trong JSON của Python
        [JsonProperty("results")]
        public List<PlateResult> results { get; set; }

        [JsonProperty("status")]
        public string status { get; set; }

        [JsonProperty("debug_url")]
        public string debug_url { get; set; }
    }

    public class PlateResult
    {
        [JsonProperty("plate")]
        public string plate { get; set; }

        [JsonProperty("confidence_yolo")]
        public double confidence_yolo { get; set; }

        [JsonProperty("box")]
        public List<int> box { get; set; }
    }
}