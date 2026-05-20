using System;
using System.IO.Ports;

namespace QuanLyGiuXe.Services
{
    public class RFIDService
    {
        SerialPort port;
        public static RFIDService Instance = new RFIDService();
        string lastUID = "";
        DateTime lastScan = DateTime.MinValue;

        public event Action<string> OnCardScanned;

        public void Start()
        {
            try
            {
                if (port != null && port.IsOpen)
                    return;

                port = new SerialPort("COM3", 9600);
                port.DataReceived += Port_DataReceived;

                port.Open();

                Console.WriteLine("✅ RFID Connected");
                try { LoggingService.Instance.LogAudit("RFID_CONNECTED", "RFIDCard", string.Empty, null, null, "RFIDService", null, null, "RFID connected to COM3"); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ RFID not available: " + ex.Message);
                try { LoggingService.Instance.LogSecurity("RFID_START_FAILED", "RFIDService", ex.Message, null, null, ex); } catch { }
            }
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string uid = ChuanHoaUID(port.ReadLine());

                if (uid == lastUID && (DateTime.Now - lastScan).TotalSeconds < 2)
                    return;

                lastUID = uid;
                lastScan = DateTime.Now;

                try { LoggingService.Instance.LogVehicle("RFID_READ", uid, null, null, null, "RFIDService"); } catch { }

                OnCardScanned?.Invoke(uid);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("RFIDReadError", "RFIDService", "Error reading UID", ex);
            }
        }

        public static string ChuanHoaUID(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return string.Empty;
            return new string(uid
                   .Where(c => char.IsLetterOrDigit(c)) // ⭐ chỉ giữ chữ và số
                   .ToArray())
                   .ToUpper();
        }
        
    }
}