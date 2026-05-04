using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class KhungGioManagementViewModel : INotifyPropertyChanged
    {
        private readonly KhungGioRepository _repo = new KhungGioRepository();

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); } }

        public ObservableCollection<KhungGioItemVM> Items { get; } = new ObservableCollection<KhungGioItemVM>();

        private KhungGioItemVM _selected;
        public KhungGioItemVM Selected { get => _selected; set { _selected = value; OnPropertyChanged(nameof(Selected)); } }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }

        public KhungGioManagementViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(_ => Update(), _ => Selected != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => Selected != null);

            // defer initial load slightly to avoid UI freeze at startup
            System.Threading.Tasks.Task.Run(() => Load());
        }

        // Load data asynchronously to avoid blocking the UI thread
        public async void Load()
        {
            try
            {
                Items.Clear();
                var list = await System.Threading.Tasks.Task.Run(() => _repo.GetAll());
                foreach(var k in list)
                {
                    Items.Add(new KhungGioItemVM
                    {
                        Id = k.Id,
                        TenKhungGio = k.TenKhungGio,
                        GioBatDau = k.GioBatDau,
                        GioKetThuc = k.GioKetThuc,
                        TrangThai = k.TrangThai
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load KhungGio failed: {ex}");
            }
        }

        private void Add()
        {
            var dialog = new QuanLyGiuXe.Views.KhungGioAddDialog();
            
            // Lấy danh sách các khung giờ hiện có để truyền vào Dialog
            var defs = _repo.GetAll();
            dialog.ExistingSlots = defs.Select(x => (x.TenKhungGio, x.GioBatDau, x.GioKetThuc)).ToList();

            // Hàm callback truyền vào Dialog để kiểm tra trùng lặp
            dialog.CheckOverlapFunc = (start, end) =>
            {
                var candidate = new KhungGioItemVM { GioBatDau = start, GioKetThuc = end };
                return CheckOverlap(candidate);
            };

            // Mở Dialog (chặn tương tác với UI chính cho đến khi đóng)
            bool? result = dialog.ShowDialog();

            // Nếu người dùng nhấn Lưu và hợp lệ (DialogResult = true)
            if (result == true)
            {
                try
                {
                    // QuaDem (nếu end < start thì là qua đêm)
                    bool isQuaDem = dialog.GioKetThuc < dialog.GioBatDau;

                    var entity = new KhungGio 
                    { 
                        TenKhungGio = dialog.TenKhungGio, 
                        GioBatDau = dialog.GioBatDau, 
                        GioKetThuc = dialog.GioKetThuc, 
                        QuaDem = isQuaDem, 
                        TrangThai = true 
                    };
                    
                    _repo.Insert(entity);
                    Load();
                    StatusMessage = "Thêm khung giờ thành công.";
                    try { MessageBox.Show("Thêm khung giờ thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Thêm khung giờ thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Update()
        {
            if (Selected == null) return;
            try
            {
                if (Selected.GioBatDau == Selected.GioKetThuc) throw new ArgumentException("GioBatDau cannot equal GioKetThuc");
                if (CheckOverlap(Selected, Selected.Id)) throw new ArgumentException("Time range overlaps existing slot");
                var entity = new KhungGio { Id = Selected.Id, TenKhungGio = Selected.TenKhungGio, GioBatDau = Selected.GioBatDau, GioKetThuc = Selected.GioKetThuc, QuaDem = Selected.QuaDem, TrangThai = Selected.TrangThai };
                _repo.Update(entity);
                Load();
                StatusMessage = "Cập nhật khung giờ thành công.";
                try { MessageBox.Show("Cập nhật khung giờ thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Cập nhật thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete()
        {
            if (Selected == null) return;
            if (MessageBox.Show($"Bạn có chắc muốn xóa khung '{Selected.TenKhungGio}'?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _repo.Delete(Selected.Id);
                Load();
                StatusMessage = "Xóa khung giờ thành công.";
                try { MessageBox.Show("Xóa khung giờ thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Xóa thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckOverlap(KhungGioItemVM candidate, int excludeId = 0)
        {
            var defs = _repo.GetAll();
            foreach(var d in defs)
            {
                if (d.Id == excludeId) continue;
                if (IntervalsOverlap(candidate.GioBatDau, candidate.GioKetThuc, d.GioBatDau, d.GioKetThuc)) return true;
            }
            return false;
        }

        private bool IntervalsOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
        {
            // normalize overnight
            var a = Normalize(aStart, aEnd);
            var b = Normalize(bStart, bEnd);
            foreach(var ai in a)
            {
                foreach(var bi in b)
                {
                    if (ai.start < bi.end && bi.start < ai.end) return true;
                }
            }
            return false;
        }

        private System.Collections.Generic.List<(TimeSpan start, TimeSpan end)> Normalize(TimeSpan s, TimeSpan e)
        {
            var res = new System.Collections.Generic.List<(TimeSpan, TimeSpan)>();
            if (e > s) res.Add((s,e));
            else { res.Add((s, TimeSpan.FromHours(24))); res.Add((TimeSpan.Zero, e)); }
            return res;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
