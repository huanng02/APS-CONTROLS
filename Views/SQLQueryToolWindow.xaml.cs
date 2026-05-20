using System.Windows;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Interaction logic for SQLQueryToolWindow.xaml
    /// </summary>
    public partial class SQLQueryToolWindow : Window
    {
        public SQLQueryToolWindow()
        {
            InitializeComponent();
            this.DataContext = new SQLQueryToolViewModel();
        }
    }
}
