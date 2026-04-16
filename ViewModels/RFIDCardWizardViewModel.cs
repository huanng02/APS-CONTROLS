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
        public System.Collections.Generic.List<QuanLyGiuXe.Models.LoaiVe> LoaiVeList { get => _loaiVeList; set { if (_loaiVeList != value) { _loaiVeList = value; OnPropertyChanged(); } } }

        // Form mode (Add vs Edit)
        private FormMode _mode = FormMode.Add;
        public FormMode Mode { get => _mode; set { if (_mode != value) { _mode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditMode)); OnPropertyChanged(nameof(IsAddMode)); } } }

        public bool IsEditMode => Mode == FormMode.Edit;
        public bool IsAddMode => Mode == FormMode.Add;

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Commands for navigation and actions
        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
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

            NextCommand = new RelayCommand(_ =>
            {
                Console.WriteLine($"CardUID: {CardUID}");
                if (!ValidateCurrentStep(out string err)) return;

                // If at step 3 and the selected ticket is not monthly, trigger Save flow (skip step 4)
                if (CurrentStep == 3 && !IsMonthlyTicket)
                {
                    // reuse SaveCommand so validation and close behavior are consistent
                    SaveCommand.Execute(null);
                    return;
                }
                if (CurrentStep < MaxStep) CurrentStep++;
            });

            BackCommand = new RelayCommand(_ =>
            {
                // If step 4 was skipped (shouldn't normally be reachable), ensure back navigates correctly
                if (CurrentStep == 4 && !IsMonthlyTicket)
                {
                    CurrentStep = 3;
                    return;
                }

                if (CurrentStep > 1) CurrentStep--;
            });

            SaveCommand = new RelayCommand(_ =>
            {
                Console.WriteLine($"CardUID: {CardUID}");
                if (!ValidateCurrentStep(out string err)) return;
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
                CardUID = normalized;
                // optionally move to next step automatically
                // if currently on step1, advance to step2
                if (CurrentStep == 1) CurrentStep = 2;
            });
        }

        // C3200 supplies cardNo and door; adapt to same update flow
        private void Scanner_OnCardScanned_C3200(string cardNo, int door)
        {
            if (IsEditMode) return;
            var normalized = QuanLyGiuXe.Services.RFIDService.ChuanHoaUID(cardNo);
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                CardUID = normalized;
                if (CurrentStep == 1) CurrentStep = 2;
            });
        }

        // Mode flag for View to make CardUID readonly
        private bool _isEdit;
        public bool IsEdit { get => _isEdit; set { _isEdit = value; OnPropertyChanged(nameof(IsEdit)); } }

        private int _id;
        public int Id { get => _id; set { _id = value; OnPropertyChanged(nameof(Id)); } }

        private string _cardUID;
        public string CardUID
        {
            get => _cardUID;
            set
            {
                if (_cardUID == value) return;
                // Prevent changing UID in edit mode
                if (IsEditMode)
                {
                    // revert any attempted change by notifying UI to refresh
                    OnPropertyChanged(nameof(CardUID));
                    return;
                }
                _cardUID = value;
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
                OnPropertyChanged(nameof(IsMonthlyTicket));

                // adjust steps depending on ticket type (monthly vs one-time)
                MaxStep = IsMonthlyTicket ? 4 : 3;
                OnPropertyChanged(nameof(MaxStep));

                // ensure CurrentStep is within new bounds
                if (CurrentStep > MaxStep) CurrentStep = MaxStep;

                // recalc expiration when ticket type changes
                UpdateNgayHetHan();

                // notify ShowStep4 change
                OnPropertyChanged(nameof(ShowStep4));
            }
        }

        private DateTime? _ngayDangKy;
        public DateTime? NgayDangKy { get => _ngayDangKy; set { _ngayDangKy = value; OnPropertyChanged(nameof(NgayDangKy)); UpdateNgayHetHan(); } }

        private DateTime? _ngayHetHan;
        public DateTime? NgayHetHan { get => _ngayHetHan; set { _ngayHetHan = value; OnPropertyChanged(nameof(NgayHetHan)); } }

        private string _trangThai;
        public string TrangThai { get => _trangThai; set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); } }

        private int _currentStep = 1;
        public int CurrentStep { get => _currentStep; set { _currentStep = value; OnPropertyChanged(nameof(CurrentStep)); OnPropertyChanged(nameof(IsFirstStep)); OnPropertyChanged(nameof(IsLastStep)); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); OnPropertyChanged(nameof(IsStep4)); OnPropertyChanged(nameof(CanGoBack)); OnPropertyChanged(nameof(ShowStep4)); } }

        private int _maxStep = 4;
        public int MaxStep { get => _maxStep; set { if (_maxStep != value) { _maxStep = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLastStep)); } } }

        public bool IsFirstStep => CurrentStep == 1;
        public bool IsLastStep => CurrentStep == MaxStep;
        public bool CanGoBack => CurrentStep > 1;
        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool IsStep4 => CurrentStep == 4;

        // Show step 4 only when ticket is monthly and current step is 4
        public bool ShowStep4 => IsMonthlyTicket && IsStep4;

        public void LoadForEdit(int id)
        {
            IsEdit = true;
            CurrentStep = 1;
            Id = id;

            var data = _service.GetById(id);
            if (data == null) return;

            // load lists first so IsMonthlyTicket can resolve based on TenLoai
            LoaiXeList = new LoaiXeService().GetAll();
            LoaiVeList = new LoaiVeService().GetAll();
            // set fields from data (set fields BEFORE switching to Edit mode so CardUID can be assigned)
            CardUID = data.CardUID;
            LoaiXeId = data.LoaiXeId;
            LoaiVeId = data.LoaiVeId;
            BienSo = data.BienSo;
            NgayDangKy = data.NgayDangKy;
            NgayHetHan = data.NgayHetHan;
            TrangThai = data.TrangThai;

            // set Edit mode AFTER fields are populated so CardUID assignment is allowed
            Mode = FormMode.Edit;

            // notify all
            OnPropertyChanged(nameof(LoaiXeList));
            OnPropertyChanged(nameof(LoaiVeList));
            OnPropertyChanged(nameof(CardUID));
            OnPropertyChanged(nameof(LoaiXeId));
            OnPropertyChanged(nameof(LoaiVeId));
            OnPropertyChanged(nameof(BienSo));
            OnPropertyChanged(nameof(NgayDangKy));
            OnPropertyChanged(nameof(NgayHetHan));
            OnPropertyChanged(nameof(TrangThai));
            OnPropertyChanged(nameof(IsEdit));
            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(ShowStep4));
        }

        // Validate current step and return message if invalid
        public bool ValidateCurrentStep(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (CurrentStep == 1)
            {
                if (string.IsNullOrWhiteSpace(CardUID)) { errorMessage = "Card UID không được để trống"; return false; }
            }
            else if (CurrentStep == 2)
            {
                if (!LoaiXeId.HasValue) { errorMessage = "Vui lòng chọn loại xe"; return false; }
            }
            else if (CurrentStep == 3)
            {
                if (!LoaiVeId.HasValue) { errorMessage = "Vui lòng chọn loại vé"; return false; }
            }
            else if (CurrentStep == 4)
            {
                // Monthly
                if (string.IsNullOrWhiteSpace(BienSo)) { errorMessage = "Biển số là bắt buộc"; return false; }
                if (!NgayDangKy.HasValue) { errorMessage = "Chọn ngày đăng ký"; return false; }
            }
            return true;
        }

        public void InitForAdd()
        {
            IsEdit = false;
            CurrentStep = 1;
            Id = 0;
            CardUID = string.Empty;
            BienSo = string.Empty;
            LoaiXeId = null;
            LoaiVeId = null;
            NgayDangKy = null;
            NgayHetHan = null;
            TrangThai = "Active";

            OnPropertyChanged(nameof(CardUID));
            OnPropertyChanged(nameof(BienSo));
            OnPropertyChanged(nameof(LoaiXeId));
            OnPropertyChanged(nameof(LoaiVeId));
            OnPropertyChanged(nameof(NgayDangKy));
            OnPropertyChanged(nameof(NgayHetHan));
            OnPropertyChanged(nameof(TrangThai));
            OnPropertyChanged(nameof(IsEdit));
            OnPropertyChanged(nameof(CurrentStep));

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

            // only calculate for monthly tickets
            if (IsMonthlyTicket)
            {
                // add one month for monthly ticket
                NgayHetHan = NgayDangKy.Value.AddMonths(1);
            }
            else
            {
                NgayHetHan = null;
            }
        }

        // determine if selected LoaiVe is monthly based on loaded LoaiVeList data (TenLoai)
        public bool IsMonthlyTicket
        {
            get
            {
                if (!LoaiVeId.HasValue || LoaiVeList == null) return false;
                var item = LoaiVeList.Find(x => x.Id == LoaiVeId.Value);
                if (item == null) return false;
                var ten = (item.TenLoai ?? string.Empty).ToLowerInvariant();
                // check common Vietnamese word for month 'tháng' or non-accented 'thang'
                return ten.Contains("tháng") || ten.Contains("thang");
            }
        }
    }
}
