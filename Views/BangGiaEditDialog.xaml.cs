using System;
using System.Globalization;
using System.Windows;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class BangGiaEditDialog : Window
    {
        private BangGia _model;

        public BangGiaEditDialog(BangGia model)
        {
            InitializeComponent();
            _model = new BangGia
            {
                Id = model.Id,
                LoaiXeId = model.LoaiXeId,
                GiaBanNgay = model.GiaBanNgay,
                GiaQuaDem = model.GiaQuaDem,
                TrangThai = model.TrangThai,
                LoaiXe = model.LoaiXe
            };

            tbLoaiXe.Text = _model.LoaiXe;
            tbGiaBanNgay.Text = _model.GiaBanNgay?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            tbGiaQuaDem.Text = _model.GiaQuaDem?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(tbGiaBanNgay.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var g1) || g1 <= 0)
            {
                MessageBox.Show("Giá ban ngày phải lớn hơn 0", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!decimal.TryParse(tbGiaQuaDem.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var g2) || g2 < 0)
            {
                MessageBox.Show("Giá qua đêm không hợp lệ", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _model.GiaBanNgay = g1;
            _model.GiaQuaDem = g2;

            try
            {
                var svc = new BangGiaAdminService();
                svc.UpdateBangGia(_model);
                MessageBox.Show("Cập nhật bảng giá thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch
            {
                MessageBox.Show("Cập nhật thất bại, vui lòng kiểm tra dữ liệu", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
