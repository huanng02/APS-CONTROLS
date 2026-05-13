using System.Configuration;
using System.Data;
using System.Windows;
using System.Linq;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Models;
using Serilog;

namespace QuanLyGiuXe
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // initialize Serilog via LoggingService
            var _ = LoggingService.Instance; 
            LoggingService.Instance.LogInfo("AppStart", "App", "Application starting");

            // Kích hoạt hệ thống xử lý lỗi toàn cục (Global Exception Handling)
            QuanLyGiuXe.Services.ErrorHandling.GlobalExceptionHandler.Initialize();

            StartLoginFlow();
        }

        private bool _isLoggingOut = false;

        private void SafeShutdown(string reason)
        {
            LoggingService.Instance.LogInfo("App", "App", $"SAFE SHUTDOWN REQUESTED: {reason}");
            this.Shutdown();
        }

        public void PerformLogout()
        {
            // Audit logout for current user (best-effort)
            try { LoggingService.Instance.LogSecurity("LOGOUT", "Auth", "{\"Action\":\"Logout\"}", userId: CurrentUser.Id > 0 ? CurrentUser.Id.ToString() : null, username: CurrentUser.Username); } catch { }

            _isLoggingOut = true;
            try
            {
                // 1. Clear session
                CurrentUser.Clear();
                
                // 2. Suppress toasts immediately (prevent DB/C3 toasts during transition)
                ToastNotificationService.Instance.IsSuppressed = true;
                ToastNotificationService.Instance.ClearQueue();
                
                // 3. Dừng monitor trên luồng nền (tránh block UI ~2s). Login sau sẽ Restart().
                _ = System.Threading.Tasks.Task.Run(() => ConnectionMonitorService.Instance.Stop());

                // 3. Close ALL open windows (including Toasts/Notifications)
                this.Dispatcher.BeginInvoke(new Action(() => {
                    LoggingService.Instance.LogInfo("App", "App", "PerformLogout: Cleaning up all windows.");
                    
                    // We need a list because we can't modify the collection while iterating
                    var windows = Application.Current.Windows.Cast<Window>().ToList();
                    foreach (var window in windows)
                    {
                        try 
                        { 
                            window.Hide(); // Hide first for visual smoothness
                            window.Close(); 
                        } 
                        catch { }
                    }
                    
                    this.MainWindow = null; 
                }));

                // 4. Restart Login Flow
                this.Dispatcher.BeginInvoke(new Action(() => {
                    StartLoginFlow();
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("App", "App", "Error during logout", ex);
                _isLoggingOut = false;
                SafeShutdown("ErrorDuringLogout");
            }
        }

        private void StartLoginFlow(Window? windowToClose = null)
        {
            LoggingService.Instance.LogInfo("App", "App", "StartLoginFlow: Showing LoginForm.");
            var loginForm = new QuanLyGiuXe.Views.LoginForm();
            var result = loginForm.ShowDialog();
            
            LoggingService.Instance.LogInfo("App", "App", $"StartLoginFlow: LoginForm result = {result}");

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                LoggingService.Instance.LogInfo("App", "App", "StartLoginFlow: Login successful.");
                
                // Now we can safely close the old window
                windowToClose?.Close();
                
                var main = new MainWindow();
                this.MainWindow = main;
                
                main.Closed += (s, e) => {
                    if (!_isLoggingOut)
                    {
                        SafeShutdown("MainWindowClosedViaX");
                    }
                };
                
                main.Show();
                _isLoggingOut = false; 
                // Re-enable toasts after new window is ready
                ToastNotificationService.Instance.IsSuppressed = false;
                LoggingService.Instance.LogInfo("App", "App", "StartLoginFlow: New MainWindow shown.");
            }
            else
            {
                _isLoggingOut = false;
                windowToClose?.Close(); // Clean up even on cancel
                SafeShutdown("LoginCancelledByUser");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggingService.Instance.LogInfo("AppExit", "App", $"Application exiting with code: {e.ApplicationExitCode}");
            LoggingService.Instance.Shutdown();
            base.OnExit(e);
        }
    }

}
