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
                LoggingService.Instance.LogInfo("RFIDConnected", "RFIDService", "RFID connected to COM3");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ RFID not available: " + ex.Message);
                LoggingService.Instance.LogError("RFIDStartFailed", "RFIDService", ex.Message, ex);
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

                LoggingService.Instance.LogInfo("RFIDRead", "RFIDService", uid);

                OnCardScanned?.Invoke(uid);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("RFIDReadError", "RFIDService", "Error reading UID", ex);
            }
        }

        public static string ChuanHoaUID(string uid)
        {
            return new string(uid
                   .Where(c => char.IsLetterOrDigit(c)) // ⭐ chỉ giữ chữ và số
                   .ToArray())
                   .ToUpper();
        }
        
    }
}