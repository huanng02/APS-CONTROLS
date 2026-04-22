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
    public class BangGiaManagementViewModel : INotifyPropertyChanged
    {
        private readonly BangGiaRepository _repo = new BangGiaRepository();
        private readonly DatabaseService _db = new DatabaseService();
        // Named time constants to avoid magic numbers
        private static readonly TimeSpan BusinessDayStart = TimeSpan.FromHours(6); // 06:00
        private static readonly TimeSpan BusinessNightStart = TimeSpan.FromHours(20); // 20:00
        private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30); // 30 minutes

        public ObservableCollection<BangGia> Items { get; } = new ObservableCollection<BangGia>();
        public ObservableCollection<LoaiXe> LoaiXeList { get; } = new ObservableCollection<LoaiXe>();
        public ObservableCollection<LoaiVe> LoaiVeList { get; } = new ObservableCollection<LoaiVe>();

        private BangGia _selectedItem;
        public BangGia SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                LoadToFormFromSelected();
            }
        }


        // Compute duration and cost helper (uses GiaTheoGio when available)
        private void ComputeDuration()
        {
            // For demo: prompt user to enter start/end times via InputBox style prompts are not available in WPF by default
            // We'll just compute duration if EditingItem has temporary fields StartTimeText and EndTimeText
            // For now, show a MessageBox with guidance.
            MessageBox.Show("Tính thời lượng: chọn thời gian vào/ra trong form (tính năng demo).", "Tính thời lượng", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Compute()
        {
            // Validate times
            if (ThoiGianVao == null || ThoiGianRa == null)
            {
                ResultText = string.Empty;
                return;
            }

            if (ThoiGianRa < ThoiGianVao)
            {
                ResultText = "Thời gian không hợp lệ";
                return;
            }

            var start = ThoiGianVao.Value;
            var end = ThoiGianRa.Value;

            // Determine selected price record by pair (LoaiXeId, LoaiVeId)
            var loaiXeId = EditingItem?.LoaiXeId ?? 0;
            var loaiVeId = SelectedLoaiVeId;

            if (loaiXeId <= 0 || loaiVeId <= 0)
            {
                ResultText = "Chưa cấu hình bảng giá";
                OnPropertyChanged(nameof(ResultText));
                return;
            }

            var bangGia = _repo.GetByLoaiXeAndLoaiVe(loaiXeId, loaiVeId);
            if (bangGia == null)
            {
                ResultText = "Chưa cấu hình bảng giá";
                OnPropertyChanged(nameof(ResultText));
                return;
            }

            // Monthly ticket handling
            var isThang = _thangLoaiVeId.HasValue && loaiVeId == _thangLoaiVeId.Value;
            if (isThang)
            {
                if (!bangGia.GiaThang.HasValue)
                {
                    ResultText = "Chưa cấu hình bảng giá";
                    OnPropertyChanged(nameof(ResultText));
                    return;
                }

                ResultText = $"Tiền: {FormatVND(bangGia.GiaThang.Value)}";
                OnPropertyChanged(nameof(ResultText));
                return;
            }

            // Vãng lai (or other non-monthly) must have hourly and overnight prices
            if (!bangGia.GiaTheoGio.HasValue || !bangGia.GiaQuaDem.HasValue)
            {
                ResultText = "Chưa cấu hình bảng giá";
                OnPropertyChanged(nameof(ResultText));
                return;
            }

            var dayPrice = bangGia.GiaTheoGio.Value;
            var nightPrice = bangGia.GiaQuaDem.Value;

            bool IsCrossingBoundary(DateTime s, DateTime e)
            {
                var from = s.Date.AddDays(-1);
                var to = e.Date.AddDays(1);
                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    var b1 = d.Date + BusinessDayStart;
                    var b2 = d.Date + BusinessNightStart;
                    if (s < b1 && b1 <= e) return true;
                    if (s < b2 && b2 <= e) return true;
                }
                return false;
            }

            bool IsGracePeriod(DateTime s, DateTime e) => (e - s) <= GracePeriod;

            // If session crosses a boundary within grace period, charge based on start zone
            if (IsCrossingBoundary(start, end) && IsGracePeriod(start, end))
            {
                var t = start.TimeOfDay;
                bool startIsNight = (t >= BusinessNightStart) || (t < BusinessDayStart);
                var total = startIsNight ? nightPrice : dayPrice;
                ResultText = $"Tiền: {FormatVND(total)}";
                OnPropertyChanged(nameof(ResultText));
                return;
            }

            // Otherwise split by business-day and apply per-day evaluation
            decimal grandTotal = 0m;

            DateTime currentDayStart = start.Date + BusinessDayStart;
            if (start.TimeOfDay < BusinessDayStart) currentDayStart = start.Date.AddDays(-1) + BusinessDayStart;

            while (currentDayStart < end)
            {
                DateTime businessDayEnd = currentDayStart.AddDays(1); // next day at BusinessDayStart

                var segStart = start > currentDayStart ? start : currentDayStart;
                var segEnd = end < businessDayEnd ? end : businessDayEnd;
                if (segEnd <= segStart)
                {
                    currentDayStart = businessDayEnd;
                    continue;
                }

                var nightStart = currentDayStart.Date + BusinessNightStart;
                var nightEnd = currentDayStart.Date.AddDays(1) + BusinessDayStart;

                var overlapNightStart = segStart > nightStart ? segStart : nightStart;
                var overlapNightEnd = segEnd < nightEnd ? segEnd : nightEnd;

                bool hasNight = overlapNightEnd > overlapNightStart;
                grandTotal += hasNight ? nightPrice : dayPrice;

                currentDayStart = businessDayEnd;
            }

            ResultText = $"Tiền: {FormatVND(grandTotal)}";
            OnPropertyChanged(nameof(ResultText));
        }

        private string FormatVND(decimal amount)
        {
            return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} đ", amount);
        }

        // Times for duration calculation
        private DateTime? _thoiGianVao;
        public DateTime? ThoiGianVao
        {
            get => _thoiGianVao;
            set { _thoiGianVao = value; OnPropertyChanged(nameof(ThoiGianVao)); Compute(); }
        }

        private DateTime? _thoiGianRa;
        public DateTime? ThoiGianRa
        {
            get => _thoiGianRa;
            set { _thoiGianRa = value; OnPropertyChanged(nameof(ThoiGianRa)); Compute(); }
        }

        // Date + Time pickers backing for non-editable time selection
        public ObservableCollection<int> Hours { get; } = new ObservableCollection<int>();
        public ObservableCollection<int> Minutes { get; } = new ObservableCollection<int>();

        private DateTime? _thoiGianVaoDate;
        public DateTime? ThoiGianVaoDate
        {
            get => _thoiGianVaoDate;
            set { _thoiGianVaoDate = value; OnPropertyChanged(nameof(ThoiGianVaoDate)); UpdateThoiGianVao(); }
        }

        private int _thoiGianVaoHour;
        public int ThoiGianVaoHour { get => _thoiGianVaoHour; set { _thoiGianVaoHour = value; OnPropertyChanged(nameof(ThoiGianVaoHour)); UpdateThoiGianVao(); } }
        private int _thoiGianVaoMinute;
        public int ThoiGianVaoMinute { get => _thoiGianVaoMinute; set { _thoiGianVaoMinute = value; OnPropertyChanged(nameof(ThoiGianVaoMinute)); UpdateThoiGianVao(); } }

        private DateTime? _thoiGianRaDate;
        public DateTime? ThoiGianRaDate
        {
            get => _thoiGianRaDate;
            set { _thoiGianRaDate = value; OnPropertyChanged(nameof(ThoiGianRaDate)); UpdateThoiGianRa(); }
        }

        private int _thoiGianRaHour;
        public int ThoiGianRaHour { get => _thoiGianRaHour; set { _thoiGianRaHour = value; OnPropertyChanged(nameof(ThoiGianRaHour)); UpdateThoiGianRa(); } }
        private int _thoiGianRaMinute;
        public int ThoiGianRaMinute { get => _thoiGianRaMinute; set { _thoiGianRaMinute = value; OnPropertyChanged(nameof(ThoiGianRaMinute)); UpdateThoiGianRa(); } }

        private void UpdateThoiGianVao()
        {
            if (ThoiGianVaoDate.HasValue)
            {
                var dt = ThoiGianVaoDate.Value.Date + new TimeSpan(ThoiGianVaoHour, ThoiGianVaoMinute, 0);
                ThoiGianVao = dt;
            }
            else
            {
                ThoiGianVao = null;
            }
        }

        private void UpdateThoiGianRa()
        {
            if (ThoiGianRaDate.HasValue)
            {
                var dt = ThoiGianRaDate.Value.Date + new TimeSpan(ThoiGianRaHour, ThoiGianRaMinute, 0);
                ThoiGianRa = dt;
            }
            else
            {
                ThoiGianRa = null;
            }
        }

        private string _resultText = string.Empty;
        public string ResultText { get => _resultText; set { _resultText = value; OnPropertyChanged(nameof(ResultText)); } }

        // Text backing properties for price inputs to avoid direct decimal binding issues
        private string _giaTheoGioText = string.Empty;
        public string GiaTheoGioText { get => _giaTheoGioText; set { _giaTheoGioText = value; OnPropertyChanged(nameof(GiaTheoGioText)); } }

        private string _giaQuaDemText = string.Empty;
        public string GiaQuaDemText { get => _giaQuaDemText; set { _giaQuaDemText = value; OnPropertyChanged(nameof(GiaQuaDemText)); } }

        private string _giaThangText = string.Empty;
        public string GiaThangText { get => _giaThangText; set { _giaThangText = value; OnPropertyChanged(nameof(GiaThangText)); } }

        private BangGia _editingItem = new BangGia();
        public BangGia EditingItem
        {
            get => _editingItem;
            set
            {
                _editingItem = value;
                OnPropertyChanged(nameof(EditingItem));
                // update SelectedLoaiVeId to match the editing item's LoaiVeId
                SelectedLoaiVeId = _editingItem.LoaiVeId;
                OnPropertyChanged(nameof(IsThang));
                OnPropertyChanged(nameof(IsVangLai));
            // populate text fields
            GiaTheoGioText = EditingItem.GiaTheoGio?.ToString() ?? string.Empty;
            GiaQuaDemText = EditingItem.GiaQuaDem?.ToString() ?? string.Empty;
            GiaThangText = EditingItem.GiaThang?.ToString() ?? string.Empty;
            }
        }

        // Selected LoaiVe Id bound from ComboBox SelectedValue (TwoWay)
        private int _selectedLoaiVeId;
        public int SelectedLoaiVeId
        {
            get => _selectedLoaiVeId;
            set
            {
                if (_selectedLoaiVeId == value) return;
                _selectedLoaiVeId = value;
                // reflect into editing item
                if (EditingItem != null) EditingItem.LoaiVeId = _selectedLoaiVeId;
                OnPropertyChanged(nameof(SelectedLoaiVeId));
                OnPropertyChanged(nameof(SelectedLoaiVe));
                OnPropertyChanged(nameof(IsThang));
                OnPropertyChanged(nameof(IsVangLai));

                // reset fields according to selection
                if (IsThang)
                {
                    if (EditingItem != null)
                    {
                        EditingItem.GiaTheoGio = null;
                        EditingItem.GiaQuaDem = null;
                        GiaTheoGioText = string.Empty;
                        GiaQuaDemText = string.Empty;
                        OnPropertyChanged(nameof(EditingItem));
                    }
                }
                else if (IsVangLai)
                {
                    if (EditingItem != null)
                    {
                        EditingItem.GiaThang = null;
                        GiaThangText = string.Empty;
                        OnPropertyChanged(nameof(EditingItem));
                    }
                }
            }
        }

        // Selected LoaiVe object bound from ComboBox SelectedItem (TwoWay)
        private LoaiVe _selectedLoaiVe;
        public LoaiVe SelectedLoaiVe
        {
            get => _selectedLoaiVe ?? LoaiVeList.FirstOrDefault(x => x.Id == SelectedLoaiVeId);
            set
            {
                if (_selectedLoaiVe == value) return;
                _selectedLoaiVe = value;
                SelectedLoaiVeId = _selectedLoaiVe?.Id ?? 0;
                if (EditingItem != null) EditingItem.LoaiVeId = SelectedLoaiVeId;
                OnPropertyChanged(nameof(SelectedLoaiVe));
                OnPropertyChanged(nameof(IsThang));
                OnPropertyChanged(nameof(IsVangLai));

                // Reset values according to selection
                if (IsThang)
                {
                    if (EditingItem != null)
                    {
                        EditingItem.GiaTheoGio = null;
                        EditingItem.GiaQuaDem = null;
                        GiaTheoGioText = string.Empty;
                        GiaQuaDemText = string.Empty;
                        OnPropertyChanged(nameof(EditingItem));
                    }
                }
                else if (IsVangLai)
                {
                    if (EditingItem != null)
                    {
                        EditingItem.GiaThang = null;
                        GiaThangText = string.Empty;
                        OnPropertyChanged(nameof(EditingItem));
                    }
                }
            }
        }

        // Dynamically detect LoaiVe Ids for "Thang" and "VangLai" at runtime based on TenLoai.
        private int? _thangLoaiVeId;
        private int? _vangLaiLoaiVeId;

        // Use ID-based logic: Id 2 = Tháng, Id 1 = VangLai
        public bool IsThang => SelectedLoaiVe != null && SelectedLoaiVe.Id == 2;
        public bool IsVangLai => SelectedLoaiVe != null && SelectedLoaiVe.Id == 1;

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand ComputeDurationCommand { get; }

        public BangGiaManagementViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(_ => Update(), _ => SelectedItem != null);
            DeleteCommand = new RelayCommand(_ => Delete(), _ => SelectedItem != null);
            ClearCommand = new RelayCommand(_ => Clear());
            ComputeDurationCommand = new RelayCommand(_ => ComputeDuration());

            // Cursor positioned for next edit
            // populate hours and minutes
            for (int h = 0; h < 24; h++) Hours.Add(h);
            for (int m = 0; m < 60; m++) Minutes.Add(m);

            Load();
        }

        public void Load()
        {
            Items.Clear();
            LoaiXeList.Clear();
            LoaiVeList.Clear();

            foreach (var lx in _db.GetLoaiXe()) LoaiXeList.Add(lx);
            foreach (var lv in _db.GetLoaiVe()) LoaiVeList.Add(lv);

            // detect Thang and VangLai ids from LoaiVeList to avoid hard-coding
            _thangLoaiVeId = LoaiVeList.FirstOrDefault(x => RemoveDiacritics((x.TenLoai ?? string.Empty)).ToLowerInvariant().Contains("thang"))?.Id;
            _vangLaiLoaiVeId = LoaiVeList.FirstOrDefault(x => RemoveDiacritics((x.TenLoai ?? string.Empty)).ToLowerInvariant().Contains("vang"))?.Id;

            var list = _repo.GetAll();
            foreach (var b in list)
            {
                b.LoaiXe = LoaiXeList.FirstOrDefault(l => l.Id == b.LoaiXeId)?.TenLoai ?? string.Empty;
                b.LoaiVe = LoaiVeList.FirstOrDefault(l => l.Id == b.LoaiVeId)?.TenLoai ?? string.Empty;
                Items.Add(b);
            }

            Clear();
        }

        private void LoadToFormFromSelected()
        {
            if (SelectedItem == null)
            {
                EditingItem = new BangGia();
                return;
            }
            // clone selected into editing instance
            EditingItem = new BangGia
            {
                Id = SelectedItem.Id,
                LoaiXeId = SelectedItem.LoaiXeId,
                LoaiVeId = SelectedItem.LoaiVeId,
                GiaTheoGio = SelectedItem.GiaTheoGio,
                GiaQuaDem = SelectedItem.GiaQuaDem,
                GiaThang = SelectedItem.GiaThang,
                TrangThai = SelectedItem.TrangThai
            };
            OnPropertyChanged(nameof(IsThang));
            OnPropertyChanged(nameof(IsVangLai));
        }

        private string GetLoaiVeName(int? loaiVeId)
        {
            if (!loaiVeId.HasValue) return string.Empty;
            var lv = LoaiVeList.FirstOrDefault(x => x.Id == loaiVeId.Value);
            return (lv?.TenLoai ?? string.Empty).ToLowerInvariant();
        }

        // Utility to remove Vietnamese diacritics for more robust name matching
        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        private void Add()
        {
            try
            {
                // ensure EditingItem.LoaiVeId reflects current selection
                if (EditingItem != null) EditingItem.LoaiVeId = SelectedLoaiVeId;
                // parse text inputs into EditingItem
                ParsePriceInputsIntoModel(EditingItem);
                ValidateForSave(EditingItem, isUpdate: false);
                _repo.Insert(EditingItem);
                MessageBox.Show("Thêm bảng giá thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Thêm thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update()
        {
            if (SelectedItem == null) return;
            try
            {
                // ensure editing item has id
                EditingItem.Id = SelectedItem.Id;
                // ensure EditingItem.LoaiVeId reflects current selection
                if (EditingItem != null) EditingItem.LoaiVeId = SelectedLoaiVeId;
                ParsePriceInputsIntoModel(EditingItem);
                ValidateForSave(EditingItem, isUpdate: true);
                _repo.Update(EditingItem);
                MessageBox.Show("Cập nhật thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cập nhật thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete()
        {
            if (SelectedItem == null) return;
            if (MessageBox.Show($"Bạn có chắc muốn xóa bản ghi ID={SelectedItem.Id}?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            try
            {
                _repo.Delete(SelectedItem.Id);
                MessageBox.Show("Xóa thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xóa thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear()
        {
            EditingItem = new BangGia { LoaiXeId = LoaiXeList.FirstOrDefault()?.Id ?? 0, LoaiVeId = LoaiVeList.FirstOrDefault()?.Id ?? 0, TrangThai = "Active" };
            // ensure SelectedLoaiVeId matches the new editing item so bindings update
            SelectedLoaiVeId = EditingItem.LoaiVeId;
            SelectedItem = null;
            OnPropertyChanged(nameof(IsThang));
            OnPropertyChanged(nameof(IsVangLai));
            GiaTheoGioText = string.Empty;
            GiaQuaDemText = string.Empty;
            GiaThangText = string.Empty;
        }

        private void ParsePriceInputsIntoModel(BangGia model)
        {
            if (model == null) return;
            // reset all
            model.GiaTheoGio = null;
            model.GiaQuaDem = null;
            model.GiaThang = null;

            if (!string.IsNullOrWhiteSpace(GiaTheoGioText) && decimal.TryParse(GiaTheoGioText, out var g1)) model.GiaTheoGio = g1;
            if (!string.IsNullOrWhiteSpace(GiaQuaDemText) && decimal.TryParse(GiaQuaDemText, out var g2)) model.GiaQuaDem = g2;
            if (!string.IsNullOrWhiteSpace(GiaThangText) && decimal.TryParse(GiaThangText, out var gt)) model.GiaThang = gt;
        }

        private void ValidateForSave(BangGia model, bool isUpdate)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.LoaiXeId <= 0) throw new ArgumentException("Vui lòng chọn Loại Xe");
            if (model.LoaiVeId <= 0) throw new ArgumentException("Vui lòng chọn Loại Vé");

            // determine type from detected LoaiVe Ids
            bool isThang = _thangLoaiVeId.HasValue && model.LoaiVeId == _thangLoaiVeId.Value;
            bool isVang = _vangLaiLoaiVeId.HasValue && model.LoaiVeId == _vangLaiLoaiVeId.Value;

            if (isThang)
            {
                // Only GiaThang is required
                if (!model.GiaThang.HasValue) throw new ArgumentException("GiaThang required for Thang");
                if (model.GiaThang < 0) throw new ArgumentException("GiaThang must be >= 0");
                // ensure others null / ignored
                model.GiaTheoGio = null;
                model.GiaQuaDem = null;
            }
            else if (isVang)
            {
                // GiaTheoGio and GiaQuaDem required
                if (!model.GiaTheoGio.HasValue) throw new ArgumentException("GiaTheoGio required for VangLai");
                if (!model.GiaQuaDem.HasValue) throw new ArgumentException("GiaQuaDem required for VangLai");
                if (model.GiaTheoGio < 0 || model.GiaQuaDem < 0) throw new ArgumentException("Prices must be >= 0");
                model.GiaThang = null;
            }
            else
            {
                // for other types require at least one provided
                if (!model.GiaTheoGio.HasValue && !model.GiaQuaDem.HasValue && !model.GiaThang.HasValue)
                    throw new ArgumentException("At least one price must be provided");
            }

            // uniqueness check
            var existing = _repo.GetByLoaiXeAndLoaiVe(model.LoaiXeId, model.LoaiVeId);
            if (!isUpdate && existing != null) throw new InvalidOperationException("Bảng giá đã tồn tại cho cặp LoaiXe+LoaiVe");
            if (isUpdate && existing != null && existing.Id != model.Id) throw new InvalidOperationException("Một bản ghi khác với cùng LoaiXe+LoaiVe tồn tại");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
