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

        // All defined KhungGio slots (for display above price editor)
        public ObservableCollection<KhungGio> KhungGioList { get; } = new ObservableCollection<KhungGio>();
        // UI-focused KhungGia items (join of KhungGio + BangGiaKhungGio)
        public System.Collections.ObjectModel.ObservableCollection<QuanLyGiuXe.ViewModels.KhungGiaItemVM> KhungGiaItems { get; } = new System.Collections.ObjectModel.ObservableCollection<QuanLyGiuXe.ViewModels.KhungGiaItemVM>();

        // number of BangGiaKhungGio entries associated with the current EditingItem
        private int _khungGiaCount;
        public int KhungGiaCount { get => _khungGiaCount; set { _khungGiaCount = value; OnPropertyChanged(nameof(KhungGiaCount)); } }

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

        // Determines whether per-slot prices can be edited (false for monthly tickets)
        // Static rule: LoaiVe.Id == 2 => Tháng (monthly) -> cannot edit khung; others can
        public bool CanEditKhungGia => EditingItem != null && EditingItem.LoaiVeId != 2;


        // Compute duration and cost helper (uses GiaBanNgay when available)
        private void ComputeDuration()
        {
            // For demo: prompt user to enter start/end times via InputBox style prompts are not available in WPF by default
            // We'll just compute duration if EditingItem has temporary fields StartTimeText and EndTimeText
            // For now, show a MessageBox with guidance.
            MessageBox.Show("Tính thời lượng: chọn thời gian vào/ra trong form (tính năng demo).", "Tính thời lượng", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Compute()
        {
            // Minimal compute: validate input then delegate pricing to PaymentService
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

            var loaiXeId = EditingItem?.LoaiXeId ?? 0;
            var loaiVeId = SelectedLoaiVeId;

            if (loaiXeId <= 0 || loaiVeId <= 0)
            {
                ResultText = "Chưa cấu hình bảng giá";
                OnPropertyChanged(nameof(ResultText));
                return;
            }

            try
            {
                var paymentService = new PaymentService();
                var total = paymentService.CalculateFee(loaiXeId, loaiVeId, start, end);
                ResultText = $"Tiền: {FormatVND(total)}";
            }
            catch (Exception ex)
            {
                // Log error and show generic message
                ResultText = "Lỗi khi tính tiền: kiểm tra cấu hình bảng giá";
                LoggingService.Instance.LogError("ComputePriceFailed", "BangGiaManagementViewModel", "Failed to compute price", ex);
            }
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
        public ObservableCollection<int> Seconds { get; } = new ObservableCollection<int>();

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
        private int _thoiGianVaoSecond;
        public int ThoiGianVaoSecond { get => _thoiGianVaoSecond; set { _thoiGianVaoSecond = value; OnPropertyChanged(nameof(ThoiGianVaoSecond)); UpdateThoiGianVao(); } }

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
        private int _thoiGianRaSecond;
        public int ThoiGianRaSecond { get => _thoiGianRaSecond; set { _thoiGianRaSecond = value; OnPropertyChanged(nameof(ThoiGianRaSecond)); UpdateThoiGianRa(); } }

        private void UpdateThoiGianVao()
        {
            if (ThoiGianVaoDate.HasValue)
            {
                var dt = ThoiGianVaoDate.Value.Date + new TimeSpan(ThoiGianVaoHour, ThoiGianVaoMinute, ThoiGianVaoSecond);
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
                var dt = ThoiGianRaDate.Value.Date + new TimeSpan(ThoiGianRaHour, ThoiGianRaMinute, ThoiGianRaSecond);
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
            // populate text fields (pricing moved to KhungGio/BangGiaKhungGio; BangGia keeps GiaThang)
            GiaThangText = EditingItem.GiaThang?.ToString() ?? string.Empty;
            // when editing item changes, load its KhungGiaList (join KhungGio + BangGiaKhungGio)
            LoadKhungGiaForEditingItem();
            // notify CanEditKhungGia
            OnPropertyChanged(nameof(CanEditKhungGia));
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
                OnPropertyChanged(nameof(CanEditKhungGia));

                // reset fields according to selection
                if (IsThang)
                {
                    if (EditingItem != null)
                    {
                        // For monthly tickets, GiaThang is used; per-slot prices are managed separately.
                        OnPropertyChanged(nameof(EditingItem));
                    }
                }
                else if (IsVangLai)
                {
                    if (EditingItem != null)
                    {
                        // monthly price not applicable for Vãng lai
                        GiaThangText = string.Empty;
                        EditingItem.GiaThang = null;
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
                        // For monthly tickets, only GiaThang is applicable. Per-slot prices are managed via KhungGio/BangGiaKhungGio.
                        GiaThangText = EditingItem.GiaThang?.ToString() ?? string.Empty;
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

        // Static ID-based ticket type identification
        // Assumption: LoaiVe.Id == 1 => Vãng lai; LoaiVe.Id == 2 => Tháng
        public bool IsThang => SelectedLoaiVeId == 2;
        public bool IsVangLai => SelectedLoaiVeId == 1;

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
            for (int s = 0; s < 60; s++) Seconds.Add(s);

            Load();
        }

        private void LoadKhungGioDefinitions()
        {
            KhungGioList.Clear();
            var repo = new KhungGioRepository();
            foreach (var k in repo.GetAll()) KhungGioList.Add(k);
        }

        public void Load()
        {
            Items.Clear();
            LoaiXeList.Clear();
            LoaiVeList.Clear();

            foreach (var lx in _db.GetLoaiXe()) LoaiXeList.Add(lx);
            foreach (var lv in _db.GetLoaiVe()) LoaiVeList.Add(lv);

            // Using static ID mapping for LoaiVe: 1 = Vãng lai, 2 = Tháng

            var list = _repo.GetAll();
            foreach (var b in list)
            {
                b.LoaiXe = LoaiXeList.FirstOrDefault(l => l.Id == b.LoaiXeId)?.TenLoai ?? string.Empty;
                b.LoaiVe = LoaiVeList.FirstOrDefault(l => l.Id == b.LoaiVeId)?.TenLoai ?? string.Empty;
                Items.Add(b);
            }

            // load khung gio definitions for header display
            LoadKhungGioDefinitions();

            // populate KhungGiaCount for each item (optional: lazy load in UI if expensive)
            var bgkRepo = new BangGiaKhungGioRepository();
            var khungRepo = new KhungGioRepository();
            foreach (var item in Items)
            {
                try
                {
                    var entries = bgkRepo.GetByBangGiaId(item.Id);
                    item.KhungGiaCount = entries.Count;
                    // populate TenKhungGio and assign KhungGiaList for inline editing
                    var khungs = khungRepo.GetAll();
                    foreach (var e in entries)
                    {
                        var k = khungs.FirstOrDefault(x => x.Id == e.KhungGioId);
                        if (k != null)
                        {
                            e.TenKhungGio = k.TenKhungGio;
                            e.GioBatDau = k.GioBatDau;
                            e.GioKetThuc = k.GioKetThuc;
                            e.QuaDem = k.QuaDem;
                        }
                    }
                    // also include missing khung rows initialized with zero price
                var missingForItem = khungs.Where(k => !entries.Any(ev => ev.KhungGioId == k.Id))
                                              .Select(k => new BangGiaKhungGio { Id = 0, BangGiaId = item.Id, KhungGioId = k.Id, GiaTien = 0m, TenKhungGio = k.TenKhungGio, GioBatDau = k.GioBatDau, GioKetThuc = k.GioKetThuc, QuaDem = k.QuaDem })
                                              .ToList();
                    entries.AddRange(missingForItem);
                    item.KhungGiaList = new System.Collections.ObjectModel.ObservableCollection<BangGiaKhungGio>(entries);
                    // build display for DataGrid (show only if not monthly)
                    // Static rule: LoaiVeId == 2 is monthly (Tháng)
                    bool isItemThang = item.LoaiVeId == 2;
                    if (isItemThang)
                        item.GiaTheoKhungDisplay = string.Empty;
                    else
                    {
                        var parts = item.KhungGiaList.Select(e => $"{e.TenKhungGio}: {e.GiaTien:N0}").ToList();
                        item.GiaTheoKhungDisplay = string.Join(" | ", parts.Where(p => !p.EndsWith(": 0")));
                    }
                }
                catch { item.KhungGiaCount = 0; item.KhungGiaList = new System.Collections.ObjectModel.ObservableCollection<BangGiaKhungGio>(); }
            }

            Clear();
        }

        private void LoadKhungGiaForEditingItem()
        {
            try
            {
                if (EditingItem == null)
                {
                    EditingItem = new BangGia();
                }

                var bgkRepo = new BangGiaKhungGioRepository();
                var khungRepo = new KhungGioRepository();

                var entries = EditingItem.Id > 0 ? bgkRepo.GetByBangGiaId(EditingItem.Id) : new System.Collections.Generic.List<BangGiaKhungGio>();
                var khungs = khungRepo.GetAll();

                // join and populate TenKhungGio on each BangGiaKhungGio for display/editing
                // ensure we include all KhungGio rows so user can edit prices even if mapping missing
                var allKhungs = khungs;
                var map = allKhungs.ToDictionary(k => k.Id);
                // include entries for khung ids that are present in KhungGio table but missing in entries
                var missing = allKhungs.Where(k => !entries.Any(e => e.KhungGioId == k.Id)).Select(k => new BangGiaKhungGio { Id = 0, BangGiaId = EditingItem.Id, KhungGioId = k.Id, GiaTien = 0m, TenKhungGio = k.TenKhungGio, GioBatDau = k.GioBatDau, GioKetThuc = k.GioKetThuc }).ToList();
                foreach (var e in entries)
                {
                    if (map.TryGetValue(e.KhungGioId, out var k))
                    {
                        e.TenKhungGio = k.TenKhungGio;
                        e.GioBatDau = k.GioBatDau;
                        e.GioKetThuc = k.GioKetThuc;
                    }
                    else
                    {
                        // fallback label if khung definition missing
                        e.TenKhungGio = $"Khung#{e.KhungGioId}";
                    }
                }
                entries.AddRange(missing);

                // Map into KhungGiaItems VM (read-only times, editable price)
                KhungGiaItems.Clear();
                foreach (var k in khungs)
                {
                    var match = entries.FirstOrDefault(e => e.KhungGioId == k.Id);
                    var dto = new KhungGiaItemVM
                    {
                        KhungGioId = k.Id,
                        TenKhungGio = k.TenKhungGio,
                        GioBatDau = k.GioBatDau,
                        GioKetThuc = k.GioKetThuc,
                        GiaTien = match != null ? match.GiaTien : 0m
                    };
                    KhungGiaItems.Add(dto);
                }

                // keep EditingItem.KhungGiaList for compatibility (not used for editing in UI)
                EditingItem.KhungGiaList = new System.Collections.ObjectModel.ObservableCollection<BangGiaKhungGio>(entries);
                OnPropertyChanged(nameof(EditingItem));
            }
            catch { EditingItem.KhungGiaList = new System.Collections.ObjectModel.ObservableCollection<BangGiaKhungGio>(); }
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
                // per-slot pricing moved to KhungGio/BangGiaKhungGio
                GiaThang = SelectedItem.GiaThang,
                TrangThai = SelectedItem.TrangThai
            };
            // ensure KhungGiaList is populated for editing (create default mappings if missing)
            LoadKhungGiaForEditingItem();
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
                if (EditingItem != null) EditingItem.LoaiVeId = SelectedLoaiVeId; // always sync
                // parse text inputs into EditingItem
                ParsePriceInputsIntoModel(EditingItem);
                ValidateForSave(EditingItem, isUpdate: false);

                // insert BangGia row
                _repo.Insert(EditingItem);

                // retrieve created record to get Id
                var created = _repo.GetByLoaiXeAndLoaiVe(EditingItem.LoaiXeId, EditingItem.LoaiVeId);
                if (created != null && created.Id > 0)
                {
                    EditingItem.Id = created.Id;
                    // persist khung gia rows for this banggia (create all entries, even zero)
                    SaveKhungGiaList(EditingItem);
                }

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
                if (EditingItem != null) EditingItem.LoaiVeId = SelectedLoaiVeId; // always sync before validation
                ParsePriceInputsIntoModel(EditingItem);
                // DEBUG: show current ticket type ids and counts (temporary)
                try
                {
                    var dbg = $"SelectedLoaiVeId={SelectedLoaiVeId}, EditingItem.LoaiVeId={EditingItem.LoaiVeId}, SelectedLoaiVe={(SelectedLoaiVe?.TenLoai ?? "(null)" )}, GiaThang={EditingItem.GiaThang}, KhungGiaItems.Count={KhungGiaItems?.Count ?? 0}";
                    System.Windows.MessageBox.Show(dbg, "DEBUG: ValidateForSave inputs", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }

                // Validate model-level fields
                ValidateForSave(EditingItem, isUpdate: true);

                // If khung gia editable, validate per-slot prices before saving
                if (CanEditKhungGia)
                {
                    // Ensure DTO list is in sync
                    if (KhungGiaItems == null || KhungGiaItems.Count == 0)
                    {
                        MessageBox.Show("Danh sách khung giá trống", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var invalid = KhungGiaItems.Where(k => k.GiaTien < 0).ToList();
                    if (invalid.Any())
                    {
                        var names = string.Join(", ", invalid.Select(i => string.IsNullOrWhiteSpace(i.TenKhungGio) ? ("#" + i.KhungGioId) : i.TenKhungGio));
                        MessageBox.Show($"Không thể lưu. Giá tiền phải >= 0 cho các khung: {names}", "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Validate time overlaps and equality
                    // Build intervals considering overnight
                    var intervals = KhungGiaItems.Select(k => new { Id = k.KhungGioId, Start = k.GioBatDau, End = k.GioKetThuc, QuaDem = k.QuaDem }).ToList();
                    // Ensure Start != End
                    var equal = intervals.Where(i => i.Start == i.End).ToList();
                    if (equal.Any())
                    {
                        MessageBox.Show("Một hoặc nhiều khung có thời gian bắt đầu trùng kết thúc. Vui lòng kiểm tra.", "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Check overlaps: for each pair, determine if they overlap on a 24h cycle
                    for (int a = 0; a < intervals.Count; a++)
                    {
                        for (int b = a + 1; b < intervals.Count; b++)
                        {
                            var ia = intervals[a];
                            var ib = intervals[b];
                            bool overlap = CheckTimeOverlap(ia.Start, ia.End, ib.Start, ib.End);
                            if (overlap)
                            {
                                MessageBox.Show($"Khung '{KhungGiaItems[a].TenKhungGio}' chồng với '{KhungGiaItems[b].TenKhungGio}'. Vui lòng sửa.", "Lỗi dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                    }
                }

                // persist BangGia first
                // If any slot times were edited, warn because these changes apply globally
                var khungRepo = new KhungGioRepository();
                var defs = khungRepo.GetAll();
                var changedDefs = KhungGiaItems.Where(dto =>
                    {
                        var def = defs.FirstOrDefault(d => d.Id == dto.KhungGioId);
                        if (def == null) return false;
                        return def.GioBatDau != dto.GioBatDau || def.GioKetThuc != dto.GioKetThuc;
                    }).ToList();

                if (changedDefs.Any())
                {
                    var names = string.Join(", ", changedDefs.Select(d => d.TenKhungGio ?? ("#" + d.KhungGioId)));
                    var msg = $"Bạn đã thay đổi thời gian cho các khung: {names}. Những thay đổi này áp dụng cho tất cả bảng giá. Tiếp tục?";
                    if (MessageBox.Show(msg, "Xác nhận thay đổi khung giờ", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                    // apply updates to KhungGio definitions
                    foreach (var dto in changedDefs)
                    {
                        var def = defs.FirstOrDefault(d => d.Id == dto.KhungGioId);
                        if (def != null)
                        {
                            def.GioBatDau = dto.GioBatDau;
                            def.GioKetThuc = dto.GioKetThuc;
                            khungRepo.Update(def);
                        }
                    }
                }

                _repo.Update(EditingItem);
                // save KhungGiaList back to DB if applicable
                if (CanEditKhungGia) SaveKhungGiaList(EditingItem);
                MessageBox.Show("Cập nhật thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cập nhật thất bại: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Check overlap between two intervals on 24h cycle. Handles overnight when end < start.
        private bool CheckTimeOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
        {
            // Convert intervals to sets of (start,end) possibly split if overnight
            var aIntervals = NormalizeIntervals(aStart, aEnd);
            var bIntervals = NormalizeIntervals(bStart, bEnd);

            foreach (var ai in aIntervals)
            {
                foreach (var bi in bIntervals)
                {
                    // overlap if ai.Start < bi.End && bi.Start < ai.End
                    if (ai.start < bi.end && bi.start < ai.end) return true;
                }
            }
            return false;
        }

        // Normalize an interval into 1 or 2 non-overnight intervals within [0,24h)
        private System.Collections.Generic.List<(TimeSpan start, TimeSpan end)> NormalizeIntervals(TimeSpan start, TimeSpan end)
        {
            var result = new System.Collections.Generic.List<(TimeSpan, TimeSpan)>();
            if (end > start)
            {
                result.Add((start, end));
            }
            else
            {
                // overnight: split into [start,24:00) and [00:00,end)
                result.Add((start, TimeSpan.FromHours(24)));
                result.Add((TimeSpan.Zero, end));
            }
            return result;
        }

        private void SaveKhungGiaList(BangGia model)
        {
            if (model == null || model.Id <= 0) return;
            try
            {
                var repo = new BangGiaKhungGioRepository();
                // existing entries: remove all then insert current set (simple upsert)
                var existing = repo.GetByBangGiaId(model.Id);
                foreach (var ex in existing) repo.Delete(ex.Id);

                // Insert current prices from KhungGiaItems (left-joined list from UI)
                foreach (var dto in KhungGiaItems)
                {
                    var bgk = new BangGiaKhungGio
                    {
                        BangGiaId = model.Id,
                        KhungGioId = dto.KhungGioId,
                        GiaTien = dto.GiaTien
                    };
                    repo.Insert(bgk);
                }
            }
            catch { }
        }

        private bool ValidateKhungGiaList(BangGia model, out string message)
        {
            message = string.Empty;
            if (model == null) { message = "Model is null"; return false; }

            var khungRepo = new KhungGioRepository();
            var defs = khungRepo.GetAll();
            if (defs == null || defs.Count == 0) { message = "Không có khung giờ nào được định nghĩa"; return false; }

            if (model.KhungGiaList == null)
            {
                message = "Danh sách giá theo khung trống";
                return false;
            }

            // Ensure one entry per definition
            var missing = defs.Where(d => !model.KhungGiaList.Any(e => e.KhungGioId == d.Id)).ToList();
            if (missing.Any())
            {
                message = "Thiếu khung giờ: " + string.Join(", ", missing.Select(m => m.TenKhungGio ?? ("#" + m.Id)));
                return false;
            }

            // Validate values
            var invalid = model.KhungGiaList.Where(e => e.GiaTien < 0).ToList();
            if (invalid.Any())
            {
                message = "Giá tiền phải lớn hơn hoặc bằng 0 cho mọi khung giờ.";
                return false;
            }

            return true;
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
            GiaThangText = string.Empty;
        }

        private void ParsePriceInputsIntoModel(BangGia model)
        {
            if (model == null) return;
            // reset all
            model.GiaThang = null;

            if (!string.IsNullOrWhiteSpace(GiaThangText) && decimal.TryParse(GiaThangText, out var gt)) model.GiaThang = gt;
        }

        private void ValidateForSave(BangGia model, bool isUpdate)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.LoaiXeId <= 0) throw new ArgumentException("Vui lòng chọn Loại Xe");
            if (model.LoaiVeId <= 0) throw new ArgumentException("Vui lòng chọn Loại Vé");
            // Ensure model.LoaiVeId is synced with current selection
            if ((model.LoaiVeId <= 0) && SelectedLoaiVeId > 0) model.LoaiVeId = SelectedLoaiVeId;

            // Static ID-based determination (1 = VangLai, 2 = Thang)
            bool isThang = (model.LoaiVeId == 2) || (SelectedLoaiVeId == 2);
            bool isVang = (model.LoaiVeId == 1) || (SelectedLoaiVeId == 1);

            if (!isThang && !isVang)
            {
                // If still ambiguous, fail explicitly
                throw new ArgumentException("LoaiVeId không hợp lệ. Expect 1 (Vãng lai) or 2 (Tháng)");
            }

            if (isThang)
            {
                // For monthly tickets, GiaThang is required and must be non-negative
                if (!model.GiaThang.HasValue) throw new ArgumentException("GiaThang là bắt buộc cho vé tháng");
                if (model.GiaThang < 0) throw new ArgumentException("GiaThang phải >= 0");
            }
            else // isVang
            {
                // For Vãng lai, ensure per-slot pricing exists and is valid
                var hasPrices = (KhungGiaItems != null && KhungGiaItems.Count > 0) || (model.KhungGiaList != null && model.KhungGiaList.Count > 0);
                if (!hasPrices)
                    throw new ArgumentException("Vãng lai yêu cầu danh sách giá theo khung (KhungGiaItems)");

                // Validate prices from KhungGiaItems preferentially (UI source)
                if (KhungGiaItems != null && KhungGiaItems.Count > 0)
                {
                    var invalid = KhungGiaItems.Where(e => e.GiaTien < 0).ToList();
                    if (invalid.Any()) throw new ArgumentException("Tất cả giá theo khung (GiaTien) phải >= 0");
                }
                else
                {
                    var invalid = model.KhungGiaList.Where(e => e.GiaTien < 0).ToList();
                    if (invalid.Any()) throw new ArgumentException("Tất cả giá theo khung (GiaTien) phải >= 0");
                }

                // Do not require GiaThang for transient tickets
                model.GiaThang = null;
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
