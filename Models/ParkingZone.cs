using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QuanLyGiuXe.Models
{
    public class ParkingZone
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Site ID")]
        [Description("ID của Site mà Zone này thuộc về (VD: 1)")]
        public int SiteId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mã Zone")]
        [Description("Mã khu vực viết liền không dấu (VD: ZONE_A)")]
        public string ZoneCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Tên Zone")]
        [Description("Tên hiển thị của khu vực (VD: Khu vực A)")]
        public string ZoneName { get; set; } = string.Empty;

        [Description("Mô tả thêm về khu vực này")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Sức chứa")]
        [Description("Sức chứa tối đa (phải là số nguyên)")]
        public int MaxCapacity { get; set; } = 100;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helpers
        public string SiteCode { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
    }
}
