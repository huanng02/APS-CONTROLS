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

        [Required(ErrorMessage = "Vui lòng nhập Zone ID")]
        [Description("ID của Zone chứa tủ này (VD: 1)")]
        public int ZoneId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helper
        public string ZoneName { get; set; } = string.Empty;
    }
}
