using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class BangGiaAdminViewModel : INotifyPropertyChanged
    {
        private readonly BangGiaAdminService _service = new BangGiaAdminService();
        // exposed collection for binding
        public ObservableCollection<BangGia> BangGiaList { get; } = new ObservableCollection<BangGia>();

        private BangGia _selected;
        public BangGia Selected { get => _selected; set { _selected = value; OnPropertyChanged(nameof(Selected)); } }

        // dirty tracking
        private readonly System.Collections.Generic.HashSet<int> _modifiedIds = new System.Collections.Generic.HashSet<int>();
        private readonly System.Collections.Generic.Dictionary<int, (decimal? giaTheoGio, decimal? giaQuaDem)> _originalValues = new System.Collections.Generic.Dictionary<int, (decimal?, decimal?)>();

        private bool _isDirty;
        public bool IsDirty { get => _isDirty; private set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } } }

        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ReloadCommand { get; }
        public ICommand OpenEditCommand { get; }

        public BangGiaAdminViewModel()
        {
            LoadCommand = new ViewModels.RelayCommand(_ => Load());
            ReloadCommand = LoadCommand;
            SaveCommand = new ViewModels.RelayCommand(param => Save(param), param => IsDirty);
            OpenEditCommand = new ViewModels.RelayCommand(param => OpenEdit(param));
            LoadData();
        }

        // Public entry to load data (explicit name requested)
        public void LoadData()
        {
            Load();
        }

        public void Load()
        {
            BangGiaList.Clear();
            _modifiedIds.Clear();
            _originalValues.Clear();
            var list = _service.GetAll();
            // map LoaiXe name for display
            var loaiXeList = new DatabaseService().GetLoaiXe();
            foreach (var b in list)
            {
                b.LoaiXe = (b.LoaiXeId > 0) ? loaiXeList.FirstOrDefault(l => l != null && l.Id == b.LoaiXeId)?.TenLoai ?? string.Empty : string.Empty;
                BangGiaList.Add(b);
                // store original values for dirty-check / revert
                _originalValues[b.Id] = (b.GiaTheoGio, b.GiaQuaDem);
            }
            IsDirty = false;
        }

        private void OpenEdit(object param)
        {
            if (!(param is BangGia bg)) return;

            // open dialog with a cloned model
            var dlg = new QuanLyGiuXe.Views.BangGiaEditDialog(bg);
            var result = dlg.ShowDialog();
            if (result == true)
            {
                // refresh list
                Load();
            }
        }

        private void Save(object param)
        {
            // Batch save all modified rows if param is null; if param is a BangGia then save that row only
            var toSave = new System.Collections.Generic.List<BangGia>();
            if (param is BangGia single)
            {
                toSave.Add(single);
            }
            else
            {
                foreach (var id in _modifiedIds) {
                    var item = BangGiaList.FirstOrDefault(x => x.Id == id);
                    if (item != null) toSave.Add(item);
                }
            }

            if (!toSave.Any()) return;

            // validate all
            foreach (var model in toSave)
            {
                if (!model.GiaTheoGio.HasValue || model.GiaTheoGio.Value <= 0)
                {
                    System.Windows.MessageBox.Show("Giá theo giờ phải lớn hơn 0", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (!model.GiaQuaDem.HasValue || model.GiaQuaDem.Value < 0)
                {
                    System.Windows.MessageBox.Show("Giá qua đêm không hợp lệ", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }

            int failed = 0;
            foreach (var model in toSave)
            {
                try
                {
                    _service.UpdateBangGia(model);
                    // update original values snapshot
                    _originalValues[model.Id] = (model.GiaTheoGio, model.GiaQuaDem);
                    _modifiedIds.Remove(model.Id);
                }
                catch
                {
                    failed++;
                }
            }

            if (failed == 0)
            {
                System.Windows.MessageBox.Show("Cập nhật bảng giá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                IsDirty = _modifiedIds.Count > 0;
            }
            else
            {
                System.Windows.MessageBox.Show("Cập nhật thất bại, vui lòng thử lại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                IsDirty = true;
            }

            // reload to reflect any DB-side normalization
            Load();
        }

        // Called from view when a row is edited
        public void MarkRowEdited(BangGia model)
        {
            if (model == null) return;
            if (!_originalValues.TryGetValue(model.Id, out var orig))
            {
                _originalValues[model.Id] = (model.GiaTheoGio, model.GiaQuaDem);
            }
            // if values equal original, remove from modified set
            var now = (model.GiaTheoGio, model.GiaQuaDem);
            if (_originalValues.TryGetValue(model.Id, out var old) && old.Equals(now))
            {
                _modifiedIds.Remove(model.Id);
            }
            else
            {
                _modifiedIds.Add(model.Id);
            }

            IsDirty = _modifiedIds.Count > 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
