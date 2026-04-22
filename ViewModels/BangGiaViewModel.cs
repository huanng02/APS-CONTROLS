using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class BangGiaViewModel : INotifyPropertyChanged
    {
        private readonly BangGiaService service = new BangGiaService();

        public ObservableCollection<BangGia> Items { get; } = new ObservableCollection<BangGia>();

        private BangGia _selected;
        public BangGia Selected { get => _selected; set { _selected = value; OnPropertyChanged(nameof(Selected)); } }

        public ICommand LoadCommand { get; }
        public ICommand EditCommand { get; }

        public BangGiaViewModel()
        {
        LoadCommand = new RelayCommand(_ => Load());
            EditCommand = new RelayCommand(param => Edit(param));
            Load();
        }

        public void Load()
        {
            Items.Clear();
            foreach (var it in service.GetAll()) Items.Add(it);
        }

        private void Edit(object param)
        {
            BangGia target = null;
            if (param is BangGia bg) target = bg;
            else if (Selected != null) target = Selected;
            if (target == null) return;

            var model = new BangGia { Id = target.Id, LoaiXeId = target.LoaiXeId, GiaBanNgay = target.GiaBanNgay, GiaQuaDem = target.GiaQuaDem, TrangThai = target.TrangThai };
            var dlg = new Views.GenericAddEditWindow(model) { Owner = System.Windows.Application.Current.MainWindow };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                // validate
                if (!model.GiaBanNgay.HasValue || model.GiaBanNgay.Value <= 0)
                {
                    System.Windows.MessageBox.Show("Giá ban ngày phải > 0", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (!model.GiaQuaDem.HasValue || model.GiaQuaDem.Value < 0)
                {
                    System.Windows.MessageBox.Show("GiaQuaDem phải >= 0", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    service.UpdateGia(model.Id, model.GiaBanNgay.Value, model.GiaQuaDem.Value);
                    System.Windows.MessageBox.Show("Cập nhật giá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cập nhật thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                Load();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
