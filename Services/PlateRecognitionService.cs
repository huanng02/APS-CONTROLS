using System.Net.Http;
using Newtonsoft.Json;

public class PlateRecognitionService
{
    private readonly HttpClient _client = new HttpClient();

    public async Task<string> GetPlate()
    {
        try
        {
            var res = await _client.GetStringAsync("http://localhost:5000/plate");
            dynamic json = JsonConvert.DeserializeObject(res);
            return json.plate;
        }
        catch
        {
            return "";
        }
    }
}