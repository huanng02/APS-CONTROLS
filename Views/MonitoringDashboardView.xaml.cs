using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Interaction logic for MonitoringDashboardView.xaml
    /// </summary>
    public partial class MonitoringDashboardView : UserControl
    {
        public MonitoringDashboardView()
        {
            InitializeComponent();
            DataContext = new MonitoringDashboardViewModel();
        }
    }

    public class StringArrayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null) return new string[0];
            var arr = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                arr[i] = values[i]?.ToString() ?? string.Empty;
            }
            return arr;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
