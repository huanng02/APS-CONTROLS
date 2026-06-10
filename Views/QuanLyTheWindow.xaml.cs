using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using QuanLyGiuXe.ViewModels;
using QuanLyGiuXe.Services;

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
                var vm = new RFIDCardWizardViewModel();
                vm.InitForAdd();
                var dlg = new RFIDCardAddEditWindow(null) { Owner = this };
                dlg.DataContext = vm;
                var result = dlg.ShowDialog();
                if (result == true)
                {
                    var toAdd = new Models.RFIDCards
                    {
                        CardUID = vm.CardUID,
                        CardName = vm.CardName,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId ?? 0,
                        LoaiVeId = vm.LoaiVeId ?? 0,
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai,
                        EmployeeId = vm.EmployeeId
                    };
                    new RFIDCardService().Add(toAdd);
                    MessageBox.Show("Thêm thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Refresh_Click(sender, e);
                }
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
                var button = sender as FrameworkElement;
                var target = button?.DataContext as Models.RFIDCards;
                if (target == null) return;

                var vm = new RFIDCardWizardViewModel();
                vm.LoadForEdit(target.Id);

                var window = new RFIDCardAddEditWindow(null) { Owner = this };
                window.DataContext = vm;
                var result = window.ShowDialog();
                if (result == true)
                {
                    var updated = new Models.RFIDCards
                    {
                        Id = vm.Id,
                        CardUID = target.CardUID,
                        CardName = vm.CardName,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId,
                        LoaiVeId = vm.LoaiVeId,
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai,
                        EmployeeId = vm.EmployeeId
                    };
                    new RFIDCardService().Update(updated);
                    MessageBox.Show("Sửa thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Refresh_Click(sender, e);
                }
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
                var button = sender as FrameworkElement;
                var target = button?.DataContext as Models.RFIDCards;
                if (target == null) return;

                var res = MessageBox.Show($"Bạn có chắc chắn muốn xóa thẻ UID: {target.CardUID}?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    new RFIDCardService().Delete(target.Id);
                    MessageBox.Show("Xóa thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Refresh_Click(sender, e);
                }
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
