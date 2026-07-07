using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LoaiXeViewModel : INotifyPropertyChanged
    {
        private readonly LoaiXeService service = new();

        public ObservableCollection<LoaiXe> DanhSach { get; set; } = new();
        public ICommand DeleteSelectedCommand { get; }
        public ICommand DeleteAllCommand { get; }

        private LoaiXe _selected;
        public LoaiXe Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged(nameof(Selected));

                if (_selected != null)
                {
                    TenLoai = _selected.TenLoai;
                    TrangThai = _selected.TrangThai;
                }
            }
        }

        private string _tenLoai;
        public string TenLoai
        {
            get => _tenLoai;
            set { _tenLoai = value; OnPropertyChanged(nameof(TenLoai)); }
        }

        private string _trangThai;
        public string TrangThai
        {
            get => _trangThai;
            set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); }
        }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }

        public LoaiXeViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(param => Update(param));
            DeleteCommand = new RelayCommand(param => Delete(param));
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            DeleteAllCommand = new RelayCommand(_ => DeleteAll());

            Load();
        }

        private void Load()
        {
            DanhSach.Clear();
            foreach (var item in service.GetAll())
                DanhSach.Add(item);
        }

        private void Add()
        {
            // Open modal Add dialog
            var model = new QuanLyGiuXe.Models.LoaiXe { TenLoai = string.Empty, TrangThai = "Active" };
            var dlg = new Views.GenericAddEditWindow(model) { Owner = System.Windows.Application.Current.MainWindow };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                try
                {
                    service.Add(model.TenLoai, model.TrangThai);
                    System.Windows.MessageBox.Show("Thêm loại xe thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Thêm thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                Load();
            }
        }

        private void Update(object? parameter)
        {
            LoaiXe target = null;
            if (parameter is LoaiXe lx) target = lx;
            else if (Selected != null) target = Selected;
            if (target == null) return;

            // clone to avoid mutating UI before confirm
            var model = new LoaiXe { Id = target.Id, TenLoai = target.TenLoai, TrangThai = target.TrangThai };
            var dlg = new Views.GenericAddEditWindow(model) { Owner = System.Windows.Application.Current.MainWindow };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                if (string.IsNullOrWhiteSpace(model.TenLoai))
                {
                    System.Windows.MessageBox.Show("Tên loại không được để trống", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    service.Update(model.Id, model.TenLoai, model.TrangThai);
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
            LoaiXe toDelete = null;
            if (parameter is LoaiXe lx) toDelete = lx;
            else if (Selected != null) toDelete = Selected;
            if (toDelete == null) return;

            if (System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa loại xe '{toDelete.TenLoai}'?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
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
            var selected = DanhSach.Where(x => x.IsSelected).ToList();
            if (!selected.Any()) return;

            // confirm
            if (System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa {selected.Count} mục đã chọn?","Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
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
            if (System.Windows.MessageBox.Show("Bạn có chắc muốn xóa tất cả loại xe?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
                return;

            bool ok = true;
            foreach (var it in DanhSach.ToList())
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
            TenLoai = "";
            TrangThai = "";
            Selected = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
