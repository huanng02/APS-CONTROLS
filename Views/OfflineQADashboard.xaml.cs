using System;
using System.Windows;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class OfflineQADashboard : Window
    {
        public OfflineQADashboard()
        {
            InitializeComponent();
            DataContext = new OfflineQAViewModel();
        }
    }
}
