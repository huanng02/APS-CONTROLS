using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public enum FormMode { Add, Edit }

    public class RFIDCardWizardViewModel : INotifyPropertyChanged
    {
        private readonly RFIDCardService _service = new RFIDCardService();

        // lists for combobox binding
        private System.Collections.Generic.List<QuanLyGiuXe.Models.LoaiXe> _loaiXeList;
        public System.Collections.Generic.List<QuanLyGiuXe.Models.LoaiXe> LoaiXeList { get => _loaiXeList; set { if (_loaiXeList != value) { _loaiXeList = value; OnPropertyChanged(); } } }

        private System.Collections.Generic.List<QuanLyGiuXe.Models.LoaiVe> _loaiVeList;
        public System.Collections.Generic.List<QuanLyGiuXe.Models.LoaiVe> LoaiVeList { get => _loaiVeList; set { if (_loaiVeList != value) { _loaiVeList = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoaiVeFiltered)); } } }

        // Form mode (Add vs Edit)
        private FormMode _mode = FormMode.Add;
        public FormMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditMode));
                OnPropertyChanged(nameof(IsAddMode));
                // update WindowTitle whenever mode changes
                WindowTitle = _mode == FormMode.Add ? "Thêm thẻ RFID" : "Sửa thẻ RFID";
                OnPropertyChanged(nameof(WindowTitle));
            }
        }

        public bool IsEditMode => Mode == FormMode.Edit;
        public bool IsAddMode => Mode == FormMode.Add;

        private string _windowTitle = "";
        public string WindowTitle { get => _windowTitle; set { if (_windowTitle != value) { _windowTitle = value; OnPropertyChanged(nameof(WindowTitle)); } } }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Commands for actions
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        // RequestClose event allows ViewModel to request the View to close (true = saved, false = canceled)
        public event Action<bool?> RequestClose;

        public RFIDCardWizardViewModel()
        {
            // subscribe to scanner events to populate CardUID in Add mode
            try
            {
                QuanLyGiuXe.Services.RFIDService.Instance.OnCardScanned += Scanner_OnCardScanned;
                // also subscribe to C3200 (network) scanner if available
                try { QuanLyGiuXe.Services.C3200Service.Instance.OnCardScanned += Scanner_OnCardScanned_C3200; } catch { }
                _scannerSubscribed = true;
            }
            catch { }

            SaveCommand = new RelayCommand(_ =>
            {
                // validate based on active tab
                string err;
                if (ActiveTabIndex == 1)
                {
                    Console.WriteLine($"Monthly CardUID: {MonthlyCardUID}");
                    if (!ValidateMonthlyTicket(out err)) return;
                }
                else
                {
                    Console.WriteLine($"Guest CardUID: {GuestCardUID}");
                    if (!ValidateGuestTicket(out err)) return;
                }
                // unsubscribe from scanner to avoid leaks
                UnsubscribeScanner();
                RequestClose?.Invoke(true);
            });

            CancelCommand = new RelayCommand(_ =>
            {
                UnsubscribeScanner();
                RequestClose?.Invoke(false);
            });
        }

        private bool _scannerSubscribed = false;

        private void UnsubscribeScanner()
        {
            if (!_scannerSubscribed) return;
            try
            {
                QuanLyGiuXe.Services.RFIDService.Instance.OnCardScanned -= Scanner_OnCardScanned;
                try { QuanLyGiuXe.Services.C3200Service.Instance.OnCardScanned -= Scanner_OnCardScanned_C3200; } catch { }
            }
            catch { }
            _scannerSubscribed = false;
        }

        private void Scanner_OnCardScanned(string uid)
        {
            // update CardUID only in Add mode
            if (IsEditMode) return;

            var normalized = QuanLyGiuXe.Services.RFIDService.ChuanHoaUID(uid);
            // marshal to UI thread
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (ActiveTabIndex == 1) MonthlyCardUID = normalized; else GuestCardUID = normalized;
            });
        }

        // C3200 supplies cardNo and door; adapt to same update flow
        private void Scanner_OnCardScanned_C3200(string cardNo, int door)
        {
            if (IsEditMode) return;
            var normalized = QuanLyGiuXe.Services.RFIDService.ChuanHoaUID(cardNo);
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (ActiveTabIndex == 1) MonthlyCardUID = normalized; else GuestCardUID = normalized;
            });
        }

        // Mode flag for View to make CardUID readonly
        private bool _isEdit;
        public bool IsEdit { get => _isEdit; set { _isEdit = value; OnPropertyChanged(nameof(IsEdit)); } }

        private int _id;
        public int Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }

        // Separate CardUID fields per tab to avoid cross-write
        private string _guestCardUID;
        public string GuestCardUID
        {
            get => _guestCardUID;
            set
            {
                if (_guestCardUID == value) return;
                if (IsEditMode && ActiveTabIndex == 0)
                {
                    OnPropertyChanged(nameof(GuestCardUID));
                    return;
                }
                _guestCardUID = value;
                OnPropertyChanged(nameof(GuestCardUID));
                OnPropertyChanged(nameof(CardUID));
            }
        }

        private string _monthlyCardUID;
        public string MonthlyCardUID
        {
            get => _monthlyCardUID;
            set
            {
                if (_monthlyCardUID == value) return;
                if (IsEditMode && ActiveTabIndex == 1)
                {
                    OnPropertyChanged(nameof(MonthlyCardUID));
                    return;
                }
                _monthlyCardUID = value;
                OnPropertyChanged(nameof(MonthlyCardUID));
                OnPropertyChanged(nameof(CardUID));
            }
        }

        private int _activeTabIndex = 0; // 0 = Guest, 1 = Monthly
        private bool _suppressActiveTabUpdate = false;
        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (_activeTabIndex == value) return;
                _activeTabIndex = value;
                OnPropertyChanged(nameof(ActiveTabIndex));
                OnPropertyChanged(nameof(IsMonthlyTicket));
                OnPropertyChanged(nameof(CanChangeLoaiVe));
                OnPropertyChanged(nameof(LoaiVeFiltered));
                OnPropertyChanged(nameof(CardUID));

                // If current LoaiVeId is not valid for this tab, set default LoaiVe for the tab.
                try
                {
                    var filtered = LoaiVeFiltered;
                    if ((filtered == null || filtered.Count == 0) && LoaiVeList != null && LoaiVeList.Count > 0)
                    {
                        // ensure there is at least a default
                        _suppressActiveTabUpdate = true;
                        LoaiVeId = GetLoaiVeIdForActiveTab();
                        _suppressActiveTabUpdate = false;
                    }
                    else if (filtered != null && LoaiVeId.HasValue)
                    {
                        var exists = filtered.Find(x => x.Id == LoaiVeId.Value) != null;
                        if (!exists)
                        {
                            _suppressActiveTabUpdate = true;
                            LoaiVeId = GetLoaiVeIdForActiveTab();
                            _suppressActiveTabUpdate = false;
                        }
                    }
                }
                catch { }

                // recalc expiration
                UpdateNgayHetHan();
            }
        }

        // Helper for View to show/hide monthly-only section
        public bool IsMonthlyTicket => ActiveTabIndex == 1;

        // Whether LoaiVe can be changed by the user. Only allowed in 'All' tab (index 2).
        public bool CanChangeLoaiVe => ActiveTabIndex == 2;

        // Filtered list of LoaiVe according to ActiveTabIndex.
        // ActiveTabIndex: 0 = Guest (show non-monthly), 1 = Monthly (show monthly), 2 = All (show all)
        public System.Collections.Generic.List<QuanLyGiuXe.Models.LoaiVe> LoaiVeFiltered
        {
            get
            {
                try
                {
                    if (LoaiVeList == null) return null;
                    if (ActiveTabIndex == 2) return LoaiVeList;
                    var isMonthly = ActiveTabIndex == 1;
                    var res = LoaiVeList.FindAll(x =>
                    {
                        var ten = (x.TenLoai ?? string.Empty).ToLowerInvariant();
                        var monthly = ten.Contains("tháng") || ten.Contains("thang");
                        return isMonthly ? monthly : !monthly;
                    });
                    return res;
                }
                catch { return LoaiVeList; }
            }
        }

        // Unified CardUID for binding in single-screen Add/Edit form. Proxies to guest/monthly slots based on ActiveTabIndex.
        public string CardUID
        {
            get => ActiveTabIndex == 1 ? MonthlyCardUID : GuestCardUID;
            set
            {
                if (ActiveTabIndex == 1) MonthlyCardUID = value; else GuestCardUID = value;
                OnPropertyChanged(nameof(CardUID));
            }
        }

        private string _bienSo;
        public string BienSo { get => _bienSo; set { _bienSo = value; OnPropertyChanged(nameof(BienSo)); } }

        private int? _loaiXeId;
        public int? LoaiXeId { get => _loaiXeId; set { _loaiXeId = value; OnPropertyChanged(nameof(LoaiXeId)); } }

        private int? _loaiVeId;
        public int? LoaiVeId
        {
            get => _loaiVeId;
            set
            {
                _loaiVeId = value;
                OnPropertyChanged(nameof(LoaiVeId));

                if (!_suppressActiveTabUpdate)
                {
                    // determine whether selected LoaiVe is monthly and update ActiveTabIndex
                    bool monthly = false;
                    try
                    {
                        var list = LoaiVeList;
                        if (list == null || list.Count == 0)
                        {
                            list = new LoaiVeService().GetAll();
                            LoaiVeList = list;
                        }

                        if (list != null && _loaiVeId.HasValue)
                        {
                            var item = list.Find(x => x.Id == _loaiVeId.Value);
                            if (item != null)
                            {
                                var ten = (item.TenLoai ?? string.Empty).ToLowerInvariant();
                                monthly = ten.Contains("tháng") || ten.Contains("thang");
                            }
                        }
                    }
                    catch { }

                    ActiveTabIndex = monthly ? 1 : 0;
                }

                // recalc expiration when ticket type changes
                UpdateNgayHetHan();
            }
        }

        private DateTime? _ngayDangKy;
        public DateTime? NgayDangKy { get => _ngayDangKy; set { _ngayDangKy = value; OnPropertyChanged(nameof(NgayDangKy)); UpdateNgayHetHan(); } }

        private DateTime? _ngayHetHan;
        public DateTime? NgayHetHan { get => _ngayHetHan; set { _ngayHetHan = value; OnPropertyChanged(nameof(NgayHetHan)); } }

        private string _trangThai;
        public string TrangThai { get => _trangThai; set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); } }

        // Single-page mode: we no longer track CurrentStep/MaxStep. The View will show sections simultaneously.

        public void LoadForEdit(int id)
        {
            IsEdit = true;
            Id = id;

            var data = _service.GetById(id);
            if (data == null) return;

            // load lists first so IsMonthlyTicket can resolve based on TenLoai
            LoaiXeList = new LoaiXeService().GetAll();
            LoaiVeList = new LoaiVeService().GetAll();
            // set fields from data (set fields BEFORE switching to Edit mode so CardUID can be assigned)
            // determine whether this is monthly ticket from LoaiVe
            LoaiXeId = data.LoaiXeId;
            LoaiVeId = data.LoaiVeId;
            BienSo = data.BienSo;
            NgayDangKy = data.NgayDangKy;
            NgayHetHan = data.NgayHetHan;
            TrangThai = data.TrangThai;

            // set CardUID into proper slot and select active tab based on LoaiVe
            // ensure lists are loaded first
            var monthly = false;
            if (LoaiVeList != null && LoaiVeId.HasValue)
            {
                var item = LoaiVeList.Find(x => x.Id == LoaiVeId.Value);
                if (item != null)
                {
                    var ten = (item.TenLoai ?? string.Empty).ToLowerInvariant();
                    monthly = ten.Contains("tháng") || ten.Contains("thang");
                }
            }

            ActiveTabIndex = monthly ? 1 : 0;
            if (monthly) MonthlyCardUID = data.CardUID; else GuestCardUID = data.CardUID;

            // set Edit mode AFTER fields are populated so CardUID assignment is allowed
            Mode = FormMode.Edit;

            // notify all
            OnPropertyChanged(nameof(LoaiXeList));
            OnPropertyChanged(nameof(LoaiVeList));
            OnPropertyChanged(nameof(GuestCardUID));
            OnPropertyChanged(nameof(MonthlyCardUID));
            OnPropertyChanged(nameof(LoaiXeId));
            OnPropertyChanged(nameof(LoaiVeId));
            OnPropertyChanged(nameof(BienSo));
            OnPropertyChanged(nameof(NgayDangKy));
            OnPropertyChanged(nameof(NgayHetHan));
            OnPropertyChanged(nameof(TrangThai));
            OnPropertyChanged(nameof(IsEdit));
        }

        // Validate all sections according to existing rules. Keeps validation messages intact.
        // Validation split by tab context
        public bool ValidateGuestTicket(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(GuestCardUID)) { errorMessage = "Card UID không được để trống"; return false; }
            if (!LoaiXeId.HasValue) { errorMessage = "Vui lòng chọn loại xe"; return false; }
            return true;
        }

        public bool ValidateMonthlyTicket(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(MonthlyCardUID)) { errorMessage = "Card UID không được để trống"; return false; }
            if (!LoaiXeId.HasValue) { errorMessage = "Vui lòng chọn loại xe"; return false; }
            if (string.IsNullOrWhiteSpace(BienSo)) { errorMessage = "Biển số là bắt buộc"; return false; }
            if (!NgayDangKy.HasValue) { errorMessage = "Chọn ngày đăng ký"; return false; }
            return true;
        }

        // Helper to map active tab to LoaiVeId (keeps backend contract)
        public int GetLoaiVeIdForActiveTab()
        {
            if (LoaiVeList == null || LoaiVeList.Count == 0) return 0;
            if (ActiveTabIndex == 1)
            {
                var item = LoaiVeList.Find(x => (x.TenLoai ?? string.Empty).ToLowerInvariant().Contains("tháng") || (x.TenLoai ?? string.Empty).ToLowerInvariant().Contains("thang"));
                if (item != null) return item.Id;
                return LoaiVeList[0].Id;
            }
            else
            {
                // guest: prefer non-monthly entries
                var item = LoaiVeList.Find(x => !((x.TenLoai ?? string.Empty).ToLowerInvariant().Contains("tháng") || (x.TenLoai ?? string.Empty).ToLowerInvariant().Contains("thang")));
                if (item != null) return item.Id;
                return LoaiVeList[0].Id;
            }
        }

        public void InitForAdd()
        {
            IsEdit = false;
            Mode = FormMode.Add;
            Id = 0;
            GuestCardUID = string.Empty;
            MonthlyCardUID = string.Empty;
            BienSo = string.Empty;
            LoaiXeId = null;
            LoaiVeId = null;
            NgayDangKy = null;
            NgayHetHan = null;
            TrangThai = "Active";

            OnPropertyChanged(nameof(GuestCardUID));
            OnPropertyChanged(nameof(MonthlyCardUID));
            OnPropertyChanged(nameof(BienSo));
            OnPropertyChanged(nameof(LoaiXeId));
            OnPropertyChanged(nameof(LoaiVeId));
            OnPropertyChanged(nameof(NgayDangKy));
            OnPropertyChanged(nameof(NgayHetHan));
            OnPropertyChanged(nameof(TrangThai));
            OnPropertyChanged(nameof(IsEdit));

            LoaiXeList = new LoaiXeService().GetAll();
            LoaiVeList = new LoaiVeService().GetAll();
        }

        private void UpdateNgayHetHan()
        {
            if (!NgayDangKy.HasValue)
            {
                NgayHetHan = null;
                return;
            }

            // only calculate for monthly tickets (determined by active tab)
            if (ActiveTabIndex == 1)
            {
                NgayHetHan = NgayDangKy.Value.AddMonths(1);
            }
            else
            {
                NgayHetHan = null;
            }
        }
    }
}
