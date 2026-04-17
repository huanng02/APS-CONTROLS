using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RFIDCardViewModel : INotifyPropertyChanged
    {
        private readonly RFIDCardService service = new RFIDCardService();

        public ObservableCollection<QuanLyGiuXe.Models.RFIDCards> Items { get; } = new ObservableCollection<QuanLyGiuXe.Models.RFIDCards>();

        private QuanLyGiuXe.Models.RFIDCards _selectedItem;
        public QuanLyGiuXe.Models.RFIDCards SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                if (_selectedItem != null)
                {
                    UID = _selectedItem.CardUID;
                    BienSo = _selectedItem.BienSo;
                    // LoaiVe/LoaiXe names are available in SelectedItem.LoaiVe / LoaiXe but we expose ids via Temp fields when editing
                    TrangThai = _selectedItem.TrangThai;
                }
            }
        }

        private string _uid;
        public string UID { get => _uid; set { _uid = value; OnPropertyChanged(nameof(UID)); } }

        private string _bienSo;
        public string BienSo { get => _bienSo; set { _bienSo = value; OnPropertyChanged(nameof(BienSo)); } }

        private int _loaiVeId;
        public int LoaiVeId { get => _loaiVeId; set { _loaiVeId = value; OnPropertyChanged(nameof(LoaiVeId)); } }

        private int _loaiXeId;
        public int LoaiXeId { get => _loaiXeId; set { _loaiXeId = value; OnPropertyChanged(nameof(LoaiXeId)); } }

        private string _trangThai;
        public string TrangThai { get => _trangThai; set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); } }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand DeleteAllCommand { get; }
        public ICommand DownloadTemplateCommand { get; }
        public ICommand ExportCommand { get; }

        public RFIDCardViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(param => Update(param));
            DeleteCommand = new RelayCommand(param => Delete(param));
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            DeleteAllCommand = new RelayCommand(_ => DeleteAll());
            ClearCommand = new RelayCommand(_ => Clear());
            DownloadTemplateCommand = new RelayCommand(async _ => await DownloadTemplate());
            ExportCommand = new RelayCommand(_ => Export());
            Load();
        }


        private void Export()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.Filter = "Excel Workbook|*.xlsx";
                dlg.FileName = "RFIDCards_Export.xlsx";
                if (dlg.ShowDialog() == true)
                {
                    var svc = new ImportExportService();
                    svc.ExportToExcel(dlg.FileName);
                    System.Windows.MessageBox.Show("Export hoàn tất", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task DownloadTemplate()
        {
            try
            {
                var svc = new TemplateExportService();
                var path = await svc.CreateTemplateOnDesktopAsync();
                if (!string.IsNullOrEmpty(path))
                {
                    System.Windows.MessageBox.Show($"Template folder created:\n{path}", "Done", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show($"Failed to create template folder.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Load()
        {
            Items.Clear();
            var list = service.GetAll();
            foreach (var it in list) Items.Add(it);
        }

        private void Add()
        {
            // open modal add dialog using a wizard ViewModel and map back on save
            var model = new QuanLyGiuXe.Models.RFIDCards { CardUID = string.Empty, BienSo = string.Empty, TrangThai = "Active" };

            var vm = new RFIDCardWizardViewModel();
            vm.InitForAdd();
            // seed initial values from model (if any)
            vm.CardUID = model.CardUID;
            vm.BienSo = model.BienSo;
            vm.TrangThai = model.TrangThai;

            var dlg = new Views.RFIDCardAddEditWindow(null) { Owner = System.Windows.Application.Current.MainWindow };
            dlg.DataContext = vm;
            var result = dlg.ShowDialog();
            if (result == true)
            {
                try
                {
                    // map back from vm to model
                    var toAdd = new QuanLyGiuXe.Models.RFIDCards
                    {
                        CardUID = vm.CardUID,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId ?? 0,
                        LoaiVeId = vm.LoaiVeId ?? 0,
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai
                    };

                    service.Add(toAdd);
                    System.Windows.MessageBox.Show("Thêm thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Thêm thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                Load();
            }
        }

        private void Update(object parameter)
        {
            QuanLyGiuXe.Models.RFIDCards target = null;
            if (parameter is QuanLyGiuXe.Models.RFIDCards rc) target = rc;
            else if (SelectedItem != null) target = SelectedItem;
            if (target == null) return;
            // Use wizard ViewModel for edit flow: load data BEFORE showing window
            var vm = new RFIDCardWizardViewModel();
            vm.LoadForEdit(target.Id);

            var window = new Views.RFIDCardAddEditWindow(null) { Owner = System.Windows.Application.Current.MainWindow };
            window.DataContext = vm;
            var result = window.ShowDialog();
            if (result == true)
            {
                try
                {
                    // map back from vm to model and update
                    var updated = new QuanLyGiuXe.Models.RFIDCards
                    {
                        Id = vm.Id,
                        // preserve original CardUID (identity) — do not allow edits to UID
                        CardUID = target.CardUID,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId,
                        LoaiVeId = vm.LoaiVeId,
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai
                    };
                    service.Update(updated);
                    System.Windows.MessageBox.Show("Sửa thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cập nhật thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                Load();
            }
        }

        private void Delete(object parameter)
        {
            QuanLyGiuXe.Models.RFIDCards toDelete = null;
            if (parameter is QuanLyGiuXe.Models.RFIDCards rc) toDelete = rc;
            else if (SelectedItem != null) toDelete = SelectedItem;
            if (toDelete == null) return;

            if (System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa thẻ '{toDelete.CardUID}'?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
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

        private void Clear()
        {
            SelectedItem = null;
            UID = string.Empty;
            BienSo = string.Empty;
            LoaiVeId = 0;
            LoaiXeId = 0;
            TrangThai = string.Empty;
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
            if (System.Windows.MessageBox.Show("Bạn có chắc muốn xóa tất cả thẻ?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
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

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
