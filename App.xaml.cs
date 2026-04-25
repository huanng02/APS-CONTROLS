using System.Configuration;
using System.Data;
using System.Windows;
using QuanLyGiuXe.Services;
using Serilog;

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

            // initialize Serilog via LoggingService
            var _ = LoggingService.Instance; // ensures static init
            LoggingService.Instance.LogInfo("AppStart", "App", "Application starting");

            // global exception handlers
            this.DispatcherUnhandledException += (s, ex) =>
            {
                LoggingService.Instance.LogError("UnhandledException", "App.Dispatcher", "Unhandled UI exception", ex.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                if (ex.ExceptionObject is Exception exception)
                    LoggingService.Instance.LogError("UnhandledException", "AppDomain", "Unhandled domain exception", exception);
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                LoggingService.Instance.LogError("UnobservedTaskException", "TaskScheduler", "Unobserved task exception", ex.Exception);
            };

            var parkingService = new ParkingLogicService();

            RFIDService.Instance.OnCardScanned += (uid) =>
            {
                LoggingService.Instance.LogInfo("RFIDScanned", "RFIDService", uid);
                parkingService.OnRFIDScanned(uid);
            };

            RFIDService.Instance.Start();
            LoggingService.Instance.LogInfo("RFIDStart", "RFIDService", "RFID service started");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggingService.Instance.LogInfo("AppExit", "App", "Application exiting");
            LoggingService.Instance.Shutdown();
            base.OnExit(e);
        }
    }

}
