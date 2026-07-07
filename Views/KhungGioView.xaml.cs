using System;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class KhungGioView : UserControl
    {
        public KhungGioView()
        {
            InitializeComponent();

            Loaded += KhungGioView_Loaded;
        }

        private void KhungGioView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (DataContext == null)
                {
                    DataContext = new KhungGioManagementViewModel();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Set DataContext failed: " + ex);
            }
        }
    }
}