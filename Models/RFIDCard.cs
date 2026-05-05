using System;

namespace QuanLyGiuXe.Models
{
    public class RFIDCard
    {
        public int Id { get; set; }
        public string UID { get; set; } = string.Empty;
        public string BienSo { get; set; } = string.Empty;
        // Human-friendly name for the card
        public string CardName { get; set; } = string.Empty;
        // Backwards-compatibility alias: some code may still use CarsName
        [Obsolete("Use CardName instead")]
        public string CardsName
        {
            get => CardName;
            set => CardName = value;
        }
        public int LoaiVeId { get; set; }
        public int LoaiXeId { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public DateTime NgayTao { get; set; }
        // Optional expiry date stored in DB (nullable)
        public DateTime? NgayHetHan { get; set; }
    }
}
