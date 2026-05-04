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

                int cardId = card.Id;

                // If there's an active entry for this CardId -> process EXIT
                if (db.IsXeTrongBaiByCardId(cardId))
                {
                    var rec = db.GetXeTrongBaiRecordByCardId(cardId);
                    if (rec == null)
                    {
                        LoggingService.Instance.LogInfo("XeRaNotFound", "ParkingLogicService", $"CardId={cardId} reported active but no record found");
                        return;
                    }

                    // compute fee
                    DateTime timeIn = rec.Value.ThoiGianVao;
                    DateTime timeOut = DateTime.Now;
                    int? loaiVeId = card.LoaiVeId > 0 ? card.LoaiVeId : (int?)null;
                    int? loaiXeId = card.LoaiXeId > 0 ? card.LoaiXeId : (int?)null;

                    double fee = db.TinhTien(loaiXeId, loaiVeId, timeIn, timeOut);

                    // persist exit
                    try
                    {
                        db.UpdateXeRaById(rec.Value.Id, DateTime.Now);
                        db.LuuLichSu(rec.Value.BienSo == string.Empty ? null : rec.Value.BienSo, timeIn, DateTime.Now, fee, string.Empty, uid);
                        db.XoaXeByCardId(cardId);
                        LoggingService.Instance.LogInfo("XeRa", "ParkingLogicService", $"CardId={cardId} exited, Fee={fee}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("XeRaDbError", "ParkingLogicService", $"CardId={cardId}", ex);
                        throw;
                    }
                }
                else
                {
                    // XE VÀO: insert with optional plate (currentPlate may be empty)
                    try
                    {
                        db.ThemXe(cardId, string.IsNullOrEmpty(currentPlate) ? null : currentPlate, "");
                        LoggingService.Instance.LogInfo("XeVao", "ParkingLogicService", $"CardId={cardId} checked in, Plate={(string.IsNullOrEmpty(currentPlate) ? "NULL" : currentPlate)}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("XeVaoDbError", "ParkingLogicService", $"CardId={cardId}", ex);
                        throw;
                    }
                }

                // clear last OCR plate buffer
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
