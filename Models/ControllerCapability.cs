using System;

namespace QuanLyGiuXe.Models
{
    public enum ControllerType
    {
        C3200,
        C3400
    }

    public class ControllerCapability
    {
        public ControllerType Type { get; set; }
        public int ReaderCount { get; set; }
        public int RelayCount { get; set; }
        public int MaxSupportedLanes { get; set; }
    }
}
