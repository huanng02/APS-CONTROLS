using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenCvSharp;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Kết quả nhận diện biển số từ LPR server.
    /// </summary>
    public class LprResult
    {
        /// <summary>Biển số nhận diện được (chuỗi rỗng nếu không nhận được).</summary>
        public string Plate { get; set; } = string.Empty;

        /// <summary>Ảnh vùng biển số đã cắt (ROI) từ server, dạng JPEG bytes. Null nếu không detect.</summary>
        public byte[]? PlateCropBytes { get; set; }
    }

    public class PlateRecognitionService
    {
        private static readonly Lazy<PlateRecognitionService> _instance = new(() => new PlateRecognitionService());
        public static PlateRecognitionService Instance => _instance.Value;

        // Static HttpClient: reuses TCP connection (keep-alive), avoids socket exhaustion
        private static readonly HttpClient _client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 4,
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // JPEG encode params: quality 75 → ~3x smaller than default 95, still readable for LPR
        private static readonly ImageEncodingParam[] _jpegParams =
        [
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 75),
            new ImageEncodingParam(ImwriteFlags.JpegOptimize, 1),
        ];

        private const string LprEndpoint = "http://localhost:5000/process_plate";

        /// <summary>
        /// Nhận diện biển số từ frame camera.
        /// Trả về LprResult gồm biển số và ảnh crop (nếu có).
        /// </summary>
        public async Task<LprResult> RecognizePlateAsync(Mat frame, CancellationToken ct = default)
        {
            var result = new LprResult();

            if (frame == null || frame.Empty())
                return result;

            try
            {
                byte[] jpegBytes = await Task.Run(() =>
                {
                    Cv2.ImEncode(".jpg", frame, out var buf, _jpegParams);
                    return buf;
                }, ct);

                if (jpegBytes == null || jpegBytes.Length == 0)
                    return result;

                using var content = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(jpegBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "image", "plate.jpg");

                using var response = await _client.PostAsync(LprEndpoint, content, ct);
                if (!response.IsSuccessStatusCode)
                    return result;

                var responseString = await response.Content.ReadAsStringAsync(ct);
                var json = JsonConvert.DeserializeObject<PlateRecognitionResponse>(responseString);

                if (json?.Status == "success" && json.Results?.Count > 0)
                {
                    result.Plate = json.Results[0].Plate ?? string.Empty;
                }

                // Parse crop image base64 nếu server trả về
                if (!string.IsNullOrEmpty(json?.PlateCropB64))
                {
                    try { result.PlateCropBytes = Convert.FromBase64String(json.PlateCropB64); }
                    catch { /* ignore decode error */ }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PlateRecognitionService] LPR failed: {ex.Message}");
            }

            return result;
        }
    }

    public class PlateRecognitionResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("results")]
        public List<PlateInfo> Results { get; set; }

        [JsonProperty("plate_crop_b64")]
        public string PlateCropB64 { get; set; }
    }

    public class PlateInfo
    {
        [JsonProperty("plate")]
        public string Plate { get; set; }

        [JsonProperty("confidence_yolo")]
        public double ConfidenceYolo { get; set; }
    }
}