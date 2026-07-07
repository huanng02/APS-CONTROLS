using System.Windows;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    public partial class TopologyValidationDialog : Window
    {
        public TopologyValidationDialog(TopologyValidationResult result)
        {
            InitializeComponent();
            DataContext = result;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
