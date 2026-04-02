using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

public class ApiService
{
    private static readonly HttpClient client = new HttpClient();

    public static async Task<string> SendImageAsync(Bitmap bitmap)
    {
        try
        {
            using (var content = new MultipartFormDataContent())
            using (var ms = new MemoryStream())
            {
                // resize để nhẹ hơn
                Bitmap resized = new Bitmap(bitmap, new Size(640, 480));

                resized.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                var byteArray = ms.ToArray();

                var imageContent = new ByteArrayContent(byteArray);
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

                content.Add(imageContent, "image", "frame.jpg");

                var response = await client.PostAsync("http://127.0.0.1:5000/recognize", content);

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PlateResponse>(json);

                return result.plate;
            }
        }
        catch
        {
            return "";
        }
    }
}