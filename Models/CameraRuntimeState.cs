using System;

namespace QuanLyGiuXe.Models
{
    public class CameraRuntimeState
    {
        public string CameraKey { get; set; } = string.Empty;
        public bool IsConnected { get; set; } = false;
        public DateTime? LastHeartbeat { get; set; }
        public DateTime? LastFrameReceived { get; set; }
        public int ReconnectCount { get; set; } = 0;
    }
}
