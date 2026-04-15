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
                LoggingService.Instance.LogInfo("PlateRecognized", "ParkingLogicService", plate, plate: plate);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("PlateProcessError", "ParkingLogicService", "Error processing frame", ex);
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
            try
            {
                var card = db.GetRFIDCardByUid(uid);
                if (card == null || card.Id == 0)
                {
                    Console.WriteLine("❌ Thẻ chưa đăng ký");
                    LoggingService.Instance.LogInfo("RFIDUnregisteredCard", "ParkingLogicService", uid);
                    return;
                }
                if (string.IsNullOrEmpty(currentPlate))
                {
                    Console.WriteLine("❌ Không có biển");
                    LoggingService.Instance.LogInfo("RFIDNoPlate", "ParkingLogicService", uid);
                    return;
                }
                if (XeDaTrongBai(currentPlate))
                {
                    // XE RA
                    Console.WriteLine("Xe ra: " + currentPlate);
                    LoggingService.Instance.LogInfo("XeRa", "ParkingLogicService", currentPlate, plate: currentPlate);
                    // calculate fee
                    DateTime timeIn = plateTime;
                    DateTime timeOut = DateTime.Now;
                    var loaiVeId = card.LoaiVeId;
                    var loaiXeId = card.LoaiXeId;
                    var payment = new PaymentService();
                    decimal fee = payment.CalculateFee(loaiVeId == 0 ? (int?)null : loaiVeId, loaiXeId == 0 ? (int?)null : loaiXeId, timeIn, timeOut);

                    db.LuuLichSu(currentPlate, plateTime, DateTime.Now, Convert.ToDouble(fee), string.Empty);
                }
                else
                {
                    // XE VÀO
                    Console.WriteLine("Xe vào: " + currentPlate);
                    LoggingService.Instance.LogInfo("XeVao", "ParkingLogicService", currentPlate, plate: currentPlate);
                    db.ThemXe(currentPlate, uid, "");
                }
                currentPlate = "";
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("OnRFIDScannedError", "ParkingLogicService", "Error handling RFID scan", ex);
            }
        }

        public bool XeDaTrongBai(string bienSo)
        {
            var table = db.LayXeTrongBai();
            return table.AsEnumerable()
                .Any(row => row["BienSo"].ToString() == bienSo);
        }

    }
}
