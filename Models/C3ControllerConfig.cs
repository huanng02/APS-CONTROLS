using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QuanLyGiuXe.Models
{
    public class C3ControllerConfig
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Tên Controller")]
        [Description("Tên hiển thị của mạch (VD: C3-200 Làn 1-2)")]
        public string ControllerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập IP Address")]
        [Description("Địa chỉ IP của tủ (VD: 192.168.1.201)")]
        public string IpAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập IP Server")]
        [Description("Địa chỉ IP của Server (VD: 192.168.1.100)")]
        public string ServerIp { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập IP Máy thực hiện (PC)")]
        [Description("Địa chỉ IP của máy dùng để quản lý/điều khiển board (VD: 192.168.1.50)")]
        public string PcIp { get; set; } = string.Empty;

        [Description("ID của Cổng kiểm soát chứa tủ này (VD: 1)")]
        public int? GateId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helper
        public string GateName { get; set; } = string.Empty;

        // Backward Compatibility for legacy controllers/views
        [Obsolete("Use GateId instead")]
        public int ZoneId
        {
            get => GateId ?? 0;
            set => GateId = value == 0 ? null : value;
        }

        [Obsolete("Use GateName instead")]
        public string ZoneName
        {
            get => GateName;
            set => GateName = value;
        }
    }
}
