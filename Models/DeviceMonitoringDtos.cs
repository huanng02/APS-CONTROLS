using System;
using System.Collections.Generic;

namespace QuanLyGiuXe.Models
{
    // ── Infrastructure Summary ──────────────────────────────────────────
    public class InfrastructureSummaryDto
    {
        public int TotalControllers { get; set; }
        public int OnlineControllers { get; set; }
        public double ControllerHealth => TotalControllers > 0 ? (double)OnlineControllers / TotalControllers * 100 : 0;

        public int TotalReaders { get; set; }
        public int OnlineReaders { get; set; }
        public double ReaderHealth => TotalReaders > 0 ? (double)OnlineReaders / TotalReaders * 100 : 0;


        public int TotalCameras { get; set; }
        public int OnlineCameras { get; set; }
        public double CameraHealth => TotalCameras > 0 ? (double)OnlineCameras / TotalCameras * 100 : 0;

        public int TotalLanes { get; set; }
        public int OnlineLanes { get; set; }
        public double LaneHealth => TotalLanes > 0 ? (double)OnlineLanes / TotalLanes * 100 : 0;
    }

    // ── Lane Status (Enhanced) ──────────────────────────────────────────
    public class LaneStatusDto
    {
        public int Id { get; set; }
        public string LaneCode { get; set; } = string.Empty;
        public string LaneName { get; set; } = string.Empty;
        public string Direction { get; set; } = "IN";
        public string ZoneName { get; set; } = string.Empty;
        public string GateName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsOnline { get; set; } = false;
        public string AssociatedCamera { get; set; } = string.Empty;
        public bool CameraOnline { get; set; } = false;
        public string StatusDetails { get; set; } = string.Empty;

        // Enhanced sub-device health
        public string ControllerStatus { get; set; } = "N/A";
        public string ReaderStatus { get; set; } = "N/A";
        public string CameraStatus { get; set; } = "N/A";
        public string OverallHealth { get; set; } = "Unknown";
    }

    // ── C3 Controller Status ────────────────────────────────────────────
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

    // ── RFID Reader Status ──────────────────────────────────────────────
    public class RfidReaderStatusDto
    {
        public int ReaderNo { get; set; }
        public string ReaderName { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = "C3 Controller";
        public string PortOrAddress { get; set; } = string.Empty;
        public string AssociatedLaneName { get; set; } = string.Empty;
        public bool IsOnline { get; set; } = false;
        public string StatusDetails { get; set; } = string.Empty;
    }


    // ── Camera Status ───────────────────────────────────────────────────
    public class CameraStatusDto
    {
        public int Id { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string LaneName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline";
        public bool IsOnline { get; set; } = false;
        public DateTime? LastFrameTime { get; set; }
        public string RtspUrl { get; set; } = string.Empty;
        public string StatusDetails { get; set; } = string.Empty;
    }

    // ── Device Event ────────────────────────────────────────────────────
    public class DeviceEventDto
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string DeviceType { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string Description { get; set; } = string.Empty;
    }

    // ── Device Detail ───────────────────────────────────────────────────
    public class DeviceDetailDto
    {
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = "N/A";
        public string CurrentStatus { get; set; } = "Unknown";
        public bool IsOnline { get; set; } = false;
        public DateTime? LastCommunication { get; set; }
        public string StatusDetails { get; set; } = string.Empty;
        public List<DeviceEventDto> RecentEvents { get; set; } = new();
    }
}
