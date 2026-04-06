using System.IO;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services
{
    public sealed class AppConfig
    {
        public ZKTecoConfig ZKTeco { get; set; } = new();

        public static AppConfig Load(string fileName = "config.json")
        {
            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, fileName),
                fileName
            };

            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var json = File.ReadAllText(path);
                    return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                }
            }

            return new AppConfig();
        }
    }

    public sealed class ZKTecoConfig
    {
        public string Protocol { get; set; } = "TCP";
        public string IpAddress { get; set; } = "192.168.1.201";
        public int TcpPort { get; set; } = 4370;
        public int Timeout { get; set; } = 3000;
        public string Password { get; set; } = "";
        public int BarrierDuration { get; set; } = 5;
    }
}
