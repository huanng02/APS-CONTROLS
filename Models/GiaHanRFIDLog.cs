using System;

namespace QuanLyGiuXe.Models
{
    public class GiaHanRFIDLog
    {
        public int Id { get; set; }
        public int CardId { get; set; }
        public int SoThang { get; set; }
        public DateTime NgayGiaHan { get; set; }
        public DateTime NgayHetHanMoi { get; set; }

        // Joined properties
        public string CardUID { get; set; }
        public string CardName { get; set; }
        public string BienSo { get; set; }
    }
}
