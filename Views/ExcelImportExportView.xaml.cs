using System;
using System.Windows.Controls;
using System.Windows;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class ExcelImportExportView : UserControl
    {
        public ExcelImportExportView()
        {
            try
            {
                InitializeComponent();

                // create ViewModel safely and assign DataContext
                try
                {
                    this.DataContext = new ExcelImportExportViewModel();
                }
                catch (Exception ex)
                {
                    // show friendly message inside the view instead of throwing to host window
                    var tb = new TextBlock { Text = "Failed to create ViewModel: " + ex.Message + "\n" + ex.StackTrace, TextWrapping = TextWrapping.Wrap };
                    this.Content = tb;
                }
            }
            catch (Exception ex)
            {
                // InitializeComponent failed — replace view with error text so host window is not blank
                var tb = new TextBlock
                {
                    Text = "Failed to initialize Import/Export UI: " + ex.Message + "\n" + ex.StackTrace,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12)
                };
                this.Content = tb;
            }
        }
    }
}
