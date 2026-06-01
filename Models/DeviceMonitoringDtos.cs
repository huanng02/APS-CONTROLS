using System;

namespace QuanLyGiuXe.Models
{
    public class DeviceKpiDto
    {
        public int XeTrongBai { get; set; }
        public int LuotXeVaoHomNay { get; set; }
        public int LuotXeRaHomNay { get; set; }
        public double DoanhThuHomNay { get; set; }
        public int TongCho { get; set; }
        public double TyLeLapDay => TongCho > 0 ? (double)XeTrongBai / TongCho * 100 : 0;
        public int ChoTrong => TongCho - XeTrongBai;
    }

    public class LaneStatusDto
    {
        public int Id { get; set; }
        public string LaneCode { get; set; } = string.Empty;
        public string LaneName { get; set; } = string.Empty;
        public string Direction { get; set; } = "IN"; // IN, OUT
        public string ZoneName { get; set; } = string.Empty;
        public string GateName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsOnline { get; set; } = false;
        public string AssociatedCamera { get; set; } = string.Empty;
        public bool CameraOnline { get; set; } = false;
        public string StatusDetails { get; set; } = string.Empty;
    }

    public class C3ControllerStatusDto
    {
        public int Id { get; set; }
        public string ControllerName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string GateName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsOnline { get; set; } = false;
        public long PingLatencyMs { get; set; } = -1;
        public string StatusDetails { get; set; } = string.Empty;
    }

    public class RfidReaderStatusDto
    {
        public int ReaderNo { get; set; } // 1, 2, 3, 4
        public string ReaderName { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = "C3 Controller"; // C3 Controller or USB COM
        public string PortOrAddress { get; set; } = string.Empty; // COM3 or C3 IP reader number
        public string AssociatedLaneName { get; set; } = string.Empty;
        public bool IsOnline { get; set; } = false;
        public string StatusDetails { get; set; } = string.Empty;
    }
}
