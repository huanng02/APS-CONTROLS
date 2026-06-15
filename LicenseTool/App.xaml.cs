using System.Configuration;
using System.Data;
using System.Windows;

namespace LicenseTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler((s, ev) =>
        {
            if (s is Window window && window.Icon == null)
            {
                try
                {
                    window.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/LicenseTool;component/Assets/app.ico"));
                }
                catch { }
            }
        }));
        base.OnStartup(e);
    }
}

