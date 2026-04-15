using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LoaiVeViewModel : INotifyPropertyChanged
    {
        private readonly LoaiVeService service = new LoaiVeService();

        public ObservableCollection<LoaiVe> Items { get; } = new ObservableCollection<LoaiVe>();
        public ICommand DeleteSelectedCommand { get; }
        public ICommand DeleteAllCommand { get; }

        private LoaiVe _selectedItem;
        public LoaiVe SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                if (_selectedItem != null)
                {
                    TenLoai = _selectedItem.TenLoai; // Ensuring consistency
                    GiaTien = _selectedItem.GiaTien;
                    TrangThai = _selectedItem.TrangThai;
                }
            }
        }

        private string _tenLoai;
        public string TenLoai { get => _tenLoai; set { _tenLoai = value; OnPropertyChanged(nameof(TenLoai)); } }

        private decimal _giaTien;
        public decimal GiaTien { get => _giaTien; set { _giaTien = value; OnPropertyChanged(nameof(GiaTien)); } }

        private string _trangThai;
        public string TrangThai { get => _trangThai; set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); } }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }

        public LoaiVeViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(param => Update(param));
            DeleteCommand = new RelayCommand(param => Delete(param));
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            DeleteAllCommand = new RelayCommand(_ => DeleteAll());
            ClearCommand = new RelayCommand(_ => Clear());
            Load();
        }

        private void Load()
        {
            Items.Clear();
            var list = service.GetAll();
            foreach (var it in list) Items.Add(it);
        }

        private void Add()
        {
            // Open modal Add dialog
            var model = new LoaiVe { TenLoai = string.Empty, GiaTien = 0m, TrangThai = "Active" };
            var dlg = new Views.GenericAddEditWindow(model) { Owner = System.Windows.Application.Current.MainWindow };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                // validate business rules
                if (string.IsNullOrWhiteSpace(model.TenLoai))
                {
                    System.Windows.MessageBox.Show("Tên loại không được để trống", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (model.GiaTien <= 0)
                {
                    System.Windows.MessageBox.Show("Giá tiền phải lớn hơn 0", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(model.TrangThai))
                {
                    System.Windows.MessageBox.Show("Vui lòng chọn trạng thái", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    service.Add(model.TenLoai, model.GiaTien, model.TrangThai);
                    System.Windows.MessageBox.Show("Thêm loại vé thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Thêm thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                Load();
                Clear();
            }
        }

        private void Update(object? parameter)
        {
            LoaiVe target = null;
            if (parameter is LoaiVe lv) target = lv;
            else if (SelectedItem != null) target = SelectedItem;
            if (target == null) return;

            var model = new LoaiVe { Id = target.Id, TenLoai = target.TenLoai, GiaTien = target.GiaTien, TrangThai = target.TrangThai };
            var dlg = new Views.GenericAddEditWindow(model) { Owner = System.Windows.Application.Current.MainWindow };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                if (string.IsNullOrWhiteSpace(model.TenLoai))
                {
                    System.Windows.MessageBox.Show("Tên loại không được để trống", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (model.GiaTien <= 0)
                {
                    System.Windows.MessageBox.Show("Giá tiền phải lớn hơn 0", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(model.TrangThai))
                {
                    System.Windows.MessageBox.Show("Vui lòng chọn trạng thái", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    service.Update(model.Id, model.TenLoai, model.GiaTien, model.TrangThai);
                    System.Windows.MessageBox.Show("Cập nhật thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Cập nhật thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }

                Load();
            }
        }

        private void Delete(object? parameter)
        {
            LoaiVe toDelete = null;
            if (parameter is LoaiVe lv) toDelete = lv;
            else if (SelectedItem != null) toDelete = SelectedItem;
            if (toDelete == null) return;

            if (System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa loại vé '{toDelete.TenLoai}'?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                service.Delete(toDelete.Id);
                System.Windows.MessageBox.Show("Xoá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("Xoá thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            Load();
            Clear();
        }

        private void DeleteSelected()
        {
            var selected = Items.Where(x => x.IsSelected).ToList();
            if (!selected.Any()) return;
            if (System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa {selected.Count} mục đã chọn?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
                return;

            bool ok = true;
            foreach (var it in selected)
            {
                try { service.Delete(it.Id); }
                catch { ok = false; }
            }

            if (ok) System.Windows.MessageBox.Show("Xoá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            else System.Windows.MessageBox.Show("Xoá thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

            Load();
        }

        private void DeleteAll()
        {
            if (System.Windows.MessageBox.Show("Bạn có chắc muốn xóa tất cả loại vé?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
                return;

            bool ok = true;
            foreach (var it in Items.ToList())
            {
                try { service.Delete(it.Id); }
                catch { ok = false; }
            }

            if (ok) System.Windows.MessageBox.Show("Xoá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            else System.Windows.MessageBox.Show("Xoá thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

            Load();
        }

        private void Clear()
        {
            SelectedItem = null;
            TenLoai = string.Empty;
            GiaTien = 0m;
            TrangThai = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
