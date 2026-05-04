using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    public static class PlateService
    {
        // Chuẩn hóa biển số: Loại bỏ khoảng trắng, dấu chấm, gạch ngang
        public static string Normalize(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return string.Empty;
            return new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
        }

        // So sánh chính xác 100% sau khi đã chuẩn hóa
        public static bool IsMatch(string plateOCR, string plateDB)
        {
            return Normalize(plateOCR) == Normalize(plateDB);
        }
    }
}
