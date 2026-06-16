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
        private static readonly Services.HttpApiServer _apiServer = new Services.HttpApiServer();

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler((s, ev) =>
                {
                    if (s is Window window && window.Icon == null)
                    {
                        try
                        {
                            window.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/QuanLyGiuXe;component/Assets/app.ico"));
                        }
                        catch { }
                    }
                }));

                System.Environment.SetEnvironmentVariable("OPENCV_FFMPEG_CAPTURE_OPTIONS", "rtsp_transport;tcp|fflags;nobuffer|flags;low_delay|stimeout;5000000");
                base.OnStartup(e);
                this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Check License
                string licenseError;
                bool isLicenseValid = LicenseValidationService.Instance.CheckLicenseOffline(out licenseError);
                if (!isLicenseValid)
                {
                    // Show friendly notice first or launch activation directly
                    var activationWin = new Views.LicenseActivationWindow();
                    var activated = activationWin.ShowDialog();
                    if (activated != true && !activationWin.IsActivated)
                    {
                        System.Windows.MessageBox.Show("Ứng dụng bị từ chối khởi động do không có bản quyền hợp lệ.", "Lỗi Bản Quyền", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        this.Shutdown(1);
                        return;
                    }
                }

                // Start background periodic check for live revocation detection
                LicenseMonitorService.Instance.Start(errorMsg =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            $"CẢNH BÁO BẢN QUYỀN:\n\n{errorMsg}\n\nỨng dụng sẽ tự động đóng để bảo vệ hệ thống.",
                            "Bản Quyền Bị Thu Hồi",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);

                        try
                        {
                            System.Diagnostics.Process.GetCurrentProcess().Kill();
                        }
                        catch
                        {
                            System.Windows.Application.Current.Shutdown(1);
                        }
                    });
                });

                // Start HTTP API Server
                _apiServer.Start();

                // initialize Serilog via LoggingService
                var _ = LoggingService.Instance; 
                LoggingService.Instance.LogInfo("AppStart", "App", "Application starting");

                // Kích hoạt hệ thống xử lý lỗi toàn cục (Global Exception Handling)
                QuanLyGiuXe.Services.ErrorHandling.GlobalExceptionHandler.Initialize();

                // Khởi động Backup Scheduler
                QuanLyGiuXe.Services.Backup.BackupScheduler.Instance.Start();

                // Run tests if --test argument is passed
                if (e.Args.Contains("--test"))
                {
                    try
                    {
                        QuanLyGiuXe.Tests.LaneDirectionConsistencyTests.Run();
                        QuanLyGiuXe.Tests.SecurityPermissionMatrixTests.Run();
                        QuanLyGiuXe.Tests.EnterpriseCrudTests.Run();
                        this.Shutdown(0);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("TEST FAILURE: " + ex.Message);
                        LoggingService.Instance.LogError("TestRun", "App", "Tests failed", ex);
                        LoggingService.Instance.Shutdown();
                        this.Shutdown(1);
                    }
                    return;
                }

                StartLoginFlow();
            }
            catch (Exception ex)
            {
                try
                {
                    LoggingService.Instance.LogError("StartupCrash", "App", "Fatal exception during startup", ex);
                    LoggingService.Instance.Shutdown();
                }
                catch { }

                System.Windows.MessageBox.Show(
                    $"Ứng dụng gặp lỗi nghiêm trọng khi khởi động:\n\n{ex.Message}\n\nChi tiết:\n{ex.ToString()}",
                    "Lỗi Khởi Động Hệ Thống",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);

                this.Shutdown(1);
            }
        }

        private bool _isLoggingOut = false;

        private void SafeShutdown(string reason)
        {
            try
            {
                LoggingService.Instance.LogInfo("App", "App", $"SAFE SHUTDOWN REQUESTED: {reason}");
            }
            catch { }
            
            try
            {
                this.Shutdown();
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                catch { }
            }
        }

        public void PerformLogout()
        {
            // Audit logout for current user (best-effort)
            try { LoggingService.Instance.LogSecurity("LOGOUT", "Auth", "{\"Action\":\"Logout\"}", userId: CurrentUserContext.Instance.Id > 0 ? CurrentUserContext.Instance.Id.ToString() : null, username: CurrentUserContext.Instance.Username); } catch { }

            _isLoggingOut = true;
            try
            {
                // 1. Clear session
                CurrentUserContext.Instance.Clear();
                
                // 2. Suppress toasts immediately (prevent DB/C3 toasts during transition)
                ToastNotificationService.Instance.IsSuppressed = true;
                ToastNotificationService.Instance.ClearQueue();
                
                // 3. Dừng monitor trên luồng nền (tránh block UI ~2s). Login sau sẽ Restart().
                _ = System.Threading.Tasks.Task.Run(() => {
                    ConnectionMonitorService.Instance.Stop();
                    ConnectivityStateService.Instance.Stop();
                    QuanLyGiuXe.Services.Connection.AutoReconnectService.Instance.Stop();
                    QuanLyGiuXe.Services.Connection.CameraDiagnosticsService.Instance.Stop();
                    QuanLyGiuXe.Services.OfflineCache.AutoSyncService.Instance.Stop();
                    C3200Service.Instance.Disconnect();
                    RFIDService.Instance.Stop();
                });

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
                
                // Run SQL Server schema migrations and seed offline cache AFTER login (DB connection confirmed)
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await DatabaseService.EnsureMigrationsAppliedAsync();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("APP_STARTUP_MIGRATION", "Startup", "SQL Server migrations failed", ex);
                    }

                    try
                    {
                        await QuanLyGiuXe.Services.OfflineCache.OfflineCacheService.Instance.PreloadCacheAsync();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("APP_STARTUP_CACHE", "Startup", "Preloading offline cache failed", ex);
                    }
                });
                
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

                // Trigger non-blocking Phase 6.5 recovery and health checks
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // 1. Recover active sessions
                        await QuanLyGiuXe.Services.OfflineCache.SessionRecoveryService.Instance.RestoreActiveSessionsAsync();
                        // 2. Restore lane states
                        await QuanLyGiuXe.Services.OfflineCache.SessionRecoveryService.Instance.RestoreLaneStateAsync();
                        // 3. Restore pending sync items
                        await QuanLyGiuXe.Services.OfflineCache.SessionRecoveryService.Instance.RestorePendingQueueAsync();
                        // 4. Start session health monitor
                        QuanLyGiuXe.Services.OfflineCache.SessionHealthMonitor.Instance.Start();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("APP_STARTUP_RECOVERY", "Startup", "Startup recovery failed", ex);
                    }
                });
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
            try
            {
                LicenseMonitorService.Instance.Stop();
            }
            catch { }

            // Start a thread-pool timer to force exit the process if it hangs for more than 2 seconds
            var watchdogTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                catch { }
            }, null, 2000, System.Threading.Timeout.Infinite);

            try
            {
                LoggingService.Instance.LogInfo("AppExit", "App", $"Application exiting with code: {e.ApplicationExitCode}");
            }
            catch { }
            
            try
            {
                _apiServer.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping API server: {ex.Message}");
            }

            try
            {
                // Dừng toàn bộ các dịch vụ chạy nền để giải phóng luồng hoàn toàn
                ConnectionMonitorService.Instance.Stop();
                ConnectivityStateService.Instance.Stop();
                QuanLyGiuXe.Services.Connection.AutoReconnectService.Instance.Stop();
                QuanLyGiuXe.Services.Connection.CameraDiagnosticsService.Instance.Stop();
                QuanLyGiuXe.Services.OfflineCache.AutoSyncService.Instance.Stop();
                QuanLyGiuXe.Services.Backup.BackupScheduler.Instance.Stop();
                QuanLyGiuXe.Services.OfflineCache.SessionHealthMonitor.Instance.Stop();
                
                // Giải phóng các luồng kết nối phần cứng
                C3200Service.Instance.Disconnect();
                RFIDService.Instance.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping background services: {ex.Message}");
            }
            
            try
            {
                LoggingService.Instance.Shutdown();
            }
            catch { }
            
            try
            {
                base.OnExit(e);
            }
            catch { }
            
            try
            {
                watchdogTimer.Dispose();
            }
            catch { }
            
            try
            {
                Environment.Exit(e.ApplicationExitCode);
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                catch { }
            }
        }
    }

}
