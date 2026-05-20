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
            LoadCommand = new RelayCommand(async _ => await LoadAsync());
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(_ => Update(), _ => Selected != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => Selected != null);

            // defer initial load slightly to avoid UI freeze at startup
            _ = LoadAsync();
        }

        // Load data asynchronously to avoid blocking the UI thread
        private bool _isLoading = false;

        public async System.Threading.Tasks.Task LoadAsync()
        {
            if (_isLoading)
                return;

            _isLoading = true;

            try
            {
                var list = await System.Threading.Tasks.Task.Run(() => _repo.GetAll());

                // build temp list trước
                var temp = list.Select(k => new KhungGioItemVM
                {
                    Id = k.Id,
                    TenKhungGio = k.TenKhungGio,
                    GioBatDau = k.GioBatDau,
                    GioKetThuc = k.GioKetThuc,
                    TrangThai = k.TrangThai
                }).ToList();

                // update UI trên UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Items.Clear();

                    foreach (var item in temp)
                    {
                        Items.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load KhungGio failed: {ex}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async void Add()
        {
            var dialog = new QuanLyGiuXe.Views.KhungGioAddDialog();

            // Lấy danh sách các khung giờ hiện có để truyền vào Dialog
            var defs = _repo.GetAll();

            dialog.ExistingSlots = defs
                .Select(x => (x.TenKhungGio, x.GioBatDau, x.GioKetThuc))
                .ToList();

            // Hàm callback truyền vào Dialog để kiểm tra trùng lặp
            dialog.CheckOverlapFunc = (start, end) =>
            {
                var candidate = new KhungGioItemVM
                {
                    GioBatDau = start,
                    GioKetThuc = end
                };

                return CheckOverlap(candidate);
            };

            // Mở dialog
            bool? result = dialog.ShowDialog();

            // Nếu user bấm Lưu
            if (result == true)
            {
                try
                {
                    // =========================
                    // VALIDATE INPUT
                    // =========================

                    var slotName = dialog.TenKhungGio?.Trim();

                    if (string.IsNullOrWhiteSpace(slotName))
                    {
                        MessageBox.Show(
                            "Vui lòng nhập tên khung giờ.",
                            "Thiếu thông tin",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    // Không cho start == end
                    if (dialog.GioBatDau == dialog.GioKetThuc)
                    {
                        MessageBox.Show(
                            "Giờ bắt đầu không được trùng giờ kết thúc.",
                            "Dữ liệu không hợp lệ",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    // Kiểm tra tên trùng
                    bool duplicatedName = defs.Any(x =>
                        !string.IsNullOrWhiteSpace(x.TenKhungGio) &&
                        x.TenKhungGio.Trim()
                            .Equals(slotName, StringComparison.OrdinalIgnoreCase));

                    if (duplicatedName)
                    {
                        MessageBox.Show(
                            "Tên khung giờ đã tồn tại.",
                            "Trùng dữ liệu",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    // Kiểm tra overlap
                    var candidate = new KhungGioItemVM
                    {
                        GioBatDau = dialog.GioBatDau,
                        GioKetThuc = dialog.GioKetThuc
                    };

                    if (CheckOverlap(candidate))
                    {
                        MessageBox.Show(
                            "Khung giờ bị chồng chéo với khung giờ khác.",
                            "Xung đột thời gian",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }

                    // =========================
                    // CREATE ENTITY
                    // =========================

                    bool isQuaDem = dialog.GioKetThuc < dialog.GioBatDau;

                    var entity = new KhungGio
                    {
                        TenKhungGio = slotName,
                        GioBatDau = dialog.GioBatDau,
                        GioKetThuc = dialog.GioKetThuc,
                        QuaDem = isQuaDem,
                        TrangThai = true
                    };

                    // =========================
                    // SAVE
                    // =========================

                    _repo.Insert(entity);

                    // Reload UI
                    await LoadAsync();

                    StatusMessage = "Thêm khung giờ thành công.";

                    MessageBox.Show(
                        "Thêm khung giờ thành công.",
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Thêm khung giờ thất bại: " + ex.Message,
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private async void Update()
        {
            if (Selected == null) return;
            try
            {
                if (Selected.GioBatDau == Selected.GioKetThuc) throw new ArgumentException("GioBatDau cannot equal GioKetThuc");
                if (CheckOverlap(Selected, Selected.Id)) throw new ArgumentException("Time range overlaps existing slot");
                var entity = new KhungGio { Id = Selected.Id, TenKhungGio = Selected.TenKhungGio, GioBatDau = Selected.GioBatDau, GioKetThuc = Selected.GioKetThuc, QuaDem = Selected.QuaDem, TrangThai = Selected.TrangThai };
                _repo.Update(entity);
                await LoadAsync();
                StatusMessage = "Cập nhật khung giờ thành công.";
                try { MessageBox.Show("Cập nhật khung giờ thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Cập nhật thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Delete()
        {
            if (Selected == null) return;
            if (MessageBox.Show($"Bạn có chắc muốn xóa khung '{Selected.TenKhungGio}'?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                _repo.Delete(Selected.Id);
                await LoadAsync();
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
