using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class    QuanLyTheWindow : Window
    {
        public QuanLyTheWindow()
        {
            InitializeComponent();
            DataContext = new QuanLyTheViewModel();
        }
        private void ThemThe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Mở form thêm thẻ (bạn sẽ build sau)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SuaThe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Sửa thẻ");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void XoaThe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Xóa thẻ");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is QuanLyTheViewModel vm)
                    vm.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is QuanLyTheViewModel vm)
                    vm.ResetFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
