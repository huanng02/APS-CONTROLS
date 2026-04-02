using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    public class ParkingLogicService
    {
       
        private DateTime lastTriggerTime = DateTime.MinValue;
        private DatabaseService db = new DatabaseService();
        private bool isProcessing = false;
        private string lastPlate = "";
        private DateTime lastTime = DateTime.MinValue;

        private string currentPlate = "";
        private DateTime plateTime;

        public async Task ProcessFrame(Bitmap bitmap)
        {
            if (isProcessing) return;

            isProcessing = true;

            try
            {
                var plate = await ApiService.SendImageAsync(bitmap);

                if (string.IsNullOrEmpty(plate))
                    return;

                // chống lặp
                if ((DateTime.Now - lastTriggerTime).TotalSeconds < 2)
                    return;
                if (plate == lastPlate &&
                     (DateTime.Now - lastTime).TotalSeconds < 5)
                    return;

                lastPlate = plate;
                lastTime = DateTime.Now;
                lastTriggerTime = DateTime.Now;

                currentPlate = plate;
                plateTime = DateTime.Now;

                Console.WriteLine("Plate: " + plate);
            }
            finally
            {
                await Task.Delay(500); // throttle
                isProcessing = false;
            }
        }

        // gọi từ RFIDService
        public void OnRFIDScanned(string uid)
        {
            if (!db.CheckCardExists(uid))
            {
                Console.WriteLine("❌ Thẻ chưa đăng ký");
                return;
            }
            if (string.IsNullOrEmpty(currentPlate))
            {
                Console.WriteLine("❌ Không có biển");
                return;
            }
            if (XeDaTrongBai(currentPlate))
            {
                // XE RA
                Console.WriteLine("Xe ra: " + currentPlate);
            }
            else
            {
                // XE VÀO
                Console.WriteLine("Xe vào: " + currentPlate);
                db.ThemXe(currentPlate, uid, "");
            }
            currentPlate = "";
        }

        public bool XeDaTrongBai(string bienSo)
        {
            var table = db.LayXeTrongBai();
            return table.AsEnumerable()
                .Any(row => row["BienSo"].ToString() == bienSo);
        }

    }
}
