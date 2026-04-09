using System.IO;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services
{
    public sealed class AppConfig
    {
        public ZKTecoConfig ZKTeco { get; set; } = new();
        public CameraConfig Cameras { get; set; } = new();
        public bool ShowLog { get; set; } = true;

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

        public void Save(string fileName = "config.json")
        {
            var path = File.Exists(fileName)
                ? fileName
                : Path.Combine(AppContext.BaseDirectory, fileName);

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
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
        // Cooldown (ms) to ignore repeated card scans for the same UID
        public int CardCooldownMs { get; set; } = 2000;
        // Physical door mapping: which physical door number is used for logical IN/OUT actions
        // Default: GateIn -> physical door 1, GateOut -> physical door 2
        public int GateInDoor { get; set; } = 1;
        public int GateOutDoor { get; set; } = 2;
        // support comma-separated lists for mapping multiple readers to IN/OUT (e.g. "1,3")
        public string GateInDoors { get; set; } = "1";
        public string GateOutDoors { get; set; } = "2";
        // Button action config: "Disabled" | "OpenThisDoor" | "OpenGroupIn" | "OpenGroupOut"
        public string Button1Action { get; set; } = "OpenThisDoor";
        public string Button2Action { get; set; } = "OpenThisDoor";

        // Helpers to parse configured door groups and map physical->logical
        public HashSet<int> GetInSet()
        {
            var set = new HashSet<int>();
            if (!string.IsNullOrWhiteSpace(GateInDoors))
            {
                foreach (var part in GateInDoors.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(part.Trim(), out var v)) set.Add(v);
            }
            else
            {
                set.Add(GateInDoor);
            }
            return set;
        }

        public HashSet<int> GetOutSet()
        {
            var set = new HashSet<int>();
            if (!string.IsNullOrWhiteSpace(GateOutDoors))
            {
                foreach (var part in GateOutDoors.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(part.Trim(), out var v)) set.Add(v);
            }
            else
            {
                set.Add(GateOutDoor);
            }
            return set;
        }

        public int MapPhysicalToLogical(int door)
        {
            if (door == 0) return 0;
            if (ForceAllIn) return 1;
            if (ForceAllOut) return 2;
            var inSet = GetInSet();
            var outSet = GetOutSet();
            if (inSet.Contains(door)) return 1;
            if (outSet.Contains(door)) return 2;
            return 0;
        }
        // If true, force all scans to be treated as IN (no OUT flow)
        public bool ForceAllIn { get; set; } = false;
        // If true, force all scans to be treated as OUT (no IN flow)
        public bool ForceAllOut { get; set; } = false;
    }

    public sealed class CameraConfig
    {
        public string VaoToanCanh { get; set; } = "";
        public string VaoBienSo { get; set; } = "";
        public string RaToanCanh { get; set; } = "";
        public string RaBienSo { get; set; } = "";
    }
}
