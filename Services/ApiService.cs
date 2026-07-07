using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuanLyGiuXe.Services;

public class ApiService
{
    // Tăng timeout lên 15 giây và dùng static để tránh nghẽn socket
    private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<string> SendImageAsync(Bitmap bitmap, string cameraId = "")
    {
        if (bitmap == null) return "Bitmap Null";

        try
        {
            // Bước 1: Tạo bản sao an toàn (Deep Copy) và cắt vùng ROI để tránh lỗi AccessViolation và tăng độ chính xác LPR
            double rx = 0.2;
            double ry = 0.2;
            double rw = 0.6;
            double rh = 0.6;

            try
            {
                int laneId = 0;
                if (!string.IsNullOrEmpty(cameraId))
                {
                    var parts = cameraId.Split('_');
                    if (parts.Length >= 2 && parts[0].Equals("Lane", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(parts[1], out laneId);
                    }
                }

                if (laneId > 0)
                {
                    var cfg = AppConfig.Load();
                    var laneCam = cfg.Cameras.LaneCameras.Find(c => c.LaneId == laneId);
                    if (laneCam != null)
                    {
                        rx = Math.Max(0.0, Math.Min(1.0, laneCam.RoiX));
                        ry = Math.Max(0.0, Math.Min(1.0, laneCam.RoiY));
                        rw = Math.Max(0.0, Math.Min(1.0 - rx, laneCam.RoiWidth));
                        rh = Math.Max(0.0, Math.Min(1.0 - ry, laneCam.RoiHeight));
                    }
                }
            }
            catch { }

            int w = bitmap.Width;
            int h = bitmap.Height;
            int cropX = (int)(w * rx);
            int cropY = (int)(h * ry);
            int cropW = (int)(w * rw);
            int cropH = (int)(h * rh);

            using (Bitmap frameToProcess = new Bitmap(cropW, cropH))
            {
                using (Graphics g = Graphics.FromImage(frameToProcess))
                {
                    g.DrawImage(bitmap, new Rectangle(0, 0, cropW, cropH), new Rectangle(cropX, cropY, cropW, cropH), GraphicsUnit.Pixel);
                }

                using (var ms = new MemoryStream())
                {
                    // Bước 2: Ép lưu định dạng Jpeg chuẩn
                    frameToProcess.Save(ms, ImageFormat.Jpeg);
                byte[] byteArray = ms.ToArray();

                if (byteArray.Length == 0) return "Lỗi nén ảnh";

                using (var content = new MultipartFormDataContent())
                {
                    var imageContent = new ByteArrayContent(byteArray);
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

                    // "image" phải khớp với request.files['image'] bên Python
                    content.Add(imageContent, "image", "frame.jpg");

                    if (!string.IsNullOrEmpty(cameraId))
                    {
                        content.Add(new StringContent(cameraId), "camera_id");
                    }

                    // Bước 3: Gọi API và đợi phản hồi
                    var response = await client.PostAsync("http://127.0.0.1:5001/process_plate", content);

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