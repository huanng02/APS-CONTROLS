using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenCvSharp;

namespace QuanLyGiuXe.Services
{
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
            EnableMultipleHttp2Connections = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(3) // Fail fast – don't block RFID flow
        };

        // JPEG encode params: quality 75 → ~3x smaller than default 95, still readable for LPR
        private static readonly ImageEncodingParam[] _jpegParams =
        [
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 75),
            new ImageEncodingParam(ImwriteFlags.JpegOptimize, 1),
        ];

        private const string LprEndpoint = "http://localhost:5000/process_plate";

        public async Task<string> RecognizePlateAsync(Mat frame, CancellationToken ct = default)
        {
            if (frame == null || frame.Empty())
                return string.Empty;

            try
            {
                // Encode Mat to JPEG with optimized quality (non-blocking)
                byte[] jpegBytes = await Task.Run(() =>
                {
                    Cv2.ImEncode(".jpg", frame, out var buf, _jpegParams);
                    return buf;
                }, ct);

                if (jpegBytes == null || jpegBytes.Length == 0)
                    return string.Empty;

                using var content = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(jpegBytes);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "image", "plate.jpg");

                using var response = await _client.PostAsync(LprEndpoint, content, ct);

                if (!response.IsSuccessStatusCode)
                    return string.Empty;

                var responseString = await response.Content.ReadAsStringAsync(ct);
                var result = JsonConvert.DeserializeObject<PlateRecognitionResult>(responseString);

                if (result?.Status == "success" && result.Results?.Count > 0)
                {
                    return result.Results[0].Plate ?? string.Empty;
                }
            }
            catch (OperationCanceledException)
            {
                // Timed out or cancelled – normal flow, don't log as error
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [PlateRecognitionService] LPR failed: {ex.Message}");
            }

            return string.Empty;
        }
    }

    public class PlateRecognitionResult
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("results")]
        public List<PlateInfo> Results { get; set; }
    }

    public class PlateInfo
    {
        [JsonProperty("plate")]
        public string Plate { get; set; }

        [JsonProperty("confidence_yolo")]
        public double ConfidenceYolo { get; set; }
    }
}