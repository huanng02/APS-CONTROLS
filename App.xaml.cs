using System.Configuration;
using System.Data;
using System.Windows;
using QuanLyGiuXe.Services;

namespace APS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var parkingService = new ParkingLogicService();

            RFIDService.Instance.OnCardScanned += (uid) =>
            {
                Console.WriteLine("🔥 RFID GLOBAL: " + uid);
                parkingService.OnRFIDScanned(uid);
            };

            RFIDService.Instance.Start();
        }
    }

}
