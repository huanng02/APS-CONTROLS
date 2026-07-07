using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Services.OfflineCache;

namespace QuanLyGiuXe.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DatabaseService db = new();
        private readonly EnterpriseCrudService _crudService = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ── Properties ────────────────────────────────────────────────────────────

        private string _bienSoNhap = "";
        public string BienSoNhap
        {
            get => _bienSoNhap;
            set { _bienSoNhap = FormatLicensePlate(value); OnPropertyChanged(nameof(BienSoNhap)); }
        }

        private bool _isLprAvailable = false;
        /// <summary>
        /// Indicates whether the last LPR/AI operation returned a valid plate.
        /// UI can bind to this to disable actions when LPR is unavailable.
        /// </summary>
        public bool IsLprAvailable
        {
            get => _isLprAvailable;
            set { _isLprAvailable = value; OnPropertyChanged(nameof(IsLprAvailable)); }
        }

        private string _lprStatusLabel = "LPR: ?";
        /// <summary>
        /// Short human readable LPR status shown on the main status bar.
        /// </summary>
        public string LprStatusLabel
        {
            get => _lprStatusLabel;
            set { _lprStatusLabel = value; OnPropertyChanged(nameof(LprStatusLabel)); }
        }

        private string _lane1ManualInput = "";
        public string Lane1ManualInput
        {
            get => _lane1ManualInput;
            set { _lane1ManualInput = value; OnPropertyChanged(nameof(Lane1ManualInput)); }
        }

        private string _lane2ManualInput = "";
        public string Lane2ManualInput
        {
            get => _lane2ManualInput;
            set { _lane2ManualInput = value; OnPropertyChanged(nameof(Lane2ManualInput)); }
        }

        private string _tienHienThi = "";
        public string TienHienThi
        {
            get => _tienHienThi;
            set { _tienHienThi = value; OnPropertyChanged(nameof(TienHienThi)); }
        }

        private string _tuKhoaTimKiem = "";
        public string TuKhoaTimKiem
        {
            get => _tuKhoaTimKiem;
            set { _tuKhoaTimKiem = value; OnPropertyChanged(nameof(TuKhoaTimKiem)); TimKiemXe(); }
        }

        public string LastScannedUID { get; set; } = "";
        public bool CanOpenBarrier => PermissionService.Instance.CheckPermission("OPEN_BARRIER");

        public object CurrentView { get; set; }
        public ObservableCollection<Xe> DanhSachXe { get; set; }

        // ── Làn Vào ──────────────────────────────────────────────────────────────

        private string _lane1BienSo = "";
        public string Lane1BienSo
        {
            get => _lane1BienSo;
            set { _lane1BienSo = FormatLicensePlate(value); OnPropertyChanged(nameof(Lane1BienSo)); }
        }

        private string _lane1TrangThai = "Chờ xe...";
        public string Lane1TrangThai
        {
            get => _lane1TrangThai;
            set 
            { 
                _lane1TrangThai = value; 
                OnPropertyChanged(nameof(Lane1TrangThai)); 
                UpdateLaneStatusColor(1, value);
            }
        }

        private string _lane1UID = "";
        public string Lane1UID
        {
            get => _lane1UID;
            set { _lane1UID = value; OnPropertyChanged(nameof(Lane1UID)); }
        }

        private string _lane1Tien = "";
        public string Lane1Tien
        {
            get => _lane1Tien;
            set { _lane1Tien = value; OnPropertyChanged(nameof(Lane1Tien)); }
        }

        private string _lane1ThoiGianVao = "";
        public string Lane1ThoiGianVao
        {
            get => _lane1ThoiGianVao;
            set { _lane1ThoiGianVao = value; OnPropertyChanged(nameof(Lane1ThoiGianVao)); }
        }

        private string _lane1ThoiGianTrongBai = "";
        public string Lane1ThoiGianTrongBai
        {
            get => _lane1ThoiGianTrongBai;
            set { _lane1ThoiGianTrongBai = value; OnPropertyChanged(nameof(Lane1ThoiGianTrongBai)); }
        }

        // ── Lane 2 (Right UI) ─────────────────────────────────────────────────
        private string _lane2BienSo = "";
        public string Lane2BienSo
        {
            get => _lane2BienSo;
            set { _lane2BienSo = FormatLicensePlate(value); OnPropertyChanged(nameof(Lane2BienSo)); }
        }

        private string _workstationId = "";
        public string WorkstationId
        {
            get => _workstationId;
            set 
            { 
                _workstationId = value; 
                OnPropertyChanged(nameof(WorkstationId)); 
                OnPropertyChanged(nameof(WorkstationLabel));
            }
        }

        public string WorkstationLabel
        {
            get
            {
                if (IsRunningOnDatabaseServer())
                {
                    return "MÁY CHỦ";
                }
                return (!string.IsNullOrEmpty(_workstationId) && _workstationId.ToUpper().Contains("SERVER")) ? "MÁY CHỦ" : "MÁY TRẠM";
            }
        }

        private bool IsRunningOnDatabaseServer()
        {
            try
            {
                var config = ConnectionManager.Instance.CurrentConfig;
                string serverHost = config?.ServerIP?.Trim();
                if (string.IsNullOrEmpty(serverHost)) return false;

                // Loopback checks
                if (serverHost == "." || 
                    serverHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) || 
                    serverHost.Equals("127.0.0.1") || 
                    serverHost.Equals("::1"))
                {
                    return true;
                }

                // Machine name check
                string localHostName = System.Net.Dns.GetHostName();
                if (serverHost.Equals(localHostName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Resolve server IP and local IPs to compare
                var localIPs = System.Net.Dns.GetHostEntry(localHostName).AddressList;
                System.Net.IPAddress[] serverIPs;
                try
                {
                    serverIPs = System.Net.Dns.GetHostEntry(serverHost).AddressList;
                }
                catch
                {
                    // If serverHost is an IP address, GetHostEntry might throw on some setups,
                    // so try parsing directly
                    if (System.Net.IPAddress.TryParse(serverHost, out var parsedIP))
                    {
                        serverIPs = new[] { parsedIP };
                    }
                    else
                    {
                        return false;
                    }
                }

                foreach (var localIP in localIPs)
                {
                    foreach (var serverIP in serverIPs)
                    {
                        if (localIP.Equals(serverIP))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // fail-safe
            }
            return false;
        }

        private string _lane2TrangThai = "Chờ xe...";
        public string Lane2TrangThai
        {
            get => _lane2TrangThai;
            set 
            { 
                _lane2TrangThai = value; 
                OnPropertyChanged(nameof(Lane2TrangThai)); 
                UpdateLaneStatusColor(2, value);
            }
        }

        private string _lane2UID = "";
        public string Lane2UID
        {
            get => _lane2UID;
            set { _lane2UID = value; OnPropertyChanged(nameof(Lane2UID)); }
        }

        private string _lane2Tien = "";
        public string Lane2Tien
        {
            get => _lane2Tien;
            set { _lane2Tien = value; OnPropertyChanged(nameof(Lane2Tien)); }
        }

        private string _lane2ThoiGianVao = "";
        public string Lane2ThoiGianVao
        {
            get => _lane2ThoiGianVao;
            set { _lane2ThoiGianVao = value; OnPropertyChanged(nameof(Lane2ThoiGianVao)); }
        }

        private string _lane2ThoiGianTrongBai = "";
        public string Lane2ThoiGianTrongBai
        {
            get => _lane2ThoiGianTrongBai;
            set { _lane2ThoiGianTrongBai = value; OnPropertyChanged(nameof(Lane2ThoiGianTrongBai)); }
        }

        // --- Lane 1 Card & Owner Details properties ---
        private string _lane1CardName = "";
        public string Lane1CardName
        {
            get => _lane1CardName;
            set { _lane1CardName = value; OnPropertyChanged(nameof(Lane1CardName)); }
        }

        private string _lane1LoaiXe = "";
        public string Lane1LoaiXe
        {
            get => _lane1LoaiXe;
            set { _lane1LoaiXe = value; OnPropertyChanged(nameof(Lane1LoaiXe)); }
        }

        private string _lane1LoaiVe = "";
        public string Lane1LoaiVe
        {
            get => _lane1LoaiVe;
            set { _lane1LoaiVe = value; OnPropertyChanged(nameof(Lane1LoaiVe)); }
        }

        private string _lane1EmployeeName = "";
        public string Lane1EmployeeName
        {
            get => _lane1EmployeeName;
            set { _lane1EmployeeName = value; OnPropertyChanged(nameof(Lane1EmployeeName)); }
        }

        private string _lane1EmployeeCode = "";
        public string Lane1EmployeeCode
        {
            get => _lane1EmployeeCode;
            set { _lane1EmployeeCode = value; OnPropertyChanged(nameof(Lane1EmployeeCode)); }
        }

        private string _lane1EmployeeCompany = "";
        public string Lane1EmployeeCompany
        {
            get => _lane1EmployeeCompany;
            set { _lane1EmployeeCompany = value; OnPropertyChanged(nameof(Lane1EmployeeCompany)); }
        }

        private string _lane1EmployeeDepartment = "";
        public string Lane1EmployeeDepartment
        {
            get => _lane1EmployeeDepartment;
            set { _lane1EmployeeDepartment = value; OnPropertyChanged(nameof(Lane1EmployeeDepartment)); }
        }

        private string _lane1EmployeePosition = "";
        public string Lane1EmployeePosition
        {
            get => _lane1EmployeePosition;
            set { _lane1EmployeePosition = value; OnPropertyChanged(nameof(Lane1EmployeePosition)); }
        }

        private string _lane1EmployeePhone = "";
        public string Lane1EmployeePhone
        {
            get => _lane1EmployeePhone;
            set { _lane1EmployeePhone = value; OnPropertyChanged(nameof(Lane1EmployeePhone)); }
        }

        private string _lane1EmployeeEmail = "";
        public string Lane1EmployeeEmail
        {
            get => _lane1EmployeeEmail;
            set { _lane1EmployeeEmail = value; OnPropertyChanged(nameof(Lane1EmployeeEmail)); }
        }

        private ImageSource? _lane1EmployeeAvatar;
        public ImageSource? Lane1EmployeeAvatar
        {
            get => _lane1EmployeeAvatar;
            set { _lane1EmployeeAvatar = value; OnPropertyChanged(nameof(Lane1EmployeeAvatar)); }
        }

        private ImageSource? _lane1PlateInImage;
        public ImageSource? Lane1PlateInImage
        {
            get => _lane1PlateInImage;
            set { _lane1PlateInImage = value; OnPropertyChanged(nameof(Lane1PlateInImage)); }
        }

        private ImageSource? _lane1PlateOutImage;
        public ImageSource? Lane1PlateOutImage
        {
            get => _lane1PlateOutImage;
            set { _lane1PlateOutImage = value; OnPropertyChanged(nameof(Lane1PlateOutImage)); }
        }

        private Visibility _lane1PlateInVisibility = Visibility.Collapsed;
        public Visibility Lane1PlateInVisibility
        {
            get => _lane1PlateInVisibility;
            set { _lane1PlateInVisibility = value; OnPropertyChanged(nameof(Lane1PlateInVisibility)); }
        }

        private Visibility _lane1PlateOutVisibility = Visibility.Collapsed;
        public Visibility Lane1PlateOutVisibility
        {
            get => _lane1PlateOutVisibility;
            set { _lane1PlateOutVisibility = value; OnPropertyChanged(nameof(Lane1PlateOutVisibility)); }
        }

        private bool _lane1HasEmployee;
        public bool Lane1HasEmployee
        {
            get => _lane1HasEmployee;
            set 
            { 
                _lane1HasEmployee = value; 
                OnPropertyChanged(nameof(Lane1HasEmployee)); 
                OnPropertyChanged(nameof(Lane1HasNoEmployee));
            }
        }

        public bool Lane1HasNoEmployee => !Lane1HasEmployee;

        private string _lane1OwnerStatusText = "Chưa cập nhật thông tin chủ thẻ";
        public string Lane1OwnerStatusText
        {
            get => _lane1OwnerStatusText;
            set { _lane1OwnerStatusText = value; OnPropertyChanged(nameof(Lane1OwnerStatusText)); }
        }

        // --- Lane 2 Card & Owner Details properties ---
        private string _lane2CardName = "";
        public string Lane2CardName
        {
            get => _lane2CardName;
            set { _lane2CardName = value; OnPropertyChanged(nameof(Lane2CardName)); }
        }

        private string _lane2LoaiXe = "";
        public string Lane2LoaiXe
        {
            get => _lane2LoaiXe;
            set { _lane2LoaiXe = value; OnPropertyChanged(nameof(Lane2LoaiXe)); }
        }

        private string _lane2LoaiVe = "";
        public string Lane2LoaiVe
        {
            get => _lane2LoaiVe;
            set { _lane2LoaiVe = value; OnPropertyChanged(nameof(Lane2LoaiVe)); }
        }

        private string _lane2EmployeeName = "";
        public string Lane2EmployeeName
        {
            get => _lane2EmployeeName;
            set { _lane2EmployeeName = value; OnPropertyChanged(nameof(Lane2EmployeeName)); }
        }

        private string _lane2EmployeeCode = "";
        public string Lane2EmployeeCode
        {
            get => _lane2EmployeeCode;
            set { _lane2EmployeeCode = value; OnPropertyChanged(nameof(Lane2EmployeeCode)); }
        }

        private string _lane2EmployeeCompany = "";
        public string Lane2EmployeeCompany
        {
            get => _lane2EmployeeCompany;
            set { _lane2EmployeeCompany = value; OnPropertyChanged(nameof(Lane2EmployeeCompany)); }
        }

        private string _lane2EmployeeDepartment = "";
        public string Lane2EmployeeDepartment
        {
            get => _lane2EmployeeDepartment;
            set { _lane2EmployeeDepartment = value; OnPropertyChanged(nameof(Lane2EmployeeDepartment)); }
        }

        private string _lane2EmployeePosition = "";
        public string Lane2EmployeePosition
        {
            get => _lane2EmployeePosition;
            set { _lane2EmployeePosition = value; OnPropertyChanged(nameof(Lane2EmployeePosition)); }
        }

        private string _lane2EmployeePhone = "";
        public string Lane2EmployeePhone
        {
            get => _lane2EmployeePhone;
            set { _lane2EmployeePhone = value; OnPropertyChanged(nameof(Lane2EmployeePhone)); }
        }

        private string _lane2EmployeeEmail = "";
        public string Lane2EmployeeEmail
        {
            get => _lane2EmployeeEmail;
            set { _lane2EmployeeEmail = value; OnPropertyChanged(nameof(Lane2EmployeeEmail)); }
        }

        private ImageSource? _lane2EmployeeAvatar;
        public ImageSource? Lane2EmployeeAvatar
        {
            get => _lane2EmployeeAvatar;
            set { _lane2EmployeeAvatar = value; OnPropertyChanged(nameof(Lane2EmployeeAvatar)); }
        }

        private ImageSource? _lane2PlateInImage;
        public ImageSource? Lane2PlateInImage
        {
            get => _lane2PlateInImage;
            set { _lane2PlateInImage = value; OnPropertyChanged(nameof(Lane2PlateInImage)); }
        }

        private ImageSource? _lane2PlateOutImage;
        public ImageSource? Lane2PlateOutImage
        {
            get => _lane2PlateOutImage;
            set { _lane2PlateOutImage = value; OnPropertyChanged(nameof(Lane2PlateOutImage)); }
        }

        private Visibility _lane2PlateInVisibility = Visibility.Collapsed;
        public Visibility Lane2PlateInVisibility
        {
            get => _lane2PlateInVisibility;
            set { _lane2PlateInVisibility = value; OnPropertyChanged(nameof(Lane2PlateInVisibility)); }
        }

        private Visibility _lane2PlateOutVisibility = Visibility.Collapsed;
        public Visibility Lane2PlateOutVisibility
        {
            get => _lane2PlateOutVisibility;
            set { _lane2PlateOutVisibility = value; OnPropertyChanged(nameof(Lane2PlateOutVisibility)); }
        }

        private bool _lane2HasEmployee;
        public bool Lane2HasEmployee
        {
            get => _lane2HasEmployee;
            set 
            { 
                _lane2HasEmployee = value; 
                OnPropertyChanged(nameof(Lane2HasEmployee)); 
                OnPropertyChanged(nameof(Lane2HasNoEmployee));
            }
        }

        public bool Lane2HasNoEmployee => !Lane2HasEmployee;

        private string _lane2OwnerStatusText = "Chưa cập nhật thông tin chủ thẻ";
        public string Lane2OwnerStatusText
        {
            get => _lane2OwnerStatusText;
            set { _lane2OwnerStatusText = value; OnPropertyChanged(nameof(Lane2OwnerStatusText)); }
        }

        private static ImageSource? ConvertBase64ToImage(string? base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] binaryData = Convert.FromBase64String(base64);
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(binaryData);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        // ── Lane 1 Notification Overlay ──────────────────────────────────────
        private bool _isLane1NotificationVisible;
        public bool IsLane1NotificationVisible
        {
            get => _isLane1NotificationVisible;
            set { _isLane1NotificationVisible = value; OnPropertyChanged(nameof(IsLane1NotificationVisible)); }
        }

        private string _lane1NotificationMessage = "";
        public string Lane1NotificationMessage
        {
            get => _lane1NotificationMessage;
            set { _lane1NotificationMessage = value; OnPropertyChanged(nameof(Lane1NotificationMessage)); }
        }

        private System.Windows.Media.Brush _lane1NotificationColor;
        public System.Windows.Media.Brush Lane1NotificationColor
        {
            get => _lane1NotificationColor;
            set { _lane1NotificationColor = value; OnPropertyChanged(nameof(Lane1NotificationColor)); }
        }

        private string _lane1NotificationIcon = "ℹ";
        public string Lane1NotificationIcon
        {
            get => _lane1NotificationIcon;
            set { _lane1NotificationIcon = value; OnPropertyChanged(nameof(Lane1NotificationIcon)); }
        }

        // ── Lane 2 Notification Overlay ──────────────────────────────────────
        private bool _isLane2NotificationVisible;
        public bool IsLane2NotificationVisible
        {
            get => _isLane2NotificationVisible;
            set { _isLane2NotificationVisible = value; OnPropertyChanged(nameof(IsLane2NotificationVisible)); }
        }

        private string _lane2NotificationMessage = "";
        public string Lane2NotificationMessage
        {
            get => _lane2NotificationMessage;
            set { _lane2NotificationMessage = value; OnPropertyChanged(nameof(Lane2NotificationMessage)); }
        }

        private System.Windows.Media.Brush _lane2NotificationColor;
        public System.Windows.Media.Brush Lane2NotificationColor
        {
            get => _lane2NotificationColor;
            set { _lane2NotificationColor = value; OnPropertyChanged(nameof(Lane2NotificationColor)); }
        }

        private string _lane2NotificationIcon = "ℹ";
        public string Lane2NotificationIcon
        {
            get => _lane2NotificationIcon;
            set { _lane2NotificationIcon = value; OnPropertyChanged(nameof(Lane2NotificationIcon)); }
        }

        // ── Lane 1 Info Panel Highlight ──────────────────────────────────────
        private System.Windows.Media.Brush _lane1InfoBackground;
        public System.Windows.Media.Brush Lane1InfoBackground
        {
            get => _lane1InfoBackground ??= GetDefaultInfoBackground();
            set { _lane1InfoBackground = value; OnPropertyChanged(nameof(Lane1InfoBackground)); }
        }

        private System.Windows.Media.Brush _lane1InfoBorderBrush = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush Lane1InfoBorderBrush
        {
            get => _lane1InfoBorderBrush;
            set { _lane1InfoBorderBrush = value; OnPropertyChanged(nameof(Lane1InfoBorderBrush)); }
        }

        // ── Lane 2 Info Panel Highlight ──────────────────────────────────────
        private System.Windows.Media.Brush _lane2InfoBackground;
        public System.Windows.Media.Brush Lane2InfoBackground
        {
            get => _lane2InfoBackground ??= GetDefaultInfoBackground();
            set { _lane2InfoBackground = value; OnPropertyChanged(nameof(Lane2InfoBackground)); }
        }

        private System.Windows.Media.Brush _lane2InfoBorderBrush = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush Lane2InfoBorderBrush
        {
            get => _lane2InfoBorderBrush;
            set { _lane2InfoBorderBrush = value; OnPropertyChanged(nameof(Lane2InfoBorderBrush)); }
        }

        private System.Windows.Media.Brush GetDefaultInfoBackground()
        {
            System.Windows.Media.Brush? brush = null;
            if (Application.Current != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    brush = Application.Current.TryFindResource("BgLightBrush") as System.Windows.Media.Brush;
                }
                else
                {
                    brush = Application.Current.Dispatcher.Invoke(() => Application.Current.TryFindResource("BgLightBrush") as System.Windows.Media.Brush);
                }
            }

            if (brush != null)
            {
                if (brush.CanFreeze)
                {
                    var clone = brush.Clone();
                    clone.Freeze();
                    return clone;
                }
                return brush;
            }

            var defaultBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            defaultBrush.Freeze();
            return defaultBrush;
        }

        private ObservableCollection<ParkingSite> _sitesList = new();
        public ObservableCollection<ParkingSite> SitesList
        {
            get => _sitesList;
            set { _sitesList = value; OnPropertyChanged(nameof(SitesList)); }
        }

        private ParkingSite? _selectedSite;
        public ParkingSite? SelectedSite
        {
            get => _selectedSite;
            set
            {
                if (_selectedSite != value)
                {
                    _selectedSite = value;
                    OnPropertyChanged(nameof(SelectedSite));
                    UpdateVehicleCount();
                }
            }
        }

        private string _trangThaiKetNoi = "C3200: Đang kết nối...";
        public string TrangThaiKetNoi
        {
            get => _trangThaiKetNoi;
            set { _trangThaiKetNoi = value; OnPropertyChanged(nameof(TrangThaiKetNoi)); }
        }

        // ── Dynamic Lane Configuration ───────────────────────────────────────────
        private string _lane1Title = "LÀN 1";
        public string Lane1Title
        {
            get => _lane1Title;
            set { _lane1Title = value; OnPropertyChanged(nameof(Lane1Title)); }
        }

        private string _lane2Title = "LÀN 2";
        public string Lane2Title
        {
            get => _lane2Title;
            set { _lane2Title = value; OnPropertyChanged(nameof(Lane2Title)); }
        }

        private System.Windows.Media.Brush _lane1Color = (Application.Current?.Resources["APSBlueBrush"] as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Blue;
        public System.Windows.Media.Brush Lane1Color
        {
            get => _lane1Color;
            set { _lane1Color = value; OnPropertyChanged(nameof(Lane1Color)); }
        }

        private System.Windows.Media.Brush _lane2Color = (Application.Current?.Resources["APSRedBrush"] as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Red;
        public System.Windows.Media.Brush Lane2Color
        {
            get => _lane2Color;
            set { _lane2Color = value; OnPropertyChanged(nameof(Lane2Color)); }
        }

        private System.Windows.Media.Brush _lane1StatusColor = System.Windows.Media.Brushes.Green;
        public System.Windows.Media.Brush Lane1StatusColor
        {
            get => _lane1StatusColor;
            set { _lane1StatusColor = value; OnPropertyChanged(nameof(Lane1StatusColor)); }
        }

        private System.Windows.Media.Brush _lane2StatusColor = System.Windows.Media.Brushes.Green;
        public System.Windows.Media.Brush Lane2StatusColor
        {
            get => _lane2StatusColor;
            set { _lane2StatusColor = value; OnPropertyChanged(nameof(Lane2StatusColor)); }
        }

        private string _lane1ButtonText = "MỞ CỔNG 1";
        public string Lane1ButtonText
        {
            get => _lane1ButtonText;
            set { _lane1ButtonText = value; OnPropertyChanged(nameof(Lane1ButtonText)); }
        }

        private string _lane2ButtonText = "MỞ CỔNG 2";
        public string Lane2ButtonText
        {
            get => _lane2ButtonText;
            set { _lane2ButtonText = value; OnPropertyChanged(nameof(Lane2ButtonText)); }
        }

        private bool _isLane1Inbound = true;
        public bool IsLane1Inbound
        {
            get => _isLane1Inbound;
            set { _isLane1Inbound = value; OnPropertyChanged(nameof(IsLane1Inbound)); }
        }

        private bool _isLane2Inbound = false;
        public bool IsLane2Inbound
        {
            get => _isLane2Inbound;
            set { _isLane2Inbound = value; OnPropertyChanged(nameof(IsLane2Inbound)); }
        }

        private string _lane1InfoLabel = "THÔNG TIN XE VÀO";
        public string Lane1InfoLabel
        {
            get => _lane1InfoLabel;
            set { _lane1InfoLabel = value; OnPropertyChanged(nameof(Lane1InfoLabel)); }
        }

        private string _lane2InfoLabel = "THÔNG TIN XE RA";
        public string Lane2InfoLabel
        {
            get => _lane2InfoLabel;
            set { _lane2InfoLabel = value; OnPropertyChanged(nameof(Lane2InfoLabel)); }
        }

        public Visibility Lane1FeeVisibility => IsLane1Inbound ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Lane2FeeVisibility => IsLane2Inbound ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Lane1TimeVisibility => IsLane1Inbound ? Visibility.Collapsed : Visibility.Visible;
        public Visibility Lane2TimeVisibility => IsLane2Inbound ? Visibility.Collapsed : Visibility.Visible;

        public string Lane1ReaderMappingIn => GetReaderMappingIn(1);
        public string Lane1ReaderMappingOut => GetReaderMappingOut(1);
        public string Lane1ReaderMappingEmpty => (string.IsNullOrEmpty(Lane1ReaderMappingIn) && string.IsNullOrEmpty(Lane1ReaderMappingOut)) ? "⚠ CHƯA CẤU HÌNH ĐẦU ĐỌC" : "";
        
        public string Lane2ReaderMappingIn => GetReaderMappingIn(2);
        public string Lane2ReaderMappingOut => GetReaderMappingOut(2);
        public string Lane2ReaderMappingEmpty => (string.IsNullOrEmpty(Lane2ReaderMappingIn) && string.IsNullOrEmpty(Lane2ReaderMappingOut)) ? "⚠ CHƯA CẤU HÌNH ĐẦU ĐỌC" : "";

        // ── Topology Info ────────────────────────────────────────────────────────
        private string _lane1TopologyText = "Chưa cấu hình Zone";
        public string Lane1TopologyText
        {
            get => _lane1TopologyText;
            set { _lane1TopologyText = value; OnPropertyChanged(nameof(Lane1TopologyText)); }
        }

        private string _lane1CapacityText = "";
        public string Lane1CapacityText
        {
            get => _lane1CapacityText;
            set { _lane1CapacityText = value; OnPropertyChanged(nameof(Lane1CapacityText)); }
        }

        private string _lane2TopologyText = "Chưa cấu hình Zone";
        public string Lane2TopologyText
        {
            get => _lane2TopologyText;
            set { _lane2TopologyText = value; OnPropertyChanged(nameof(Lane2TopologyText)); }
        }

        private string _lane2CapacityText = "";
        public string Lane2CapacityText
        {
            get => _lane2CapacityText;
            set { _lane2CapacityText = value; OnPropertyChanged(nameof(Lane2CapacityText)); }
        }

        private bool _isLane1Visible = true;
        public bool IsLane1Visible
        {
            get => _isLane1Visible;
            set { _isLane1Visible = value; OnPropertyChanged(nameof(IsLane1Visible)); }
        }

        private bool _isLane2Visible = true;
        public bool IsLane2Visible
        {
            get => _isLane2Visible;
            set { _isLane2Visible = value; OnPropertyChanged(nameof(IsLane2Visible)); }
        }

        private bool _isDualLaneMode = true;
        public bool IsDualLaneMode
        {
            get => _isDualLaneMode;
            set { _isDualLaneMode = value; OnPropertyChanged(nameof(IsDualLaneMode)); }
        }

        private bool HasActiveReaders(int laneId)
        {
            return ReaderLaneMappingService.Instance.GetAll()
                .Any(m => m.IsEnabled && m.LaneId == laneId);
        }

        public void CalculateLaneVisibilities()
        {
            int? lane1Id = GetDbLaneIdForUiIndex(1);
            int? lane2Id = GetDbLaneIdForUiIndex(2);

            bool lane1Visible = lane1Id.HasValue && HasActiveReaders(lane1Id.Value);
            bool lane2Visible = lane2Id.HasValue && HasActiveReaders(lane2Id.Value);

            // Fallback: if no active readers are configured anywhere, show both lanes by default
            if (!lane1Visible && !lane2Visible)
            {
                lane1Visible = true;
                lane2Visible = true;
            }

            IsLane1Visible = lane1Visible;
            IsLane2Visible = lane2Visible;
            IsDualLaneMode = lane1Visible && lane2Visible;
        }
        // ─────────────────────────────────────────────────────────────────────────

        public int? GetDbLaneIdForUiIndex(int uiLaneIndex)
        {
            var distinctLaneIds = ReaderLaneMappingService.Instance.GetActiveLaneIds();

            try
            {
                var lanes = ParkingTopologyService.Instance.GetLanes();
                if (lanes != null)
                {
                    distinctLaneIds = distinctLaneIds
                        .OrderBy(id => {
                            var lane = lanes.FirstOrDefault(l => l.Id == id);
                            return (lane?.DisplayIndex == null || lane.DisplayIndex == 0) ? 999 : lane.DisplayIndex.Value;
                        })
                        .ThenBy(id => id)
                        .ToList();
                }
            }
            catch { }

            if (uiLaneIndex == 1)
            {
                if (distinctLaneIds.Count > 0) return distinctLaneIds[0];
                return 1; // default fallback
            }
            else // uiLaneIndex == 2
            {
                if (distinctLaneIds.Count > 1) return distinctLaneIds[1];
                if (distinctLaneIds.Count == 1) return null; // only 1 lane configured
                return 2; // default fallback
            }
        }

        private async Task SyncLaneUIStateAsync(int uiLaneIndex)
        {
            try
            {
                int? dbLaneId = GetDbLaneIdForUiIndex(uiLaneIndex);

                if (dbLaneId == null)
                {
                    return;
                }

                string currentDir = "IN";
                string laneName = $"LÀN {uiLaneIndex}";
                string vehicleTypeSuffix = "Hỗn hợp";

                var activeMappingsForLane = ReaderLaneMappingService.Instance.GetAll()
                    .Where(m => m.IsEnabled && m.LaneId == dbLaneId.Value)
                    .ToList();

                var state = LaneRuntimeManager.Instance.GetLaneState(dbLaneId.Value);
                if (state != null)
                {
                    currentDir = state.CurrentDirection;
                }
                else if (activeMappingsForLane.Any())
                {
                    currentDir = activeMappingsForLane.First().Direction;
                }

                bool isInbound = (currentDir != "OUT");

                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                var laneDb = lanes.FirstOrDefault(l => l.Id == dbLaneId.Value);
                if (laneDb != null)
                {
                    laneName = laneDb.LaneName;
                    vehicleTypeSuffix = (laneDb.LoaiXeId.HasValue && !string.IsNullOrEmpty(laneDb.LoaiXeName)) 
                        ? laneDb.LoaiXeName 
                        : "Hỗn hợp";
                }
                
                var (_, zoneId, _, siteName, zoneName, maxCapacity, _, gateName) = await ResolveTopologyForLaneAsync(dbLaneId.Value);
                string displayLocation = !string.IsNullOrEmpty(gateName) ? $"Cổng: {gateName}" : (!string.IsNullOrEmpty(zoneName) ? $"Zone: {zoneName}" : "Chưa cấu hình");
                string topoText = string.IsNullOrEmpty(siteName) ? "Chưa cấu hình Cổng" : $"Bãi xe: {siteName} - {displayLocation}";
                int count = 0;
                if (zoneId.HasValue) count = await db.GetXeTrongBaiCountByZoneAsync(zoneId.Value);

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (uiLaneIndex == 1)
                    {
                        Lane1TopologyText = topoText;
                    }
                    else
                    {
                        Lane2TopologyText = topoText;
                    }

                    string dirSuffix = "";
                    if (currentDir == "IN") dirSuffix = "VÀO";
                    else if (currentDir == "OUT") dirSuffix = "RA";
                    else if (currentDir == "MAINTENANCE") dirSuffix = "BẢO TRÌ";
                    else if (currentDir == "DISABLED") dirSuffix = "VÔ HIỆU HÓA";

                    string title = $"{laneName.ToUpper()} [{dirSuffix}] - {vehicleTypeSuffix.ToUpper()}";

                    string brushKey = "APSBlueBrush";
                    if (currentDir == "OUT")
                    {
                        brushKey = "APSRedBrush";
                    }
                    else if (currentDir == "MAINTENANCE")
                    {
                        brushKey = "WarningBrush";
                    }
                    else if (currentDir == "DISABLED")
                    {
                        brushKey = "TextSecondaryBrush";
                    }

                    var dynamicBrush = Application.Current?.Resources[brushKey] as System.Windows.Media.Brush;
                    if (dynamicBrush == null)
                    {
                        if (brushKey == "APSBlueBrush")
                            dynamicBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 79, 163));
                        else if (brushKey == "APSRedBrush")
                            dynamicBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 57, 85));
                        else if (brushKey == "WarningBrush")
                            dynamicBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                        else if (brushKey == "TextSecondaryBrush")
                            dynamicBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158));
                    }

                    if (uiLaneIndex == 1)
                    {
                        IsLane1Inbound = isInbound;
                        Lane1Title = title;
                        Lane1InfoLabel = (currentDir == "MAINTENANCE" || currentDir == "DISABLED") ? "LÀN ĐANG BẢO TRÌ" : (isInbound ? "THÔNG TIN XE VÀO" : "THÔNG TIN XE RA");
                        Lane1ButtonText = "MỞ CỔNG 1";
                        if (dynamicBrush != null) Lane1Color = dynamicBrush;

                        if (currentDir == "MAINTENANCE")
                        {
                            Lane1TrangThai = "Làn đang bảo trì";
                        }
                        else if (currentDir == "DISABLED")
                        {
                            Lane1TrangThai = "Làn đã vô hiệu hóa";
                        }
                        else if (Lane1TrangThai == "Làn đang bảo trì" || Lane1TrangThai == "Làn đã vô hiệu hóa")
                        {
                            Lane1TrangThai = "Chờ xe...";
                        }

                        OnPropertyChanged(nameof(Lane1FeeVisibility));
                        OnPropertyChanged(nameof(Lane1TimeVisibility));
                        OnPropertyChanged(nameof(Lane1ReaderMappingIn));
                        OnPropertyChanged(nameof(Lane1ReaderMappingOut));
                        OnPropertyChanged(nameof(Lane1ReaderMappingEmpty));
                    }
                    else if (uiLaneIndex == 2)
                    {
                        IsLane2Inbound = isInbound;
                        Lane2Title = title;
                        Lane2InfoLabel = (currentDir == "MAINTENANCE" || currentDir == "DISABLED") ? "LÀN ĐANG BẢO TRÌ" : (isInbound ? "THÔNG TIN XE VÀO" : "THÔNG TIN XE RA");
                        Lane2ButtonText = "MỞ CỔNG 2";
                        if (dynamicBrush != null) Lane2Color = dynamicBrush;

                        if (currentDir == "MAINTENANCE")
                        {
                            Lane2TrangThai = "Làn đang bảo trì";
                        }
                        else if (currentDir == "DISABLED")
                        {
                            Lane2TrangThai = "Làn đã vô hiệu hóa";
                        }
                        else if (Lane2TrangThai == "Làn đang bảo trì" || Lane2TrangThai == "Làn đã vô hiệu hóa")
                        {
                            Lane2TrangThai = "Chờ xe...";
                        }

                        OnPropertyChanged(nameof(Lane2FeeVisibility));
                        OnPropertyChanged(nameof(Lane2TimeVisibility));
                        OnPropertyChanged(nameof(Lane2ReaderMappingIn));
                        OnPropertyChanged(nameof(Lane2ReaderMappingOut));
                        OnPropertyChanged(nameof(Lane2ReaderMappingEmpty));
                    }
                });
            }
            catch (Exception ex)
            {
                try { LoggingService.Instance.LogError("SyncLaneUIState", "MainViewModel", $"Lỗi đồng bộ giao diện làn {uiLaneIndex}", ex); } catch { }
            }
        }

        private string GetReaderMappingIn(int uiLaneIndex)
        {
            int? dbLaneId = GetDbLaneIdForUiIndex(uiLaneIndex);
            if (dbLaneId == null) return "";

            var inReaders = ReaderLaneMappingService.Instance
                .GetAll()
                .Where(m =>
                    m.LaneId == dbLaneId.Value &&
                    m.IsEnabled &&
                    m.Direction == "IN")
                .Select(m => "R" + m.ReaderNo)
                .ToList();

            return inReaders.Any()
                ? $"[VÀO: {string.Join(",", inReaders)}]"
                : "";
        }

        private string GetReaderMappingOut(int uiLaneIndex)
        {
            int? dbLaneId = GetDbLaneIdForUiIndex(uiLaneIndex);
            if (dbLaneId == null) return "";

            var outReaders = ReaderLaneMappingService.Instance
                .GetAll()
                .Where(m =>
                    m.LaneId == dbLaneId.Value &&
                    m.IsEnabled &&
                    m.Direction == "OUT")
                .Select(m => "R" + m.ReaderNo)
                .ToList();

            return outReaders.Any()
                ? $"[RA: {string.Join(",", outReaders)}]"
                : "";
        }

        private int _totalXeTrongBai = 0;
        private int _totalCapacity = 0;
        private string _soXeTrongBaiText = "Xe trong bãi: 0";
        public string SoXeTrongBai => _soXeTrongBaiText;

        private string _selectedSiteName = string.Empty;
        public string SelectedSiteName
        {
            get => _selectedSiteName;
            set { _selectedSiteName = value; OnPropertyChanged(nameof(SelectedSiteName)); }
        }

        private int _totalSiteXeTrongBai;
        public int TotalSiteXeTrongBai
        {
            get => _totalSiteXeTrongBai;
            set { _totalSiteXeTrongBai = value; OnPropertyChanged(nameof(TotalSiteXeTrongBai)); }
        }

        private int _totalSiteCapacity;
        public int TotalSiteCapacity
        {
            get => _totalSiteCapacity;
            set { _totalSiteCapacity = value; OnPropertyChanged(nameof(TotalSiteCapacity)); }
        }

        private string _totalSiteOccupancyText = "0";
        public string TotalSiteOccupancyText
        {
            get => _totalSiteOccupancyText;
            set { _totalSiteOccupancyText = value; OnPropertyChanged(nameof(TotalSiteOccupancyText)); }
        }

        private string _totalSiteOccupancyPercentageText = "0%";
        public string TotalSiteOccupancyPercentageText
        {
            get => _totalSiteOccupancyPercentageText;
            set { _totalSiteOccupancyPercentageText = value; OnPropertyChanged(nameof(TotalSiteOccupancyPercentageText)); }
        }

        public ObservableCollection<ZoneOccupancyInfo> ZoneOccupancies { get; } = new();


        private int _todayLuotVao = 0;
        public int TodayLuotVao
        {
            get => _todayLuotVao;
            set { _todayLuotVao = value; OnPropertyChanged(nameof(TodayLuotVao)); }
        }

        private int _todayLuotRa = 0;
        public int TodayLuotRa
        {
            get => _todayLuotRa;
            set { _todayLuotRa = value; OnPropertyChanged(nameof(TodayLuotRa)); }
        }

        private int _todayXeTrongBai = 0;
        public int TodayXeTrongBai
        {
            get => _todayXeTrongBai;
            set { _todayXeTrongBai = value; OnPropertyChanged(nameof(TodayXeTrongBai)); }
        }

        private int _todayXeTonQuaNgay = 0;
        public int TodayXeTonQuaNgay
        {
            get => _todayXeTonQuaNgay;
            set { _todayXeTonQuaNgay = value; OnPropertyChanged(nameof(TodayXeTonQuaNgay)); }
        }

        private double _todayDoanhThu = 0;
        public double TodayDoanhThu
        {
            get => _todayDoanhThu;
            set 
            { 
                _todayDoanhThu = value; 
                OnPropertyChanged(nameof(TodayDoanhThu)); 
                OnPropertyChanged(nameof(TodayDoanhThuText)); 
            }
        }

        public string TodayDoanhThuText => TodayDoanhThu.ToString("#,##0 VNĐ");

        private bool _isUserPopupOpen;
        public bool IsUserPopupOpen
        {
            get => _isUserPopupOpen;
            set { _isUserPopupOpen = value; OnPropertyChanged(nameof(IsUserPopupOpen)); }
        }

        private bool _isSidebarExpanded = false;
        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set
            {
                _isSidebarExpanded = value;
                OnPropertyChanged(nameof(IsSidebarExpanded));
            }
        }

        public string CurrentUserTen => QuanLyGiuXe.Models.CurrentUser.Ten ?? "Nhân viên";
        public string CurrentUserRole => QuanLyGiuXe.Models.CurrentUser.Role ?? "Người vận hành";
        public string CurrentUserUsername => QuanLyGiuXe.Models.CurrentUser.Username ?? "user";

        public async void UpdateVehicleCount()
        {
            try
            {
                if (!SitesList.Any())
                {
                    var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() => {
                        SitesList.Clear();
                        foreach (var s in sites) SitesList.Add(s);
                        if (SitesList.Any() && SelectedSite == null) SelectedSite = SitesList[0];
                    }));
                }

                if (SelectedSite == null) return;

                // Load stats
                var stats = await db.GetTodayStatsAsync(SelectedSite.Id);
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() => {
                    TodayLuotVao = stats.LuotVao;
                    TodayLuotRa = stats.LuotRa;
                    TodayDoanhThu = stats.DoanhThu;
                    TodayXeTrongBai = stats.XeTrongBai;
                    TodayXeTonQuaNgay = stats.XeTonQuaNgay;
                }));

                int count = await Task.Run(() => db.GetTotalXeTrongBaiCount());
                var zones = await ParkingTopologyService.Instance.GetZonesAsync();
                
                var siteZones = zones.Where(z => z.SiteId == SelectedSite.Id).ToList();
                var zoneInfos = new List<string>();
                var tempOccupancies = new List<ZoneOccupancyInfo>();
                int totalSiteCount = 0;
                int totalSiteCapacity = 0;

                foreach (var zone in siteZones)
                {
                    int zoneCount = await db.GetXeTrongBaiCountByZoneAsync(zone.Id);
                    totalSiteCount += zoneCount;
                    totalSiteCapacity += zone.MaxCapacity;
                    
                    // Format zone name (e.g. lowercase zone name) and count/capacity
                    string zoneDisplayName = zone.ZoneName;
                    if (zoneDisplayName.ToLower().StartsWith("zone ")) 
                        zoneDisplayName = zoneDisplayName.Substring(5);
                    
                    zoneInfos.Add($"{zoneDisplayName.ToLower()} ({zoneCount}/{zone.MaxCapacity})");
                    tempOccupancies.Add(new ZoneOccupancyInfo
                    {
                        ZoneName = zoneDisplayName,
                        Count = zoneCount,
                        MaxCapacity = zone.MaxCapacity
                    });
                }

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() => {
                    _totalXeTrongBai = count;
                    _totalCapacity = totalSiteCapacity;
                    
                    string totalText = _totalCapacity > 0 ? $"{totalSiteCount}/{_totalCapacity}" : $"{totalSiteCount}";
                    string zoneInfoStr = zoneInfos.Any() ? " │ " + string.Join(" │ ", zoneInfos) : "";
                    
                    _soXeTrongBaiText = $"Xe trong bãi ({SelectedSite.SiteName}): {totalText}{zoneInfoStr}";
                    OnPropertyChanged(nameof(SoXeTrongBai));

                    SelectedSiteName = SelectedSite?.SiteName ?? string.Empty;
                    TotalSiteXeTrongBai = totalSiteCount;
                    TotalSiteCapacity = totalSiteCapacity;
                    TotalSiteOccupancyText = totalText;
                    
                    double percentage = totalSiteCapacity > 0 ? (double)totalSiteCount / totalSiteCapacity * 100 : 0;
                    TotalSiteOccupancyPercentageText = $"{percentage:0.0}%";

                    ZoneOccupancies.Clear();
                    foreach (var occ in tempOccupancies)
                    {
                        ZoneOccupancies.Add(occ);
                    }
                }));
                
                // Refresh Lane Capacities
                await SyncLaneUIStateAsync(1);
                await SyncLaneUIStateAsync(2);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("UpdateCount", "MainViewModel", "Lỗi cập nhật số xe", ex);
            }
        }



        // ── Connection status indicators (Phase 2) ──────────────────────────────────

        private Services.Connection.ConnectionState _dbState = Services.Connection.ConnectionState.Disconnected;
        public Services.Connection.ConnectionState DbState
        {
            get => _dbState;
            set { _dbState = value; OnPropertyChanged(nameof(DbState)); }
        }

        private Services.Connection.ConnectionState _c3State = Services.Connection.ConnectionState.Disconnected;
        public Services.Connection.ConnectionState C3State
        {
            get => _c3State;
            set { _c3State = value; OnPropertyChanged(nameof(C3State)); }
        }


        private bool _isDbConnected;
        public bool IsDbConnected
        {
            get => _isDbConnected;
            set { _isDbConnected = value; OnPropertyChanged(nameof(IsDbConnected)); }
        }

        private bool _isC3Connected;
        public bool IsC3Connected
        {
            get => _isC3Connected;
            set { _isC3Connected = value; OnPropertyChanged(nameof(IsC3Connected)); }
        }

        private string _dbStatusLabel = "Cơ sở dữ liệu";
        public string DbStatusLabel
        {
            get => _dbStatusLabel;
            set { _dbStatusLabel = value; OnPropertyChanged(nameof(DbStatusLabel)); }
        }

        private string _c3StatusLabel = "C3-200";
        public string C3StatusLabel
        {
            get => _c3StatusLabel;
            set { _c3StatusLabel = value; OnPropertyChanged(nameof(C3StatusLabel)); }
        }





        // ── Ảnh biển số ─────────────────────────────────────────────────────────────

        private ImageSource? _anhBienSoVao;
        public ImageSource? AnhBienSoVao
        {
            get => _anhBienSoVao;
            set { _anhBienSoVao = value; OnPropertyChanged(nameof(AnhBienSoVao)); }
        }

        private ImageSource? _anhBienSoRaVao;
        public ImageSource? AnhBienSoRaVao
        {
            get => _anhBienSoRaVao;
            set { _anhBienSoRaVao = value; OnPropertyChanged(nameof(AnhBienSoRaVao)); }
        }

        private ImageSource? _anhBienSoRaRa;
        public ImageSource? AnhBienSoRaRa
        {
            get => _anhBienSoRaRa;
            set { _anhBienSoRaRa = value; OnPropertyChanged(nameof(AnhBienSoRaRa)); }
        }

        // ── Ảnh chụp từ 2 cam (snapshot khi xe vào/ra) ──────────────────────────

        private ImageSource? _anhChupVao1;
        public ImageSource? AnhChupVao1
        {
            get => _anhChupVao1;
            set { _anhChupVao1 = value; OnPropertyChanged(nameof(AnhChupVao1)); }
        }

        private ImageSource? _anhChupVao2;
        public ImageSource? AnhChupVao2
        {
            get => _anhChupVao2;
            set { _anhChupVao2 = value; OnPropertyChanged(nameof(AnhChupVao2)); }
        }

        private ImageSource? _anhChupRa1;
        public ImageSource? AnhChupRa1
        {
            get => _anhChupRa1;
            set { _anhChupRa1 = value; OnPropertyChanged(nameof(AnhChupRa1)); }
        }

        private ImageSource? _anhChupRa2;
        public ImageSource? AnhChupRa2
        {
            get => _anhChupRa2;
            set { _anhChupRa2 = value; OnPropertyChanged(nameof(AnhChupRa2)); }
        }

        // ── Helper methods for UI updates ─────────────────────────────────────

        public void UpdateLaneSnapshot(int lane, int cameraIndex, ImageSource source)
        {
            if (lane == 1) // Lane 1 (Left)
            {
                if (cameraIndex == 1) AnhChupVao1 = source;
                else if (cameraIndex == 2) AnhChupVao2 = source;
            }
            else if (lane == 2) // Lane 2 (Right)
            {
                if (cameraIndex == 1) AnhChupRa1 = source;
                else if (cameraIndex == 2) AnhChupRa2 = source;
            }
        }

        public static string FormatLicensePlate(string plate, string vehicleType = "")
        {
            if (string.IsNullOrEmpty(plate)) return string.Empty;
            
            // Loại bỏ ký tự đặc biệt, viết hoa
            string clean = System.Text.RegularExpressions.Regex.Replace(plate, @"[^A-Za-z0-9]", "").ToUpper();
            
            if (clean.Length < 6) return plate.ToUpper();

            string province = clean.Substring(0, 2);
            char seriesChar = clean[2];
            bool isCar = false;
            if (!string.IsNullOrEmpty(vehicleType))
            {
                isCar = string.Equals(vehicleType, "oto", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Nếu thuộc nhóm xe con thông dụng ở tất cả các tỉnh (A, B, C, D) hoặc F (Hà Nội, HCM và một số tỉnh lẻ cũ)
                if ("ABCDF".Contains(seriesChar))
                {
                    isCar = true;
                }
                // Nếu là các chữ cái khác (G, H, K, L...), chỉ được coi là ô tô nếu ở Hà Nội (30-33) hoặc TP.HCM (50-59)
                else if ("EGHIKLMNPRSTUVWXYZ".Contains(seriesChar))
                {
                    int provNum;
                    if (int.TryParse(province, out provNum))
                    {
                        if ((provNum >= 30 && provNum <= 33) || (provNum >= 50 && provNum <= 59))
                        {
                            isCar = true;
                        }
                    }
                }
            }

            // 1. Biển 9 ký tự (ví dụ: 29A123456 hoặc 29AA12345 hoặc 30LD12345)
            if (clean.Length == 9)
            {
                // Kiểm tra biển liên doanh / nước ngoài xe con (bắt đầu bằng 2 số, 2 chữ cái như LD, DA, NN, NG, QT...)
                string letters2 = clean.Substring(2, 2);
                bool isSpecialCar = false;
                string specialTypes = "LD,DA,NN,NG,QT,MD";
                if (char.IsLetter(clean[2]) && char.IsLetter(clean[3]))
                {
                    if (specialTypes.Contains(letters2))
                    {
                        isSpecialCar = true;
                    }
                }
                
                if (isSpecialCar)
                {
                    // Ô tô liên doanh: 30LD - 12345
                    return $"{clean.Substring(0, 4)} - {clean.Substring(4)}";
                }
                else
                {
                    // Xe máy: 29-A123456 hoặc 29-AA12345
                    return $"{clean.Substring(0, 2)}-{clean.Substring(2)}";
                }
            }

            // 2. Biển 8 ký tự (ví dụ: 29A11234 hoặc 30A12345 hoặc 73K99999)
            if (clean.Length == 8)
            {
                // Nếu 5 ký tự cuối là số -> Kiểm tra xem có phải Ô tô không
                bool last5AreDigits = true;
                for (int i = 3; i < 8; i++)
                {
                    if (!char.IsDigit(clean[i])) { last5AreDigits = false; break; }
                }

                if (last5AreDigits && char.IsLetter(clean[2]) && isCar)
                {
                    // Ô tô: 30A - 12345
                    return $"{clean.Substring(0, 3)} - {clean.Substring(3)}";
                }
                else
                {
                    // Xe máy: 29-A11234 hoặc 73-K99999
                    return $"{clean.Substring(0, 2)}-{clean.Substring(2)}";
                }
            }

            // 3. Biển 7 ký tự (ví dụ: 30A1234 - biển ô tô 4 số cũ)
            if (clean.Length == 7)
            {
                if (char.IsLetter(clean[2]) && isCar)
                {
                    // Ô tô 4 số: 30A - 1234
                    return $"{clean.Substring(0, 3)} - {clean.Substring(3)}";
                }
                else
                {
                    // Xe máy 4 số: 29-A1234
                    return $"{clean.Substring(0, 2)}-{clean.Substring(2)}";
                }
            }

            return clean;
        }

        public void SetLanePlate(int lane, string plate)
        {
            string formattedPlate = FormatLicensePlate(plate);
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                if (lane == 1) Lane1BienSo = formattedPlate;
                else if (lane == 2) Lane2BienSo = formattedPlate;
            }));
        }



        // ── Commands ──────────────────────────────────────────────────────────────

        public ICommand XeVaoCommand { get; }
        public ICommand XeRaCommand { get; }
        public ICommand Lane1ManualSendCommand { get; }
        public ICommand Lane2ManualSendCommand { get; }
        public ICommand XeChiTietCommand { get; }
        public ICommand TrangChuCommand { get; }
        public ICommand TimKiemCommand { get; }
        public ICommand LichSuCommand { get; }
        public ICommand DatabaseExplorerCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ToggleUserPopupCommand { get; }
        public ICommand EditProfileCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public ICommand RealtimeEventFeedCommand { get; }
        public ICommand DeploymentCenterCommand { get; }
        public ICommand BackupRestoreCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────────

        public MainViewModel()
        {
            var cfg = AppConfig.Load();
            WorkstationId = cfg.WorkstationId;
            UpdateLaneStatusColor(1, _lane1TrangThai);
            UpdateLaneStatusColor(2, _lane2TrangThai);

            DanhSachXe = new ObservableCollection<Xe>();
            DanhSachXe.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SoXeTrongBai));
            XeVaoCommand = new SecureCommand("OPEN_BARRIER", async _ => {
                int laneId = GetDbLaneIdForUiIndex(1) ?? 1;
                if (!Services.PermissionService.Instance.HasLaneAccess(CurrentUserContext.Instance.Id, laneId))
                {
                    MessageBox.Show("Không có quyền thao tác trên làn này!", "Lỗi phân quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                await ProcessActionAsync(1, laneId, IsLane1Inbound, LastScannedUID);
            });
            XeRaCommand = new SecureCommand("OPEN_BARRIER", async _ => {
                int laneId = GetDbLaneIdForUiIndex(2) ?? 2;
                if (!Services.PermissionService.Instance.HasLaneAccess(CurrentUserContext.Instance.Id, laneId))
                {
                    MessageBox.Show("Không có quyền thao tác trên làn này!", "Lỗi phân quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                await ProcessActionAsync(2, laneId, IsLane2Inbound, LastScannedUID);
            });
            XeChiTietCommand = new RelayCommand<Xe>(XeChiTiet);

            Lane1ManualSendCommand = new RelayCommand(async _ => {
                int laneId = GetDbLaneIdForUiIndex(1) ?? 1;
                await ProcessManualInputAsync(1, laneId, Lane1ManualInput);
                Lane1ManualInput = ""; // Clear input after sending
            });
            Lane2ManualSendCommand = new RelayCommand(async _ => {
                int laneId = GetDbLaneIdForUiIndex(2) ?? 2;
                await ProcessManualInputAsync(2, laneId, Lane2ManualInput);
                Lane2ManualInput = ""; // Clear input after sending
            });

            C3200Service.Instance.OnConnectionChanged += OnC3200ConnectionChanged;
            
            LaneRuntimeManager.Instance.OnLaneDirectionChanged += (laneId) => {
                try
                {
                    // Find UI lane index corresponding to this db lane
                    int uiLaneIndex = -1;
                    if (GetDbLaneIdForUiIndex(1) == laneId) uiLaneIndex = 1;
                    else if (GetDbLaneIdForUiIndex(2) == laneId) uiLaneIndex = 2;
                    
                    if (uiLaneIndex != -1)
                    {
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                await SyncLaneUIStateAsync(uiLaneIndex);
                            }
                            catch (Exception ex)
                            {
                                try { LoggingService.Instance.LogError("OnLaneDirectionChanged_Dispatch", "MainViewModel", "Failed to sync UI lane state", ex); } catch { }
                            }
                        }));
                    }
                }
                catch (Exception ex)
                {
                    try { LoggingService.Instance.LogError("OnLaneDirectionChanged", "MainViewModel", "Failed in OnLaneDirectionChanged event handler", ex); } catch { }
                }
            };
            _ = SyncLaneUIStateAsync(1);
            _ = SyncLaneUIStateAsync(2);
            
            CurrentView = new TrangChuViewModel();
            
            TrangChuCommand = new RelayCommand(_ => SetView(new TrangChuViewModel()));
            TimKiemCommand = new RelayCommand(_ => SetView(new TimKiemViewModel()));
            RealtimeEventFeedCommand = new RelayCommand(_ => SetView(new RealtimeEventFeedViewModel()));
            LichSuCommand = new RelayCommand(_ => SetView(new LichSuViewModel()));
            DatabaseExplorerCommand = new SecureCommand("DATABASE_EXPLORER", _ => SetView(new DatabaseExplorerViewModel()));
            DeploymentCenterCommand = new RelayCommand(_ => SetView(new SystemConfigDeploymentViewModel { ActiveTabIndex = 2 }));
            ToggleUserPopupCommand = new RelayCommand(_ => IsUserPopupOpen = !IsUserPopupOpen);
            EditProfileCommand = new RelayCommand(_ =>
            {
                try
                {
                    IsUserPopupOpen = false;

                    var window = new Views.UserProfileWindow();

                    window.ShowDialog();

                    // Refresh UI user info
                    RefreshCurrentUserInfo();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError(
                        "OpenProfileWindow",
                        "MainViewModel",
                        "Failed to open profile window",
                        ex);

                    MessageBox.Show(
                        "Không thể mở thông tin cá nhân",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });

            ChangePasswordCommand = new RelayCommand(_ =>
            {
                try
                {
                    IsUserPopupOpen = false;

                    var window = new Views.ChangePasswordWindow();

                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError(
                        "OpenChangePasswordWindow",
                        "MainViewModel",
                        "Failed to open change password window",
                        ex);

                    MessageBox.Show(
                        "Không thể mở đổi mật khẩu",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });

            BackupRestoreCommand = new SecureCommand("BACKUP_RESTORE", _ =>
            {
                try
                {
                    IsUserPopupOpen = false;
                    var window = new Views.BackupRestoreWindow();
                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("OpenBackupWindow", "MainViewModel", "Failed to open backup window", ex);
                    MessageBox.Show("Không thể mở cửa sổ Quản lý Backup", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
            LogoutCommand = new RelayCommand(_ => {
                IsUserPopupOpen = false;
                UnsubscribeEvents(); // Clean up this VM
                if (Application.Current is App app)
                {
                    app.PerformLogout();
                }
            });

            // Subscriptions    
            ConnectivityStateService.Instance.PropertyChanged += OnConnectivityChanged;

            // Kick off heavy initialization in the background
            Task.Run(async () => await InitializeAsync(cfg));
            
            RefreshSettings();
        }

        public async void RefreshSettings()
        {
            try
            {
                // Refresh reader mappings FIRST so that GetDbLaneIdForUiIndex reads the new configuration
                ReaderLaneMappingService.Instance.Load();

                // Invalidate EventBus topology cache so it reloads fresh data from DB/SQLite
                try
                {
                    EventBus.Instance.InvalidateTopologyCache();
                }
                catch { }

                var cfg = AppConfig.Load();
                var blue = (Application.Current?.Resources["APSBlueBrush"] as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Blue;
                var red = (Application.Current?.Resources["APSRedBrush"] as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Red;

                CalculateLaneVisibilities();

                // Lane UI state is now managed dynamically via LaneRuntimeManager and SyncLaneUIState
                await SyncLaneUIStateAsync(1);
                await SyncLaneUIStateAsync(2);

                // Notify visibility changes
                OnPropertyChanged(nameof(Lane1FeeVisibility));
                OnPropertyChanged(nameof(Lane2FeeVisibility));
                OnPropertyChanged(nameof(Lane1TimeVisibility));
                OnPropertyChanged(nameof(Lane2TimeVisibility));
                
                OnPropertyChanged(nameof(Lane1ReaderMappingIn));
                OnPropertyChanged(nameof(Lane1ReaderMappingOut));
                OnPropertyChanged(nameof(Lane1ReaderMappingEmpty));
                OnPropertyChanged(nameof(Lane2ReaderMappingIn));
                OnPropertyChanged(nameof(Lane2ReaderMappingOut));
                OnPropertyChanged(nameof(Lane2ReaderMappingEmpty));

                // Ensure SitesList is populated
                if (!SitesList.Any())
                {
                    var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        SitesList.Clear();
                        foreach (var s in sites) SitesList.Add(s);
                    }));
                }

                // Determine selected site based on saved controller IP
                var sitesAll = await ParkingTopologyService.Instance.GetSitesAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var activeController = controllers.FirstOrDefault(c => c.IpAddress == cfg.ZKTeco.IpAddress);
                ParkingSite activeSite = null;
                if (activeController != null)
                {
                    var activeGate = gates.FirstOrDefault(g => g.Id == activeController.GateId);
                    if (activeGate != null)
                    {
                        activeSite = sitesAll.FirstOrDefault(s => s.Id == activeGate.SiteId);
                    }
                }
                // Fallback to first site if none matched
                if (activeSite == null && sitesAll.Any())
                {
                    activeSite = sitesAll[0];
                }
                // Update SelectedSite on UI thread
                if (activeSite != null)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        SelectedSite = activeSite;
                    }));
                }
            }
            catch { }
        }

        private async Task InitializeAsync(AppConfig cfg)
        {
            try
            {
                // 1. ZKTeco/C3200 Init
                var zk = cfg.ZKTeco;
                C3200Service.Instance.Configure(
                    ip: zk.IpAddress, port: zk.TcpPort,
                    password: zk.Password, timeoutMs: zk.Timeout,
                    barrierDuration: zk.BarrierDuration);

                // ── Kiểm tra quyền sở hữu: chỉ máy có PcIp khớp mới được connect ──
                bool isC3Owner = true; // mặc định cho phép nếu không tìm được config
                try
                {
                    var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                    var matchedCtrl = allControllers
                        .FirstOrDefault(c => c.IsActive &&
                            c.IpAddress.Trim().Equals(zk.IpAddress.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (matchedCtrl != null && !string.IsNullOrWhiteSpace(matchedCtrl.PcIp))
                    {
                        isC3Owner = C3200Service.IsOwnerOfController(matchedCtrl.PcIp);
                        if (isC3Owner)
                        {
                            LoggingService.Instance.LogInfo("C3_OWNER", "MainViewModel",
                                $"Máy này là owner của controller {zk.IpAddress} (PcIp={matchedCtrl.PcIp}). Bắt đầu kết nối.");
                        }
                        else
                        {
                            var myIps = string.Join(", ", C3200Service.GetLocalIpAddresses());
                            LoggingService.Instance.LogInfo("C3_SKIP", "MainViewModel",
                                $"Controller {zk.IpAddress} thuộc về máy {matchedCtrl.PcIp}. " +
                                $"Máy này ({myIps}) sẽ không kết nối vào controller đó.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Nếu lỗi khi kiểm tra → vẫn cho phép connect (fail-safe)
                    LoggingService.Instance.LogError("C3_OWNER_CHECK_ERROR", "MainViewModel",
                        "Không kiểm tra được PcIp ownership, cho phép connect mặc định.", ex);
                }

                if (isC3Owner)
                    await C3200Service.Instance.ConnectAsync();

                // 2. Heavy data loading removed from startup (Load on demand)
                UpdateVehicleCount();



                // 4. Connection monitor: reset UI + restart loop (login lại / VM mới)
                await Application.Current.Dispatcher.InvokeAsync(ResetStatus);
                await StartConnectionCheck();

                // 5. Initialize Auto Reconnect (Phase 2)
                InitializeAutoReconnect(cfg);
                
                // 6. Khởi động Backup Scheduler
                Services.Backup.BackupScheduler.Instance.Start();
                
                // 7. Khởi động SQL Connectivity Monitoring (New Service)
                ConnectivityStateService.Instance.Start();
                
                // 8. Khởi động Auto Sync Engine (Phase 6.2)
                AutoSyncService.Instance.Start();

                // 8.5. Khởi động Database Sync Service (C# Bidirectional Sync)
                DatabaseSyncService.Instance.Start();

                // 9. Khởi động cấu hình làn và controller cục bộ
                await InitializeLocalLanesAsync();
                
                LoggingService.Instance.LogInfo("VMInit", "MainViewModel", "Async initialization complete");
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("VMInitError", "MainViewModel", "Async init failed", ex);
            }
        }

        private async Task InitializeLocalLanesAsync()
        {
            try
            {
                var activeLaneIds = ReaderLaneMappingService.Instance.GetActiveLaneIds();
                var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                var allBarriers = await ParkingTopologyService.Instance.GetBarriersAsync();

                // Connect to controllers that are now active for us
                foreach (var laneId in activeLaneIds)
                {
                    var barrier = allBarriers.FirstOrDefault(b => b.LaneId == laneId && b.IsActive);
                    if (barrier != null)
                    {
                        var ctrl = allControllers.FirstOrDefault(c => c.Id == barrier.ControllerId && c.IsActive);
                        if (ctrl != null)
                        {
                            C3200Service.Instance.Configure(
                                ip: ctrl.IpAddress, port: 4370,
                                password: "", timeoutMs: 4000,
                                barrierDuration: 5);

                            await C3200Service.Instance.ConnectToControllerAsync(ctrl.IpAddress);
                        }
                    }
                }

                // Disconnect controllers that are no longer active for us
                var activeIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var laneId in activeLaneIds)
                {
                    var barrier = allBarriers.FirstOrDefault(b => b.LaneId == laneId && b.IsActive);
                    if (barrier != null)
                    {
                        var ctrl = allControllers.FirstOrDefault(c => c.Id == barrier.ControllerId && c.IsActive);
                        if (ctrl != null) activeIps.Add(ctrl.IpAddress);
                    }
                }

                var connected = C3200Service.Instance.GetAllActiveConnections();
                foreach (var conn in connected)
                {
                    if (!activeIps.Contains(conn.IpAddress))
                    {
                        C3200Service.Instance.DisconnectController(conn.IpAddress);
                    }
                }

                // Trigger UI refresh of lane visibilities
                if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CalculateLaneVisibilities();
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("LOCAL_LANES", "InitializeLocalLanes", "Error handling local lanes initialization in MainViewModel", ex);
            }
        }

        private void InitializeAutoReconnect(AppConfig cfg)
        {
            var reconnectService = Services.Connection.AutoReconnectService.Instance;
            
            // Đăng ký DB và C3
            reconnectService.RegisterResource(new Services.Connection.DatabaseResource());
            reconnectService.RegisterResource(new Services.Connection.C3200Resource());
            
            // Đăng ký Cameras
            // Trong project này, mỗi View tự tạo CameraService. 
            // Ta sẽ dùng một instance chung cho Monitor AutoReconnect.
            var monitorCamService = new CameraService(); 
            reconnectService.RegisterResource(new Services.Connection.CameraResource("VaoToanCanh", monitorCamService));
            reconnectService.RegisterResource(new Services.Connection.CameraResource("VaoBienSo", monitorCamService));
            reconnectService.RegisterResource(new Services.Connection.CameraResource("RaToanCanh", monitorCamService));
            reconnectService.RegisterResource(new Services.Connection.CameraResource("RaBienSo", monitorCamService));

            // Lắng nghe thay đổi trạng thái để cập nhật UI
            Services.Connection.ConnectionStateService.Instance.PropertyChanged += (s, e) =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    var svc = Services.Connection.ConnectionStateService.Instance;
                    switch (e.PropertyName)
                    {
                        case "Database":
                            DbState = svc.GetState("Database");
                            IsDbConnected = DbState == Services.Connection.ConnectionState.Connected;
                            DbStatusLabel = DbState switch
                            {
                                Services.Connection.ConnectionState.Connected => "Cơ sở dữ liệu",
                                Services.Connection.ConnectionState.Reconnecting => "DB (Đang thử lại...)",
                                _ => "DB (Mất kết nối)"
                            };
                            break;
                        case "C3200":
                            C3State = svc.GetState("C3200");
                            IsC3Connected = C3State == Services.Connection.ConnectionState.Connected;
                            C3StatusLabel = C3State switch
                            {
                                Services.Connection.ConnectionState.Connected    => "C3-200: Kết nối thành công",
                                Services.Connection.ConnectionState.Reconnecting => "C3-200: Đang thử kết nối lại...",
                                _                                                => "C3-200: Mất kết nối"
                            };
                            break;
                    }
                }));
            };

            reconnectService.Start();
            Services.Connection.CameraDiagnosticsService.Instance.Start();
        }

        // ── Connection Monitor handler ─────────────────────────────────────────

        /// <summary>
        /// Đặt chỉ báo DB/C3 về màu vàng "Đang kiểm tra". Gọi trên UI thread (Dispatcher).
        /// </summary>
        public void ResetStatus()
        {
            DbStatusLabel = "Cơ sở dữ liệu — Đang kiểm tra";
            C3StatusLabel = "C3-200 — Đang kiểm tra";
            Services.Connection.ConnectionStateService.Instance.ResetState();
        }

        /// <summary>
        /// Đăng ký handler và hủy task monitor cũ (nếu có), chạy vòng kiểm tra mới.
        /// </summary>
        public async Task StartConnectionCheck()
        {
            ConnectionMonitorService.Instance.StatusChanged -= OnConnectionStatusChanged;
            ConnectionMonitorService.Instance.StatusChanged += OnConnectionStatusChanged;
            await Task.Run(() => ConnectionMonitorService.Instance.Restart()).ConfigureAwait(false);
        }

        private void OnConnectionStatusChanged(ConnectionStatus status)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                // UI cập nhật Label đã được chuyển sang ConnectionStateService PropertyChanged
            }));
        }



        public void SetView(object view)
        {
            if (CurrentView != null)
            {
                string oldTabName = CurrentView.GetType().Name switch
                {
                    "TrangChuViewModel" => "Bàn giám sát",
                    "TimKiemViewModel" => "Tìm kiếm xe",
                    "RealtimeEventFeedViewModel" => "Sự kiện trực tiếp",
                    "LichSuViewModel" => "Lịch sử xe",
                    "DatabaseExplorerViewModel" => "Khám phá CSDL mini",
                    "DashboardViewModel" => "Thống kê tổng quan",
                    "MonitoringDashboardViewModel" => "Giám sát bãi đỗ xe",
                    "ParkingTopologyViewModel" => "Cấu hình & Kiểm tra Sơ đồ",
                    "SystemConfigDeploymentViewModel" => "Cấu hình & Triển khai",
                    "PersonnelAndCardTabsViewModel" => "Nhân sự & Thẻ",
                    _ => CurrentView.GetType().Name
                };
                LoggingService.Instance.LogInfo("TAB_CLOSE", "UI", $"Đóng tab: {oldTabName}");
            }

            if (view != null)
            {
                string tabName = view.GetType().Name switch
                {
                    "TrangChuViewModel" => "Bàn giám sát",
                    "TimKiemViewModel" => "Tìm kiếm xe",
                    "RealtimeEventFeedViewModel" => "Sự kiện trực tiếp",
                    "LichSuViewModel" => "Lịch sử xe",
                    "DatabaseExplorerViewModel" => "Khám phá CSDL mini",
                    "DashboardViewModel" => "Thống kê tổng quan",
                    "MonitoringDashboardViewModel" => "Giám sát bãi đỗ xe",
                    "ParkingTopologyViewModel" => "Cấu hình & Kiểm tra Sơ đồ",
                    "SystemConfigDeploymentViewModel" => "Cấu hình & Triển khai",
                    "PersonnelAndCardTabsViewModel" => "Nhân sự & Thẻ",
                    _ => view.GetType().Name
                };
                LoggingService.Instance.LogInfo("TAB_OPEN", "UI", $"Chuyển sang tab: {tabName}");
            }
            CurrentView = view;
            OnPropertyChanged(nameof(CurrentView));
            if (view is ParkingTopologyViewModel topoVm)
            {
                topoVm.RefreshCommand.Execute(null);
            }
        }

        // LoadXeTrongBai removed - use UpdateVehicleCount for dashboard instead


        // ── Xe Vào / Ra ──────────────────────────────────────────────────────────

        public async Task ProcessScanFromReaderAsync(int readerNo, string uid)
        {
            var mapping = ReaderLaneMappingService.Instance.GetMappingByReader(readerNo);

            if (mapping == null || !mapping.IsEnabled)
            {
                LoggingService.Instance.LogWarning(
                    "ProcessScan",
                    "MainViewModel",
                    $"Reader {readerNo} is unmapped or disabled.");

                return;
            }

            int dbLaneId = mapping.LaneId;
            int uiLaneIndex = 1;
            if (GetDbLaneIdForUiIndex(2) == dbLaneId)
            {
                uiLaneIndex = 2;
            }

            var laneState = LaneRuntimeManager.Instance.GetLaneState(dbLaneId);

            if (laneState == null)
            {
                LoggingService.Instance.LogWarning(
                    "ProcessScan",
                    "MainViewModel",
                    $"Lane state not found for laneId={dbLaneId}");

                return;
            }

            if (laneState.CurrentDirection == "DISABLED" ||
                laneState.CurrentDirection == "MAINTENANCE")
            {
                SetLaneStatus(
                    uiLaneIndex,
                    $"❌ Làn đang bảo trì/vô hiệu hóa");

                return;
            }

            if (mapping.Direction != laneState.CurrentDirection)
            {
                SetLaneStatus(
                    uiLaneIndex,
                    $"❌ Sai luồng thẻ! Làn đang là {laneState.CurrentDirection}");

                return;
            }

            if (laneState.IsLocked)
            {
                SetLaneStatus(
                    uiLaneIndex,
                    "⚠ Làn đang bận xử lý xe khác!");

                return;
            }

            LaneRuntimeManager.Instance.LockLane(dbLaneId, uid);

            bool isInbound = mapping.Direction == "IN";

            await ProcessActionAsync(
                uiLaneIndex,
                dbLaneId,
                isInbound,
                uid);
        }

            public async Task ProcessActionAsync(int uiLaneIndex, int dbLaneId, bool isInbound, string uid)
            {
                DateTime rfidReceived = DateTime.Now;
                DateTime? validationCompleted = null;
                DateTime? lprCompleted = null;
                DateTime? barrierTriggered = null;

                // We always run LPR on captured frame at the moment of swipe, so preRecognized is not needed

                if (string.IsNullOrEmpty(uid))
                {
                    SetLaneStatus(uiLaneIndex, "❌ Vui lòng quét thẻ RFID!");
                    LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                    return;
                }

                string recognizedPlate = string.Empty;
                string detectedVehicleType = string.Empty;
                byte[]? plateCropBytes = null;
                Mat? plateRawFrame = null;
                Mat? fullFrame = null;

                try
                {
                    LoggingService.Instance.LogInfo("ProcessAction", "MainViewModel", $"UI_Lane={uiLaneIndex} DB_Lane={dbLaneId} In={isInbound} UID={uid}");

                    // Get lane config immediately at the start of scan
                    var laneConfig = await ParkingTopologyService.Instance.GetLaneByIdAsync(dbLaneId);
                    if (laneConfig == null)
                    {
                        SetLaneStatus(uiLaneIndex, "❌ Lỗi: Cấu hình làn không tồn tại!");
                        LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                        return;
                    }

                    // Capture snapshots immediately upon scanning
                    string direction = laneConfig.Direction ?? (isInbound ? "IN" : "OUT");
                    string plateCamKey = direction.ToUpper() == "IN" ? "Vao2" : "Ra2";
                    string overviewCamKey = direction.ToUpper() == "IN" ? "Vao1" : "Ra1";
                    try
                    {
                        var cfg = AppConfig.Load().Cameras;
                        var lc = cfg.LaneCameras?.FirstOrDefault(c => c.LaneId == dbLaneId);
                        if (lc != null)
                        {
                            if (!string.IsNullOrEmpty(lc.ToanCanh)) overviewCamKey = $"Lane_{dbLaneId}_ToanCanh";
                            if (!string.IsNullOrEmpty(lc.BienSo)) plateCamKey = $"Lane_{dbLaneId}_BienSo";
                        }
                    }
                    catch { }

                    plateRawFrame = CameraService.Instance.GetLatestFrame(plateCamKey);
                    fullFrame = CameraService.Instance.GetLatestFrame(overviewCamKey);

                    // Start LPR task in parallel immediately
                    Task<LprResult>? lprTask = null;
                    if (plateRawFrame != null && !plateRawFrame.Empty())
                    {
                        lprTask = PlateRecognitionService.Instance.RecognizePlateAsync(plateRawFrame, plateCamKey);
                    }

                    // Reset UI details immediately (Marshal to UI thread)
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        ResetLaneCardAndOwnerDetails(uiLaneIndex);
                        if (uiLaneIndex == 1)
                        {
                            Lane1UID = uid;
                            Lane1BienSo = "";
                            Lane1TrangThai = "⏳ Đang xử lý...";
                            Lane1ManualInput = uid;
                            Lane1ThoiGianVao = "";
                            Lane1ThoiGianTrongBai = "";
                            Lane1Tien = "";
                        }
                        else
                        {
                            Lane2UID = uid;
                            Lane2BienSo = "";
                            Lane2TrangThai = "⏳ Đang xử lý...";
                            Lane2ManualInput = uid;
                            Lane2ThoiGianVao = "";
                            Lane2ThoiGianTrongBai = "";
                            Lane2Tien = "";
                        }
                    });

                    // ─── 1. VALIDATE CARD ───
                    var card = await db.GetRFIDCardByUidAsync(uid);
                    if (card == null || card.Id == 0)
                    {
                        SetLaneStatus(uiLaneIndex, $"❌ Thẻ {uid} chưa đăng ký!");
                        LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                        return;
                    }

                    if (string.IsNullOrEmpty(card.UID) || card.LoaiVeId <= 0 || card.LoaiXeId <= 0)
                    {
                        SetLaneStatus(uiLaneIndex, $"❌ Thông tin thẻ {uid} chưa đầy đủ (loại vé, loại xe hoặc UID trống)!");
                        LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                        return;
                    }

                    if (!string.Equals(card.TrangThai, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        SetLaneStatus(uiLaneIndex, $"❌ Thẻ {uid} đang ở trạng thái không hoạt động ({card.TrangThai})!");
                        LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                        return;
                    }

                    // Load owner details on UI thread
                    Application.Current?.Dispatcher?.Invoke(() => LoadOwnerDetailsForLane(uiLaneIndex, card));

                    bool isMonthly = IsMonthlyTicket(card.LoaiVeId);

                    if (isMonthly)
                    {
                        // ─── CHECK EMPLOYEE INFO ───
                        if (!card.EmployeeId.HasValue)
                        {
                            SetLaneStatus(uiLaneIndex, $"❌ Thẻ {uid} chưa đăng ký thông tin nhân viên!");
                            SetLanePlate(uiLaneIndex, ""); // Clear auto-detected plate
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "NO_EMPLOYEE", uid, $"Card UID: {uid} has no employee info registered.", "MainViewModel");
                            LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                            return;
                        }
                    }

                    // Await LPR result early so we have the plate for access and duplicate checks
                    if (lprTask != null)
                    {
                        try
                        {
                            var lprResult = await lprTask;
                            recognizedPlate = lprResult.Plate;
                            plateCropBytes = lprResult.PlateCropBytes;
                            detectedVehicleType = lprResult.VehicleType;
                        }
                        catch (Exception lprEx)
                        {
                            LoggingService.Instance.LogError("LprError", "MainViewModel", $"Failed to run LPR on captured frame", lprEx);
                        }
                        lprCompleted = DateTime.Now;
                    }

                    // ─── 1.5. VALIDATE VEHICLE TYPE COMPATIBILITY ───
                    if (!string.IsNullOrEmpty(recognizedPlate) && !string.IsNullOrEmpty(detectedVehicleType))
                    {
                        var lxList = await db.GetLoaiXeAsync();
                        var cardLx = lxList.FirstOrDefault(x => x.Id == card.LoaiXeId);
                        if (cardLx != null && !string.IsNullOrEmpty(cardLx.TenLoai))
                        {
                            string cardLxName = cardLx.TenLoai.ToLower();
                            string detectedLx = detectedVehicleType.ToLower();

                            if (cardLxName.Contains("xe máy") && detectedLx == "oto")
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Lỗi: Xe ô tô nhưng quẹt thẻ xe máy!");
                                SetLanePlate(uiLaneIndex, recognizedPlate);
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }
                            else if (cardLxName.Contains("ô tô") && detectedLx == "xe_may")
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Lỗi: Xe máy nhưng quẹt thẻ ô tô!");
                                SetLanePlate(uiLaneIndex, recognizedPlate);
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }
                        }
                    }

                    // ─── 2. CHECK ACCESS ───
                
                    // Get active vehicle session in lot (once, async)
                    var xeTrongBai = await db.GetXeTrongBaiEntityByCardIdAsync(card.Id);

                    if (isInbound)
                    {
                        if (xeTrongBai != null)
                        {
                            SetLaneStatus(uiLaneIndex, "⚠ Thẻ này đang ở trong bãi!");
                            LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                            return;
                        }

                        if (!string.IsNullOrEmpty(recognizedPlate))
                        {
                            var xeTheoBienSo = await db.GetXeTrongBaiRecordByPlateAsync(recognizedPlate);
                            if (xeTheoBienSo != null)
                            {
                                SetLaneStatus(uiLaneIndex, $"⚠ Xe biển số {recognizedPlate} đang ở trong bãi!");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (xeTrongBai == null)
                        {
                            if (!string.IsNullOrEmpty(recognizedPlate))
                            {
                                var xeTheoBienSo = await db.GetXeTrongBaiRecordByPlateAsync(recognizedPlate);
                                if (xeTheoBienSo != null)
                                {
                                    var alternateCard = await db.GetRFIDCardByIdAsync(xeTheoBienSo.Value.CardId);
                                    if (alternateCard != null)
                                    {
                                        if (isMonthly && !IsMonthlyTicket(alternateCard.LoaiVeId))
                                        {
                                            SetLaneStatus(uiLaneIndex, $"❌ Lỗi: Xe vào bằng thẻ lượt {alternateCard.UID}, vui lòng quẹt thẻ lượt!");
                                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "CARD_MISMATCH_EXIT", uid, $"Monthly card {uid} swiped at exit, but vehicle entered with daily card {alternateCard.UID}", "MainViewModel");
                                            LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                            return;
                                        }
                                        else if (!isMonthly && IsMonthlyTicket(alternateCard.LoaiVeId))
                                        {
                                            SetLaneStatus(uiLaneIndex, $"❌ Lỗi: Xe vào bằng thẻ tháng {alternateCard.UID}, vui lòng quẹt thẻ tháng!");
                                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "CARD_MISMATCH_EXIT", uid, $"Daily card {uid} swiped at exit, but vehicle entered with monthly card {alternateCard.UID}", "MainViewModel");
                                            LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                            return;
                                        }
                                        else
                                        {
                                            SetLaneStatus(uiLaneIndex, $"❌ Lỗi: Xe đang ở trong bãi bằng thẻ {alternateCard.UID}!");
                                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "CARD_MISMATCH_EXIT", uid, $"Card {uid} swiped at exit, but vehicle is in lot under card {alternateCard.UID}", "MainViewModel");
                                            LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                            return;
                                        }
                                    }
                                }
                            }

                            SetLaneStatus(uiLaneIndex, "⚠ Không tìm thấy xe trong bãi");
                            LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                            return;
                        }
                    }

                    // ─── COMBINED RFID & LPR DECISION LOGIC ───
                    if (isMonthly)
                    {
                        if (isInbound)
                        {
                            string registeredPlate = card.BienSo ?? string.Empty;

                            if (string.IsNullOrEmpty(registeredPlate))
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Thẻ chưa đăng ký biển số!");
                                LoggingService.Instance.LogSecurity(
                                    "ACCESS_DENIED",
                                    "NO_REGISTERED_PLATE",
                                    uid,
                                    "RFID card has no registered plate",
                                    "MainViewModel"
                                );

                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            // BẮT BUỘC PHẢI CÓ LPR
                            if (string.IsNullOrEmpty(recognizedPlate))
                            {
                                SetLaneStatus(
                                    uiLaneIndex,
                                    "❌ Không nhận diện được biển số!"
                                );

                                LoggingService.Instance.LogSecurity(
                                    "ACCESS_DENIED",
                                    "LPR_EMPTY",
                                    uid,
                                    $"Cannot recognize plate. Registered={registeredPlate}",
                                    "MainViewModel"
                                );

                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            // SO SÁNH BIỂN SỐ ĐĂNG KÝ
                            if (!ComparePlates(recognizedPlate, registeredPlate))
                            {
                                SetLaneStatus(
                                    uiLaneIndex,
                                    $"❌ Sai biển số! Xe: {recognizedPlate} vs Đăng ký: {registeredPlate}"
                                );

                                LoggingService.Instance.LogSecurity(
                                    "ACCESS_DENIED",
                                    "LPR_MISMATCH",
                                    uid,
                                    $"Plate mismatch: Recognized={recognizedPlate}, Registered={registeredPlate}",
                                    "MainViewModel"
                                );

                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            // Match OK
                            SetLanePlate(uiLaneIndex, recognizedPlate);
                        }
                        else
                        {
                            // Monthly Ticket - Outbound (Exit Gate)
                            if (string.IsNullOrEmpty(recognizedPlate))
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Không nhận diện được biển số lúc ra!");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_EMPTY", uid, "Exit (monthly): no plate recognized by AI", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            string registeredPlate = card.BienSo ?? string.Empty;
                            if (string.IsNullOrEmpty(registeredPlate))
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Thẻ chưa đăng ký biển số!");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "NO_REGISTERED_PLATE", uid, "Exit (monthly): RFID card has no registered plate", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            if (!ComparePlates(recognizedPlate, registeredPlate))
                            {
                                SetLaneStatus(uiLaneIndex, $"❌ Sai biển số đăng ký! Xe: {recognizedPlate} vs Đăng ký: {registeredPlate}");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_MISMATCH", uid, $"Exit (monthly): Plate mismatch against registered: Recognized={recognizedPlate}, Registered={registeredPlate}", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            string entryPlate = xeTrongBai.BienSo ?? string.Empty;
                            if (string.IsNullOrEmpty(entryPlate))
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Không tìm thấy biển số lúc vào!");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "ENTRY_PLATE_EMPTY", uid, "Exit (monthly): entry plate is empty in database", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            if (!ComparePlates(recognizedPlate, entryPlate))
                            {
                                SetLaneStatus(uiLaneIndex, $"❌ Sai biển số lúc vào! Ra: {recognizedPlate} vs Vào: {entryPlate}");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_MISMATCH", uid, $"Exit (monthly): Plate mismatch: Recognized={recognizedPlate}, Entry={entryPlate}", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            // Match OK
                            SetLanePlate(uiLaneIndex, recognizedPlate);
                        }
                    }
                    else
                    {
                        // Daily Ticket
                        if (!isInbound)
                        {
                            if (string.IsNullOrEmpty(recognizedPlate))
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Không nhận diện được biển số lúc ra!");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_EMPTY", uid, "Exit: no plate recognized by AI", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            string entryPlate = xeTrongBai.BienSo ?? string.Empty;
                            if (string.IsNullOrEmpty(entryPlate))
                            {
                                // Legacy fallback: allow exit but log warning
                                LoggingService.Instance.LogSecurity("ACCESS_WARN", "ENTRY_PLATE_EMPTY", uid, "Exit: entry plate is empty in database, allowing exit without verification (legacy record)", "MainViewModel");
                            }
                            else
                            {
                                if (!ComparePlates(recognizedPlate, entryPlate))
                                {
                                    SetLaneStatus(uiLaneIndex, $"❌ Sai biển số lúc vào! Ra: {recognizedPlate} vs Vào: {entryPlate}");
                                    LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_MISMATCH", uid, $"Exit: Plate mismatch: Recognized={recognizedPlate}, Entry={entryPlate}", "MainViewModel");
                                    LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                    return;
                                }
                            }

                            // Match OK
                            SetLanePlate(uiLaneIndex, recognizedPlate);
                        }
                        else
                        {
                            // Daily Ticket - Inbound: enforce LPR
                            if (string.IsNullOrEmpty(recognizedPlate))
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Không nhận diện được biển số lúc vào!");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_EMPTY", uid, "Entry: no plate recognized by AI", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            if (!IsValidPlate(recognizedPlate))
                            {
                                SetLaneStatus(uiLaneIndex, $"❌ Biển số không hợp lệ: {recognizedPlate}");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_INVALID", uid, $"Entry: invalid plate format: {recognizedPlate}", "MainViewModel");
                                LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                return;
                            }

                            // Check if casual card is used for monthly plate
                            if (AppConfig.Load().ZKTeco.BlockDailyCardForMonthlyPlate)
                            {
                                var registeredCard = db.GetRFIDCardByNormalizedBienSo(recognizedPlate);
                                if (registeredCard != null && registeredCard.Id > 0 && IsMonthlyTicket(registeredCard.LoaiVeId))
                                {
                                    SetLaneStatus(uiLaneIndex, "❌ Lỗi: Biển số xe tháng, không được dùng thẻ lượt!");
                                    LoggingService.Instance.LogSecurity("ACCESS_DENIED", "MONTHLY_PLATE_WITH_DAILY_CARD", uid, $"Monthly plate {recognizedPlate} tried to enter with daily card UID {uid}", "MainViewModel");
                                    LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                                    return;
                                }
                            }

                            SetLanePlate(uiLaneIndex, recognizedPlate);
                        }
                    }

                    // Physical Access Check (using optimized overload that takes card)
                    var (allowed, reason) = await CardAccessPolicyService.Instance.ValidatePhysicalAccessAsync(card, dbLaneId);
                    if (!allowed)
                    {
                        SetLaneStatus(uiLaneIndex, $"❌ Từ chối: {reason}");
                        LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                        return;
                    }

                    // Vehicle Type Check
                    if (laneConfig.LoaiXeId.HasValue && card.LoaiXeId != laneConfig.LoaiXeId.Value)
                    {
                        string laneTypeName = !string.IsNullOrEmpty(laneConfig.LoaiXeName) ? laneConfig.LoaiXeName : $"ID={laneConfig.LoaiXeId.Value}";
                        SetLaneStatus(uiLaneIndex, $"❌ Sai loại xe! Làn này chỉ dành cho {laneTypeName}");
                        LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                        return;
                    }

                    validationCompleted = DateTime.Now;

                    // ─── 3. TRIGGER BARRIER ───
                    bool opened = await C3200Service.Instance.OpenBarrierAsync(uiLaneIndex);
                    barrierTriggered = DateTime.Now;

                    double totalMs = (DateTime.Now - rfidReceived).TotalMilliseconds;
                    double lprMs = lprCompleted.HasValue ? (lprCompleted.Value - rfidReceived).TotalMilliseconds : 0;
                    double valMs = validationCompleted.HasValue ? (validationCompleted.Value - rfidReceived).TotalMilliseconds : 0;
                    double barMs = barrierTriggered.HasValue ? (barrierTriggered.Value - rfidReceived).TotalMilliseconds : 0;

                    string perfMsg = $"[PERF] RFID Scan Processed: Received={rfidReceived:HH:mm:ss.fff}, LPR={(lprCompleted.HasValue ? lprCompleted.Value.ToString("HH:mm:ss.fff") : "N/A")} ({lprMs:N0}ms), Validation={validationCompleted:HH:mm:ss.fff} ({valMs:N0}ms), BarrierTrigger={barrierTriggered:HH:mm:ss.fff} ({barMs:N0}ms), Total={totalMs:N0}ms";
                    System.Diagnostics.Debug.WriteLine(perfMsg);
                    Console.WriteLine(perfMsg);
                    try { LoggingService.Instance.LogInfo("RFID_PERF_STATS", "MainViewModel", perfMsg); } catch { }

                    // Deep copy mats for background thread safety
                    Mat? plateRawFrameClone = plateRawFrame?.Clone();
                    Mat? fullFrameClone = fullFrame?.Clone();

                    // ─── 4. UPDATE UI & WRITE TO DB (Post-processing background task) ───
                    if (isInbound)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                int? siteId = null;
                                int? zoneId = null;
                                int? laneId = null;
                                try
                                {
                                    var topo = await ResolveTopologyForLaneAsync(dbLaneId);
                                    siteId = topo.SiteId;
                                    zoneId = topo.ZoneId;
                                    laneId = topo.LaneId;
                                    if (siteId.HasValue)
                                    {
                                        int? dynamicZoneId = await db.GetZoneBySiteAndVehicleTypeAsync(siteId.Value, card.LoaiXeId);
                                        if (dynamicZoneId.HasValue)
                                        {
                                            zoneId = dynamicZoneId.Value;
                                        }
                                    }
                                }
                                catch { }

                                string plate = recognizedPlate;
                                if (lprTask != null)
                                {
                                    try
                                    {
                                        var lprResult = await lprTask;
                                        plate = lprResult.Plate;
                                        plateCropBytes = lprResult.PlateCropBytes;
                                    }
                                    catch (Exception lprEx)
                                    {
                                        LoggingService.Instance.LogError("LprBackgroundError", "MainViewModel", $"Failed to await background LPR", lprEx);
                                    }
                                }

                                if (opened)
                                 {
                                     if (fullFrameClone != null && !fullFrameClone.Empty())
                                     {
                                         try
                                         {
                                             var img1 = MatToBitmapSource(fullFrameClone);
                                             if (img1 != null)
                                             {
                                                 Application.Current?.Dispatcher?.Invoke(() => UpdateLaneSnapshot(uiLaneIndex, 1, img1));
                                             }
                                         }
                                         catch (Exception snapEx) { LoggingService.Instance.LogError("Snap1Error", "MainViewModel", "Inbound overview snap show failed", snapEx); }
                                     }
                                     if (plateRawFrameClone != null && !plateRawFrameClone.Empty())
                                     {
                                         try
                                         {
                                             var img2 = MatToBitmapSource(plateRawFrameClone);
                                             if (img2 != null)
                                             {
                                                 Application.Current?.Dispatcher?.Invoke(() => UpdateLaneSnapshot(uiLaneIndex, 2, img2));
                                             }
                                         }
                                         catch (Exception snapEx) { LoggingService.Instance.LogError("Snap2Error", "MainViewModel", "Inbound plate snap show failed", snapEx); }
                                     }
                                 }
                                string? imageFolderPath = null;
                                try
                                {
                                    var timestamp = DateTime.Now;
                                    var (siteName, zoneName, gateName, laneName) = await ParkingImageService.ResolveTopologyNamesAsync(siteId, zoneId, laneId);
                                    imageFolderPath = await ParkingImageService.Instance.SaveSnapshotsAsync(
                                        siteName, zoneName, gateName, laneName,
                                        plate, "IN", timestamp,
                                        fullFrameClone,
                                        plateRawFrameClone,
                                        plateCropBytes);
                                }
                                catch (Exception imgEx)
                                {
                                    LoggingService.Instance.LogError("ImageSaveInbound", "MainViewModel", "Lỗi lưu ảnh vào", imgEx);
                                }

                                await db.ThemXeAsync(card.Id, string.IsNullOrEmpty(plate) ? null : plate, imageFolderPath ?? "", siteId, zoneId, laneId);

                                try
                                {
                                    string empName = string.Empty;
                                    Application.Current?.Dispatcher?.Invoke(() =>
                                    {
                                        empName = uiLaneIndex == 1 ? Lane1EmployeeName : Lane2EmployeeName;
                                    });
                                    LoggingService.Instance.LogInfo("RFID_SUCCESS", "MainViewModel",
                                        $"CardUID={card.UID}, EmployeeId={(card.EmployeeId.HasValue ? card.EmployeeId.Value.ToString() : "null")}, EmployeeName={(string.IsNullOrEmpty(empName) ? "null" : empName)}, Lane={laneId ?? 1}, Direction=IN, Timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                                }
                                catch { }

                                SetLanePlate(uiLaneIndex, plate);
                                SetLanePlateImages(uiLaneIndex, imageFolderPath, null);
                                SetLaneUID(uiLaneIndex, uid);
                                SetLaneStatus(uiLaneIndex, opened ? $"✅ Xe vào lúc {DateTime.Now:HH:mm}" : "⚠ Xe vào – barrier lỗi");

                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    DanhSachXe.Add(new Xe { BienSo = plate, ThoiGianVao = DateTime.Now });
                                
                                    var mainWin = Application.Current?.MainWindow as QuanLyGiuXe.MainWindow;
                                    if (mainWin != null && mainWin.AllowShowSession)
                                    {
                                        var session = new QuanLyGiuXe.Models.LichSuXe
                                        {
                                            CardId = card.Id,
                                            BienSo = plate,
                                            ThoiGianVao = DateTime.Now,
                                            ThoiGianRa = null,
                                            Tien = null,
                                            AnhVao = imageFolderPath ?? string.Empty
                                        };
                                        mainWin.ShowScanSessionForLane(uiLaneIndex, session);
                                    }
                                });

                                UpdateVehicleCount();
                            }
                            catch (Exception bgEx)
                            {
                                LoggingService.Instance.LogError("InboundBackgroundProcessing", "MainViewModel", "Lỗi hậu xử lý xe vào", bgEx);
                            }
                            finally
                            {
                                plateRawFrameClone?.Dispose();
                                fullFrameClone?.Dispose();
                            }
                        });
                    }
                    else
                    {
                        DateTime timeIn = xeTrongBai.ThoiGianVao ?? DateTime.Now;
                        var duration = DateTime.Now - timeIn;
                        double fee = db.TinhTien(card.LoaiXeId, card.LoaiVeId, timeIn, DateTime.Now);

                        int? entrySiteId = xeTrongBai.SiteId;
                        int? entryZoneId = xeTrongBai.ZoneId;
                        int? entryLaneId = xeTrongBai.EntryLaneId;
                        string? entryImageFolder = QuanLyGiuXe.Services.ParkingImageService.ResolveSharedFolderPath(xeTrongBai.AnhXe);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                int? siteId = null;
                                int? zoneId = null;
                                int? laneId = null;
                                try
                                {
                                    var topo = await ResolveTopologyForLaneAsync(dbLaneId);
                                    siteId = topo.SiteId;
                                    zoneId = topo.ZoneId;
                                    laneId = topo.LaneId;
                                    if (siteId.HasValue)
                                    {
                                        int? dynamicZoneId = await db.GetZoneBySiteAndVehicleTypeAsync(siteId.Value, card.LoaiXeId);
                                        if (dynamicZoneId.HasValue)
                                        {
                                            zoneId = dynamicZoneId.Value;
                                        }
                                    }
                                }
                                catch { }

                                string plate = recognizedPlate;
                                if (lprTask != null)
                                {
                                    try
                                    {
                                        var lprResult = await lprTask;
                                        plate = lprResult.Plate;
                                        plateCropBytes = lprResult.PlateCropBytes;
                                    }
                                    catch (Exception lprEx)
                                    {
                                        LoggingService.Instance.LogError("LprBackgroundError", "MainViewModel", $"Failed to await background LPR", lprEx);
                                    }
                                }

                                if (opened)
                                {
                                     if (fullFrameClone != null && !fullFrameClone.Empty())
                                     {
                                         try
                                         {
                                             var img1 = MatToBitmapSource(fullFrameClone);
                                             if (img1 != null)
                                             {
                                                 Application.Current?.Dispatcher?.Invoke(() => UpdateLaneSnapshot(uiLaneIndex, 1, img1));
                                             }
                                         }
                                         catch (Exception snapEx) { LoggingService.Instance.LogError("Snap1Error", "MainViewModel", "Outbound overview snap show failed", snapEx); }
                                     }

                                     bool showEntrySnap = false;
                                     try { showEntrySnap = AppConfig.Load().Cameras.ShowEntrySnapAtExit; } catch { }

                                     if (showEntrySnap && !string.IsNullOrEmpty(entryImageFolder) && System.IO.Directory.Exists(entryImageFolder))
                                     {
                                         try
                                         {
                                             string platePath = System.IO.Path.Combine(entryImageFolder, "plate_raw.jpg");
                                             if (!System.IO.File.Exists(platePath))
                                             {
                                                 platePath = System.IO.Path.Combine(entryImageFolder, "plate_crop.jpg");
                                             }
                                             if (System.IO.File.Exists(platePath))
                                             {
                                                 var img2 = LoadImageFromFile(platePath);
                                                 if (img2 != null)
                                                 {
                                                     Application.Current?.Dispatcher?.Invoke(() => UpdateLaneSnapshot(uiLaneIndex, 2, img2));
                                                 }
                                             }
                                         }
                                         catch (Exception snapEx) { LoggingService.Instance.LogError("Snap2Error", "MainViewModel", "Outbound entry plate snap show failed", snapEx); }
                                     }
                                     else
                                     {
                                         if (plateRawFrameClone != null && !plateRawFrameClone.Empty())
                                         {
                                             try
                                             {
                                                 var img2 = MatToBitmapSource(plateRawFrameClone);
                                                 if (img2 != null)
                                                 {
                                                     Application.Current?.Dispatcher?.Invoke(() => UpdateLaneSnapshot(uiLaneIndex, 2, img2));
                                                 }
                                             }
                                             catch (Exception snapEx) { LoggingService.Instance.LogError("Snap2Error", "MainViewModel", "Outbound plate snap show failed", snapEx); }
                                         }
                                     }
                                }
                                string? exitImageFolderPath = null;
                                try
                                {
                                    var timestamp = DateTime.Now;
                                    var (siteName, zoneName, gateName, laneName) = await ParkingImageService.ResolveTopologyNamesAsync(siteId ?? entrySiteId, zoneId ?? entryZoneId, laneId);
                                    exitImageFolderPath = await ParkingImageService.Instance.SaveSnapshotsAsync(
                                        siteName, zoneName, gateName, laneName,
                                        plate,
                                        "OUT", timestamp,
                                        fullFrameClone,
                                        plateRawFrameClone,
                                        plateCropBytes);
                                }
                                catch (Exception imgEx)
                                {
                                    LoggingService.Instance.LogError("ImageSaveOutbound", "MainViewModel", "Lỗi lưu ảnh ra", imgEx);
                                }

                                await db.UpdateXeRaByIdAsync(xeTrongBai.Id, DateTime.Now);
                                await db.LuuLichSuAsync(
                                    plate, timeIn, DateTime.Now, fee,
                                    exitImageFolderPath ?? "",
                                    uid,
                                    siteId: entrySiteId ?? siteId,
                                    zoneId: entryZoneId ?? zoneId,
                                    entryLaneId: entryLaneId,
                                    exitLaneId: laneId,
                                    anhVao: entryImageFolder);
                                await db.XoaXeByCardIdAsync(card.Id);

                                try
                                {
                                    string empName = string.Empty;
                                    Application.Current?.Dispatcher?.Invoke(() =>
                                    {
                                        empName = uiLaneIndex == 1 ? Lane1EmployeeName : Lane2EmployeeName;
                                    });
                                    LoggingService.Instance.LogInfo("RFID_SUCCESS", "MainViewModel",
                                        $"CardUID={card.UID}, EmployeeId={(card.EmployeeId.HasValue ? card.EmployeeId.Value.ToString() : "null")}, EmployeeName={(string.IsNullOrEmpty(empName) ? "null" : empName)}, Lane={laneId ?? 2}, Direction=OUT, Timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                                }
                                catch { }

                                string displayPlate = !string.IsNullOrEmpty(recognizedPlate) ? recognizedPlate : plate;
                                SetLanePlate(uiLaneIndex, displayPlate);
                                SetLanePlateImages(uiLaneIndex, entryImageFolder, exitImageFolderPath);
                                SetLaneUID(uiLaneIndex, uid);
                                SetLaneTimeInfo(uiLaneIndex, timeIn, duration);
                                SetLaneFee(uiLaneIndex, fee);
                                SetLaneStatus(uiLaneIndex, opened ? $"✅ Xe ra lúc {DateTime.Now:HH:mm}" : "⚠ Xe ra – barrier lỗi");

                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    var match = DanhSachXe.FirstOrDefault(x => x.BienSo == plate);
                                    if (match != null) DanhSachXe.Remove(match);

                                    var mainWin = Application.Current?.MainWindow as QuanLyGiuXe.MainWindow;
                                    if (mainWin != null && mainWin.AllowShowSession)
                                    {
                                        var session = new QuanLyGiuXe.Models.LichSuXe
                                        {
                                            CardId = card.Id,
                                            BienSo = plate,
                                            ThoiGianVao = timeIn,
                                            ThoiGianRa = DateTime.Now,
                                            Tien = fee,
                                            AnhVao = entryImageFolder ?? string.Empty,
                                            AnhRa = exitImageFolderPath ?? string.Empty
                                        };
                                        mainWin.ShowScanSessionForLane(uiLaneIndex, session);
                                    }
                                });

                                UpdateVehicleCount();
                            }
                            catch (Exception bgEx)
                            {
                                LoggingService.Instance.LogError("OutboundBackgroundProcessing", "MainViewModel", "Lỗi hậu xử lý xe ra", bgEx);
                            }
                            finally
                            {
                                plateRawFrameClone?.Dispose();
                                fullFrameClone?.Dispose();
                            }
                        });
                    }

                    LastScannedUID = string.Empty;

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                    });
                }
                catch (Exception ex)
                {
                    SetLaneStatus(uiLaneIndex, $"❌ Lỗi xử lý: {ex.Message}");
                    LoggingService.Instance.LogError("ProcessActionError", "MainViewModel", $"DB_Lane={dbLaneId}", ex);
                    LaneRuntimeManager.Instance.UnlockLane(dbLaneId);
                }
                finally
                {
                    fullFrame?.Dispose();
                    plateRawFrame?.Dispose();
                }
            }

        private async Task<bool> ProcessInboundAsync(
            int uiLaneIndex, RFIDCard card, string uid,
            int? siteId, int? zoneId, int? laneId,
            string recognizedPlate,
            byte[]? plateCropBytes = null,
            Mat? fullFrame = null,
            Mat? plateRawFrame = null)
        {
            var existingRec = db.GetXeTrongBaiRecordByCardId(card.Id);
            if (existingRec != null)
            {
                SetLaneStatus(uiLaneIndex, "⚠ Thẻ này đang ở trong bãi!");
                return false;
            }

            string plate = recognizedPlate;

            try
            {
                // ── Lưu ảnh theo topology trước khi ghi DB ──
                string? imageFolderPath = null;
                try
                {
                    var timestamp = DateTime.Now;
                    var (siteName, zoneName, gateName, laneName) =
                        await ParkingImageService.ResolveTopologyNamesAsync(siteId, zoneId, laneId);

                    string overviewCamKey = laneId.HasValue ? $"Lane_{laneId}_ToanCanh" : "Vao1";
                    imageFolderPath = await ParkingImageService.Instance.SaveSnapshotsAsync(
                        siteName, zoneName, gateName, laneName,
                        plate, "IN", timestamp,
                        fullFrame,
                        plateRawFrame,
                        plateCropBytes);
                }
                catch (Exception imgEx)
                {
                    LoggingService.Instance.LogError("ImageSaveInbound", "MainViewModel", "Lỗi lưu ảnh vào", imgEx);
                }

                await db.ThemXeAsync(card.Id, string.IsNullOrEmpty(plate) ? null : plate,
                    imageFolderPath ?? "", siteId, zoneId, laneId);

                try
                {
                    string empName = uiLaneIndex == 1 ? Lane1EmployeeName : Lane2EmployeeName;
                    LoggingService.Instance.LogInfo("RFID_SUCCESS", "MainViewModel",
                        $"CardUID={card.UID}, EmployeeId={(card.EmployeeId.HasValue ? card.EmployeeId.Value.ToString() : "null")}, EmployeeName={(string.IsNullOrEmpty(empName) ? "null" : empName)}, Lane={laneId ?? 1}, Direction=IN, Timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }
                catch { }

                SetLanePlate(uiLaneIndex, plate);
                SetLanePlateImages(uiLaneIndex, imageFolderPath, null);
                SetLaneUID(uiLaneIndex, uid);

                bool opened = await C3200Service.Instance.OpenBarrierAsync(uiLaneIndex);
                SetLaneStatus(uiLaneIndex, opened ? $"✅ Xe vào lúc {DateTime.Now:HH:mm}" : "⚠ Xe vào – barrier lỗi");

                if (opened)
                {
                      if (fullFrame != null && !fullFrame.Empty())
                      {
                          try
                          {
                              var img1 = MatToBitmapSource(fullFrame);
                              if (img1 != null)
                              {
                                  Application.Current?.Dispatcher?.Invoke(() => UpdateLaneSnapshot(uiLaneIndex, 1, img1));
                              }
                          }
                          catch (Exception snapEx) { LoggingService.Instance.LogError("Snap1Error", "MainViewModel", "Manual inbound overview snap show failed", snapEx); }
                      }
                      if (plateRawFrame != null && !plateRawFrame.Empty())
                      {
                          try
                          {
                              var img2 = MatToBitmapSource(plateRawFrame);
                              if (img2 != null)
                              {
                                  Application.Current?.Dispatcher?.Invoke(() => UpdateLaneSnapshot(uiLaneIndex, 2, img2));
                              }
                          }
                          catch (Exception snapEx) { LoggingService.Instance.LogError("Snap2Error", "MainViewModel", "Manual inbound plate snap show failed", snapEx); }
                      }
                }

                DanhSachXe.Add(new Xe { BienSo = plate, ThoiGianVao = DateTime.Now });
                try
                {
                    var session = new QuanLyGiuXe.Models.LichSuXe
                    {
                        CardId = card.Id,
                        BienSo = plate,
                        ThoiGianVao = DateTime.Now,
                        ThoiGianRa = null,
                        Tien = null,
                        AnhVao = imageFolderPath ?? string.Empty
                    };
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var mainWin = Application.Current?.MainWindow as QuanLyGiuXe.MainWindow;
                        if (mainWin != null && mainWin.AllowShowSession)
                        {
                            mainWin.ShowScanSessionForLane(uiLaneIndex, session);
                        }
                    });
                }
                catch { }

                return opened;
            }
            catch (Exception ex)
            {
                SetLaneStatus(uiLaneIndex, $"❌ Lỗi ghi DB: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessOutboundAsync(
            int uiLaneIndex, RFIDCard card, string uid,
            int? exitSiteId, int? exitZoneId, int? exitLaneId,
            string recognizedPlate,
            byte[]? plateCropBytes = null,
            Mat? fullFrame = null,
            Mat? plateRawFrame = null)
        {
            var rec = db.GetXeTrongBaiRecordByCardId(card.Id);
            if (rec == null)
            {
                SetLaneStatus(uiLaneIndex, "⚠ Không tìm thấy xe trong bãi");
                return false;
            }

            var (id, plate, timeIn) = rec.Value;
            var duration = DateTime.Now - timeIn;
            double fee = db.TinhTien(card.LoaiXeId, card.LoaiVeId, timeIn, DateTime.Now);

            var xeTrongBai = await db.GetXeTrongBaiEntityByCardIdAsync(card.Id);
            int? entrySiteId = xeTrongBai?.SiteId;
            int? entryZoneId = xeTrongBai?.ZoneId;
            int? entryLaneId = xeTrongBai?.EntryLaneId;
            string? entryImageFolder = QuanLyGiuXe.Services.ParkingImageService.ResolveSharedFolderPath(xeTrongBai?.AnhXe); // folder lưu ảnh lúc vào

            try
            {
                // ── Lưu ảnh ra theo topology ──
                string? exitImageFolderPath = null;
                try
                {
                    var timestamp = DateTime.Now;
                    var (siteName, zoneName, gateName, laneName) =
                        await ParkingImageService.ResolveTopologyNamesAsync(
                            exitSiteId ?? entrySiteId, exitZoneId ?? entryZoneId, exitLaneId);

                    exitImageFolderPath = await ParkingImageService.Instance.SaveSnapshotsAsync(
                        siteName, zoneName, gateName, laneName,
                        !string.IsNullOrEmpty(recognizedPlate) ? recognizedPlate : plate,
                        "OUT", timestamp,
                        fullFrame,
                        plateRawFrame,
                        plateCropBytes);
                }
                catch (Exception imgEx)
                {
                    LoggingService.Instance.LogError("ImageSaveOutbound", "MainViewModel", "Lỗi lưu ảnh ra", imgEx);
                }

                await db.UpdateXeRaByIdAsync(id, DateTime.Now);
                await db.LuuLichSuAsync(
                    recognizedPlate, timeIn, DateTime.Now, fee,
                    exitImageFolderPath ?? "",
                    uid,
                    siteId: entrySiteId ?? exitSiteId,
                    zoneId: entryZoneId ?? exitZoneId,
                    entryLaneId: entryLaneId,
                    exitLaneId: exitLaneId,
                    anhVao: entryImageFolder);
                await db.XoaXeByCardIdAsync(card.Id);

                try
                {
                    string empName = uiLaneIndex == 1 ? Lane1EmployeeName : Lane2EmployeeName;
                    LoggingService.Instance.LogInfo("RFID_SUCCESS", "MainViewModel",
                        $"CardUID={card.UID}, EmployeeId={(card.EmployeeId.HasValue ? card.EmployeeId.Value.ToString() : "null")}, EmployeeName={(string.IsNullOrEmpty(empName) ? "null" : empName)}, Lane={exitLaneId ?? 2}, Direction=OUT, Timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }
                catch { }

                string displayPlate = !string.IsNullOrEmpty(recognizedPlate) ? recognizedPlate : plate;
                SetLanePlate(uiLaneIndex, displayPlate);
                SetLanePlateImages(uiLaneIndex, entryImageFolder, exitImageFolderPath);
                SetLaneUID(uiLaneIndex, uid);
                SetLaneTimeInfo(uiLaneIndex, timeIn, duration);
                SetLaneFee(uiLaneIndex, fee);

                bool opened = await C3200Service.Instance.OpenBarrierAsync(uiLaneIndex);
                SetLaneStatus(uiLaneIndex, opened ? $"✅ Xe ra lúc {DateTime.Now:HH:mm}" : "⚠ Xe ra – barrier lỗi");

                if (opened)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        await CaptureAndShowSnapshotsAsync(uiLaneIndex, exitLaneId ?? 2, entryImageFolder);
                    });
                }

                var item = DanhSachXe.FirstOrDefault(x => x.BienSo == plate);
                if (item != null) DanhSachXe.Remove(item);

                try
                {
                    var session = new QuanLyGiuXe.Models.LichSuXe
                    {
                        CardId = card.Id,
                        BienSo = displayPlate,
                        ThoiGianVao = timeIn,
                        ThoiGianRa = DateTime.Now,
                        Tien = fee,
                        AnhVao = entryImageFolder ?? string.Empty,
                        AnhRa = exitImageFolderPath ?? string.Empty
                    };
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var mainWin = Application.Current?.MainWindow as QuanLyGiuXe.MainWindow;
                        if (mainWin != null && mainWin.AllowShowSession)
                        {
                            mainWin.ShowScanSessionForLane(uiLaneIndex, session);
                        }
                    });
                }
                catch { }
                return true;
            }
            catch (Exception ex)
            {
                SetLaneStatus(uiLaneIndex, $"❌ Lỗi ghi DB: {ex.Message}");
                return false;
            }
        }

        // ─── MANUAL INPUT FLOW CONTROL ───

        public async Task ProcessManualInputAsync(int uiLaneIndex, int dbLaneId, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                SetLaneStatus(uiLaneIndex, "❌ Vui lòng nhập UID hoặc biển số!");
                return;
            }

            input = input.Trim().ToUpper();

            // 1. Try to find card by UID first
            var card = db.GetRFIDCardByUid(input);
            if (card != null && card.Id > 0)
            {
                // Found card by UID! We run the standard flow
                await ProcessManualActionAsync(uiLaneIndex, dbLaneId, card, input);
                return;
            }

            // 2. If not found by UID, try to find card by registered license plate
            card = db.GetRFIDCardByBienSo(input);
            if (card != null && card.Id > 0)
            {
                // Found card by registered plate!
                // We run the flow using this card's UID and the plate as manualPlate
                await ProcessManualActionAsync(uiLaneIndex, dbLaneId, card, card.UID, manualPlate: input);
                return;
            }

            // 3. If still not found, check if it matches an active vehicle in the lot (for exit)
            var laneConfig = await ParkingTopologyService.Instance.GetLaneByIdAsync(dbLaneId);
            bool isInbound = laneConfig?.Direction == "IN";

            if (!isInbound)
            {
                // Exit direction: find active vehicle in lot by plate
                var rec = await db.GetXeTrongBaiRecordByPlateAsync(input);
                if (rec != null)
                {
                    // Found vehicle session! Get the card details associated with this session
                    card = await db.GetRFIDCardByIdAsync(rec.Value.CardId);
                    if (card != null && card.Id > 0)
                    {
                        await ProcessManualActionAsync(uiLaneIndex, dbLaneId, card, card.UID, manualPlate: input);
                        return;
                    }
                }
            }

            SetLaneStatus(uiLaneIndex, $"❌ Không tìm thấy thẻ/biển số: {input}!");
        }

        public async Task ProcessManualActionAsync(int uiLaneIndex, int dbLaneId, RFIDCard card, string uid, string manualPlate = "")
        {
            byte[]? plateCropBytes = null;
            Mat? plateRawFrame = null;
            Mat? fullFrame = null;

            try
            {
                // Display UID and temporary status immediately
                ResetLaneCardAndOwnerDetails(uiLaneIndex);
                LoadOwnerDetailsForLane(uiLaneIndex, card);

                if (card == null || card.Id == 0)
                {
                    SetLaneStatus(uiLaneIndex, $"❌ Thẻ {uid} chưa đăng ký!");
                    return;
                }

                if (string.IsNullOrEmpty(card.UID) || card.LoaiVeId <= 0 || card.LoaiXeId <= 0)
                {
                    SetLaneStatus(uiLaneIndex, $"❌ Thông tin thẻ {uid} chưa đầy đủ (loại vé, loại xe hoặc UID trống)!");
                    return;
                }

                if (!string.Equals(card.TrangThai, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    SetLaneStatus(uiLaneIndex, $"❌ Thẻ {uid} đang ở trạng thái không hoạt động ({card.TrangThai})!");
                    return;
                }

                // ─── CHECK EMPLOYEE INFO ───
                if (!card.EmployeeId.HasValue)
                {
                    SetLaneStatus(uiLaneIndex, $"❌ Thẻ {uid} chưa đăng ký thông tin nhân viên!");
                    SetLanePlate(uiLaneIndex, "");
                    LoggingService.Instance.LogSecurity("ACCESS_DENIED", "NO_EMPLOYEE", uid, $"Card UID: {uid} has no employee info registered (manual).", "MainViewModel");
                    return;
                }

                SetLaneUID(uiLaneIndex, uid);
                SetLanePlate(uiLaneIndex, string.IsNullOrEmpty(manualPlate) ? "" : manualPlate);
                SetLaneStatus(uiLaneIndex, "⏳ Đang xử lý...");
                
                if (uiLaneIndex == 1)
                {
                    Lane1ManualInput = uid;
                    Lane1ThoiGianVao = "";
                    Lane1ThoiGianTrongBai = "";
                    Lane1Tien = "";
                }
                else
                {
                    Lane2ManualInput = uid;
                    Lane2ThoiGianVao = "";
                    Lane2ThoiGianTrongBai = "";
                    Lane2Tien = "";
                }

                var laneConfig = await ParkingTopologyService.Instance.GetLaneByIdAsync(dbLaneId);
                if (laneConfig == null)
                {
                    SetLaneStatus(uiLaneIndex, "❌ Lỗi: Cấu hình làn không tồn tại!");
                    return;
                }

                bool isInbound = laneConfig.Direction == "IN";

                // Determine recognized plate
                // ─── FORCE LPR RECOGNITION ───
                string recognizedPlate = string.Empty;

                string lprCamKey2 = GetLicensePlateCameraKey(
                    dbLaneId,
                    laneConfig.Direction
                );

                try
                {
                    plateRawFrame = CameraService.Instance.GetLatestFrame(lprCamKey2);

                    if (plateRawFrame != null && !plateRawFrame.Empty())
                    {
                        var lprResult =
                            await PlateRecognitionService.Instance
                            .RecognizePlateAsync(plateRawFrame, lprCamKey2);

                        recognizedPlate = lprResult.Plate;
                        plateCropBytes = lprResult.PlateCropBytes;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError(
                        "ManualLprError",
                        "MainViewModel",
                        "LPR failed in manual process",
                        ex
                    );
                }


                // Không nhận diện được thì chặn
                if (string.IsNullOrEmpty(recognizedPlate))
                {
                    SetLaneStatus(
                        uiLaneIndex,
                        "❌ Không nhận diện được biển số!"
                    );

                    LoggingService.Instance.LogSecurity(
                        "ACCESS_DENIED",
                        "LPR_EMPTY",
                        uid,
                        "Manual flow: no plate detected",
                        "MainViewModel"
                    );

                    return;
                }

                // If we got a plate, update UI
                if (!string.IsNullOrEmpty(recognizedPlate))
                {
                    SetLanePlate(uiLaneIndex, recognizedPlate);
                }

                // ─── COMBINED RFID & LPR DECISION LOGIC ───
                bool isMonthly = IsMonthlyTicket(card.LoaiVeId);
                var xeTrongBai = db.GetXeTrongBaiRecordByCardId(card.Id);

                if (isInbound)
                {
                    if (xeTrongBai != null)
                    {
                        SetLaneStatus(uiLaneIndex, "⚠ Thẻ này đang ở trong bãi!");
                        return;
                    }

                    if (!string.IsNullOrEmpty(recognizedPlate))
                    {
                        var xeTheoBienSo = Task.Run(() => db.GetXeTrongBaiRecordByPlateAsync(recognizedPlate)).GetAwaiter().GetResult();
                        if (xeTheoBienSo != null)
                        {
                            SetLaneStatus(uiLaneIndex, $"⚠ Xe biển số {recognizedPlate} đang ở trong bãi!");
                            return;
                        }
                    }
                }
                else
                {
                    if (xeTrongBai == null)
                    {
                        if (!string.IsNullOrEmpty(recognizedPlate))
                        {
                            var xeTheoBienSo = await db.GetXeTrongBaiRecordByPlateAsync(recognizedPlate);
                            if (xeTheoBienSo != null)
                            {
                                var alternateCard = await db.GetRFIDCardByIdAsync(xeTheoBienSo.Value.CardId);
                                if (alternateCard != null)
                                {
                                    if (isMonthly && !IsMonthlyTicket(alternateCard.LoaiVeId))
                                    {
                                        SetLaneStatus(uiLaneIndex, $"❌ Lỗi: Xe vào bằng thẻ lượt {alternateCard.UID}, vui lòng quẹt thẻ lượt!");
                                        return;
                                    }
                                    else if (!isMonthly && IsMonthlyTicket(alternateCard.LoaiVeId))
                                    {
                                        SetLaneStatus(uiLaneIndex, $"❌ Lỗi: Xe vào bằng thẻ tháng {alternateCard.UID}, vui lòng quẹt thẻ tháng!");
                                        return;
                                    }
                                    else
                                    {
                                        SetLaneStatus(uiLaneIndex, $"❌ Lỗi: Xe đang ở trong bãi bằng thẻ {alternateCard.UID}!");
                                        return;
                                    }
                                }
                            }
                        }

                        SetLaneStatus(uiLaneIndex, "⚠ Không tìm thấy xe trong bãi");
                        return;
                    }
                }

                if (isMonthly)
                {
                    if (isInbound)
                    {
                        string registeredPlate = card.BienSo ?? string.Empty;
                        
                        if (string.IsNullOrEmpty(registeredPlate))
                        {
                            SetLaneStatus(uiLaneIndex, "❌ Thẻ tháng chưa đăng ký biển số!");
                            return;
                        }

                        if (string.IsNullOrEmpty(recognizedPlate))
                        {
                            SetLaneStatus(uiLaneIndex, "❌ Không nhận diện được biển số!");
                            return;
                        }

                        if (!ComparePlates(recognizedPlate, registeredPlate))
                        {
                            SetLaneStatus(uiLaneIndex, $"❌ Sai biển số! Xe: {recognizedPlate} vs Đăng ký: {registeredPlate}");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_MISMATCH", uid, $"Manual entry (monthly) mismatch: {recognizedPlate} vs {registeredPlate}", "MainViewModel");
                            return;
                        }
                    }
                    else
                    {
                        // Monthly - Outbound (Exit)
                        string entryPlate = xeTrongBai.Value.BienSo ?? string.Empty;

                        if (string.IsNullOrEmpty(recognizedPlate))
                        {
                            SetLaneStatus(uiLaneIndex, "❌ Không nhận diện được biển số lúc ra!");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_EMPTY", uid, "Manual exit (monthly): no plate recognized by AI", "MainViewModel");
                            return;
                        }

                        string registeredPlate = card.BienSo ?? string.Empty;
                        if (string.IsNullOrEmpty(registeredPlate))
                        {
                            SetLaneStatus(uiLaneIndex, "❌ Thẻ chưa đăng ký biển số!");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "NO_REGISTERED_PLATE", uid, "Manual exit (monthly): RFID card has no registered plate", "MainViewModel");
                            return;
                        }

                        if (!ComparePlates(recognizedPlate, registeredPlate))
                        {
                            SetLaneStatus(uiLaneIndex, $"❌ Sai biển số đăng ký! Xe: {recognizedPlate} vs Đăng ký: {registeredPlate}");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_MISMATCH", uid, $"Manual exit (monthly): Plate mismatch against registered: Recognized={recognizedPlate}, Registered={registeredPlate}", "MainViewModel");
                            return;
                        }

                        if (string.IsNullOrEmpty(entryPlate))
                        {
                            SetLaneStatus(uiLaneIndex, "❌ Không tìm thấy biển số lúc vào!");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "ENTRY_PLATE_EMPTY", uid, "Manual exit (monthly): entry plate is empty in database", "MainViewModel");
                            return;
                        }

                        if (!ComparePlates(recognizedPlate, entryPlate))
                        {
                            SetLaneStatus(uiLaneIndex, $"❌ Sai biển số lúc vào! Ra: {recognizedPlate} vs Vào: {entryPlate}");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_MISMATCH", uid, $"Manual exit (monthly): Plate mismatch: Recognized={recognizedPlate}, Entry={entryPlate}", "MainViewModel");
                            return;
                        }
                    }
                }
                else
                {
                    // Daily/Guest ticket
                    if (isInbound)
                    {
                        if (string.IsNullOrEmpty(recognizedPlate))
                        {
                            SetLaneStatus(uiLaneIndex, "❌ Không nhận diện được biển số lúc vào!");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_EMPTY", uid, "Manual entry: no plate recognized by AI", "MainViewModel");
                            return;
                        }

                        if (!IsValidPlate(recognizedPlate))
                        {
                            SetLaneStatus(uiLaneIndex, $"❌ Biển số không hợp lệ: {recognizedPlate}");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_INVALID", uid, $"Manual entry: invalid plate format: {recognizedPlate}", "MainViewModel");
                            return;
                        }

                        // Check if casual card is used for monthly plate
                        if (AppConfig.Load().ZKTeco.BlockDailyCardForMonthlyPlate)
                        {
                            var registeredCard = db.GetRFIDCardByNormalizedBienSo(recognizedPlate);
                            if (registeredCard != null && registeredCard.Id > 0 && IsMonthlyTicket(registeredCard.LoaiVeId))
                            {
                                SetLaneStatus(uiLaneIndex, "❌ Lỗi: Biển số xe tháng, không được dùng thẻ lượt!");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "MONTHLY_PLATE_WITH_DAILY_CARD", uid, $"Monthly plate {recognizedPlate} tried to enter manually with daily card UID {uid}", "MainViewModel");
                                return;
                            }
                        }
                    }
                    else
                    {
                        string entryPlate = xeTrongBai.Value.BienSo ?? string.Empty;

                        if (string.IsNullOrEmpty(recognizedPlate))
                        {
                            SetLaneStatus(uiLaneIndex, "❌ Không nhận diện được biển số lúc ra!");
                            LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_EMPTY", uid, "Manual exit: no plate recognized by AI", "MainViewModel");
                            return;
                        }

                        if (string.IsNullOrEmpty(entryPlate))
                        {
                            // Legacy fallback: allow exit but log warning
                            LoggingService.Instance.LogSecurity("ACCESS_WARN", "ENTRY_PLATE_EMPTY", uid, "Manual exit: entry plate is empty in database, allowing exit without verification (legacy record)", "MainViewModel");
                        }
                        else
                        {
                            if (!ComparePlates(recognizedPlate, entryPlate))
                            {
                                SetLaneStatus(uiLaneIndex, $"❌ Sai biển số lúc vào! Ra: {recognizedPlate} vs Vào: {entryPlate}");
                                LoggingService.Instance.LogSecurity("ACCESS_DENIED", "LPR_MISMATCH", uid, $"Manual exit: Plate mismatch: Recognized={recognizedPlate}, Entry={entryPlate}", "MainViewModel");
                                return;
                            }
                        }
                    }
                }

                // --- PHYSICAL ACCESS SECURITY CHECK (GROUP & SCHEDULE VALIDATION) ---
                var (allowed, reason) = await CardAccessPolicyService.Instance.ValidatePhysicalAccessAsync(uid, dbLaneId);
                if (!allowed)
                {
                    SetLaneStatus(uiLaneIndex, $"❌ Từ chối: {reason}");
                    return;
                }

                // --- LANE-VEHICLE TYPE VALIDATION ---
                if (laneConfig.LoaiXeId.HasValue)
                {
                    if (card.LoaiXeId != laneConfig.LoaiXeId.Value)
                    {
                        string laneTypeName = !string.IsNullOrEmpty(laneConfig.LoaiXeName) ? laneConfig.LoaiXeName : $"ID={laneConfig.LoaiXeId.Value}";
                        SetLaneStatus(uiLaneIndex, $"❌ Sai loại xe! Làn này chỉ dành cho {laneTypeName}");
                        return;
                    }
                }

                // Resolve topology
                var (siteId, zoneId, laneId, _, _, _, _, _) = await ResolveTopologyForLaneAsync(dbLaneId);

                // --- DYNAMIC ZONE RESOLUTION ---
                if (siteId.HasValue)
                {
                    int? dynamicZoneId = await db.GetZoneBySiteAndVehicleTypeAsync(siteId.Value, card.LoaiXeId);
                    if (dynamicZoneId.HasValue)
                    {
                        zoneId = dynamicZoneId.Value;
                    }
                }

                // Capture full overview frame for image saving
                string overviewCamKey = laneConfig.Direction?.ToUpper() == "OUT" ? "Ra1" : "Vao1";
                var lc2 = AppConfig.Load().Cameras?.LaneCameras?.FirstOrDefault(c => c.LaneId == dbLaneId);
                if (lc2 != null && !string.IsNullOrEmpty(lc2.ToanCanh)) overviewCamKey = $"Lane_{dbLaneId}_ToanCanh";
                fullFrame = CameraService.Instance.GetLatestFrame(overviewCamKey);

                if (isInbound)
                {
                    await ProcessInboundAsync(uiLaneIndex, card, uid, siteId, zoneId, laneId, recognizedPlate, plateCropBytes, fullFrame, plateRawFrame);
                }
                else
                {
                    await ProcessOutboundAsync(uiLaneIndex, card, uid, siteId, zoneId, laneId, recognizedPlate, plateCropBytes, fullFrame, plateRawFrame);
                }

                UpdateVehicleCount();
            }
            catch (Exception ex)
            {
                SetLaneStatus(uiLaneIndex, $"❌ Lỗi xử lý: {ex.Message}");
                LoggingService.Instance.LogError("ProcessManualActionError", "MainViewModel", $"DB_Lane={dbLaneId}", ex);
            }
            finally
            {
                fullFrame?.Dispose();
                plateRawFrame?.Dispose();
            }
        }

        // ─── LPR & TICKET TYPE HELPERS ───

        private string GetLicensePlateCameraKey(int laneId, string direction)
        {
            try
            {
                var cfg = AppConfig.Load().Cameras;
                bool hasDynamic = cfg.LaneCameras != null && cfg.LaneCameras.Any(lc => lc.LaneId == laneId && (!string.IsNullOrEmpty(lc.ToanCanh) || !string.IsNullOrEmpty(lc.BienSo)));
                if (hasDynamic)
                {
                    return $"Lane_{laneId}_BienSo";
                }
            }
            catch { }
            return direction?.ToUpper() == "IN" ? "Vao2" : "Ra2";
        }

        private bool IsMonthlyTicket(int loaiVeId)
        {
            if (loaiVeId <= 0) return false;
            try
            {
                var loaiVe = db.GetLoaiVe().FirstOrDefault(x => x.Id == loaiVeId);
                if (loaiVe == null) return false;
                if (loaiVe.CoTheGiaHan) return true;
                var name = (loaiVe.TenLoai ?? string.Empty).ToLowerInvariant();
                return name.Contains("thang") || name.Contains("tháng") || name.Contains("month");
            }
            catch
            {
                return false;
            }
        }

        public static bool ComparePlates(string plate1, string plate2)
        {
            if (string.IsNullOrEmpty(plate1) || string.IsNullOrEmpty(plate2))
                return false;

            string p1 = NormalizePlate(plate1);
            string p2 = NormalizePlate(plate2);

            return p1 == p2;
        }

            private static string CorrectSpecialPlateOcrErrors(string normalized)
        {
            // Tự động sửa lỗi chữ cái đọc nhầm thành số cho biển chứa NN, NG, QT (trừ cụm NN, NG, QT ra)
            foreach (var marker in new[] { "NN", "NG", "QT" })
            {
                if (normalized.Contains(marker))
                {
                    string temp = normalized.Replace(marker, "#");
                    char[] chars = temp.ToCharArray();
                    for (int i = 0; i < chars.Length; i++)
                    {
                        if (char.IsLetter(chars[i]) && chars[i] != '#')
                        {
                            chars[i] = chars[i] switch
                            {
                                'B' => '8',
                                'D' => '0',
                                'O' => '0',
                                'I' => '1',
                                'L' => '1',
                                'T' => '1',
                                'Z' => '2',
                                'S' => '5',
                                'G' => '6',
                                'A' => '4',
                                'Q' => '0',
                                'U' => '0',
                                _ => chars[i]
                            };
                        }
                    }
                    return new string(chars).Replace("#", marker);
                }
            }

            // Sửa lỗi AI nhận diện nhầm số thứ 3 thành chữ cái ở biển nước ngoài đặc biệt kết thúc bằng N:
            if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^\d{2}[A-Z]\d{5}(NN|NG|QT|N)$"))
            {
                char[] chars = normalized.ToCharArray();
                char badChar = chars[2];
                char corrected = badChar switch
                {
                    'G' => '6',
                    'B' => '8',
                    'O' => '0',
                    'D' => '0',
                    'Q' => '0',
                    'I' => '1',
                    'L' => '1',
                    'S' => '5',
                    'Z' => '2',
                    'T' => '7',
                    _ => badChar
                };
                chars[2] = corrected;
                return new string(chars);
            }
            return normalized;
        }

        private static string NormalizePlate(string plate)
        {
            if (string.IsNullOrEmpty(plate)) return string.Empty;
            string normalized = new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
            return CorrectSpecialPlateOcrErrors(normalized);
        }

        public static bool IsValidPlate(string plate)
        {
            if (string.IsNullOrEmpty(plate)) return false;

            string normalized = NormalizePlate(plate); // Sử dụng hàm đã sửa lỗi OCR

            // Hỗ trợ biển ngoại giao / nước ngoài đăng ký tại VN (chứa cụm NN, NG, QT)
            if (normalized.Contains("NN") || normalized.Contains("NG") || normalized.Contains("QT"))
            {
                // Đối với biển chứa NN, NG, QT: Chỉ cần có độ dài hợp lý từ 4 đến 15 ký tự là cho phép qua
                return normalized.Length >= 4 && normalized.Length <= 15;
            }

            // Hỗ trợ biển nước ngoài khác kết thúc bằng chữ cái
            bool isDiplomatOrForeigner = (normalized.Length > 0 && char.IsLetter(normalized[normalized.Length - 1]));

            // Kiểm tra xem có bắt đầu giống định dạng biển số VN tiêu chuẩn (2 số + 1 chữ cái) hay không
            // Nếu là biển ngoại giao/nước ngoài, ta bỏ qua để tránh kiểm tra nghiêm ngặt của biển dân sự dân dụng
            bool isVnFormatPrefix = !isDiplomatOrForeigner && System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^\d{2}[A-Z]");

            if (isVnFormatPrefix)
            {
                // Strict Vietnamese format check: 2 digits + (2 letters OR 1 letter + optional 1 digit) + 4-5 digits
                var vnRegex = new System.Text.RegularExpressions.Regex(@"^\d{2}([A-Z]{2}|[A-Z]\d?)\d{4,5}$");
                return vnRegex.IsMatch(normalized);
            }
            else
            {
                // Validate theo chuẩn nước ngoài / quân đội / ngoại giao / đặc biệt khác
                // Cấu hình thông thoáng hơn (độ dài từ 4-15, tối thiểu 1 chữ cái, 2 chữ số)
                if (normalized.Length >= 4 && normalized.Length <= 15)
                {
                    int letterCount = normalized.Count(char.IsLetter);
                    int digitCount = normalized.Count(char.IsDigit);
                    return letterCount >= 1 && digitCount >= 2;
                }
            }

            return false;
        }

        private static int GetLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        // ── UI Helper Methods (Lane Aware) ─────────────────────────────────────

        private void SetLaneStatus(int lane, string msg)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                if (lane == 1) Lane1TrangThai = msg;
                else Lane2TrangThai = msg;

                if (!string.IsNullOrEmpty(msg) && (msg.Contains("✅") || msg.Contains("❌") || msg.Contains("⚠") || msg.Contains("Từ chối")))
                {
                    ToastType toastType = ToastType.Success;
                    string icon = "ℹ";
                    System.Windows.Media.Brush overlayColor = null;

                    if (msg.Contains("❌"))
                    {
                        toastType = ToastType.Error;
                        icon = "❌";
                        overlayColor = Application.Current?.Resources["DangerBrush"] as System.Windows.Media.Brush;
                        if (overlayColor == null) overlayColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                    }
                    else if (msg.Contains("⚠") || msg.Contains("Từ chối"))
                    {
                        toastType = ToastType.Warning;
                        icon = "⚠";
                        overlayColor = Application.Current?.Resources["WarningBrush"] as System.Windows.Media.Brush;
                        if (overlayColor == null) overlayColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
                    }
                    else
                    {
                        toastType = ToastType.Success;
                        icon = "✅";
                        overlayColor = Application.Current?.Resources["SuccessBrush"] as System.Windows.Media.Brush;
                        if (overlayColor == null) overlayColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                    }

                    System.Windows.Media.Brush highlightBg = null;
                    System.Windows.Media.Brush highlightBorder = overlayColor;

                    // Create a soft tint for the background (10% opacity)
                    if (msg.Contains("❌"))
                    {
                        highlightBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(26, 239, 68, 68)); // 10% Alpha Red
                    }
                    else if (msg.Contains("⚠") || msg.Contains("Từ chối"))
                    {
                        highlightBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(26, 245, 158, 11)); // 10% Alpha Orange
                    }
                    else
                    {
                        highlightBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(26, 16, 185, 129)); // 10% Alpha Green
                    }

                    // Clean emoji from message text for overlay presentation
                    string displayMsg = msg.Replace("✅", "").Replace("❌", "").Replace("⚠", "").Trim();

                    if (lane == 1)
                    {
                        Lane1NotificationMessage = displayMsg;
                        Lane1NotificationIcon = icon;
                        Lane1NotificationColor = overlayColor;
                        IsLane1NotificationVisible = true;
                        Lane1InfoBackground = highlightBg;
                        Lane1InfoBorderBrush = highlightBorder;
                    }
                    else
                    {
                        Lane2NotificationMessage = displayMsg;
                        Lane2NotificationIcon = icon;
                        Lane2NotificationColor = overlayColor;
                        IsLane2NotificationVisible = true;
                        Lane2InfoBackground = highlightBg;
                        Lane2InfoBorderBrush = highlightBorder;
                    }

                    // Auto reset status back to default (Chờ xe...) after 4 seconds
                    ResetLaneStatusAfterDelay(lane, 4000);
                }
            }));
        }

        private void ResetLaneStatusAfterDelay(int uiLaneIndex, int delayMs)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delayMs);
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // Hide overlay and reset highlights
                    if (uiLaneIndex == 1)
                    {
                        IsLane1NotificationVisible = false;
                        Lane1InfoBackground = GetDefaultInfoBackground();
                        Lane1InfoBorderBrush = System.Windows.Media.Brushes.Transparent;
                    }
                    else
                    {
                        IsLane2NotificationVisible = false;
                        Lane2InfoBackground = GetDefaultInfoBackground();
                        Lane2InfoBorderBrush = System.Windows.Media.Brushes.Transparent;
                    }

                    int? dbLaneId = GetDbLaneIdForUiIndex(uiLaneIndex);
                    if (!dbLaneId.HasValue) return;

                    var laneState = LaneRuntimeManager.Instance.GetLaneState(dbLaneId.Value);
                    if (laneState == null) return;

                    string defaultStatus = "Chờ xe...";
                    if (laneState.CurrentDirection == "DISABLED")
                    {
                        defaultStatus = "Làn đã vô hiệu hóa";
                    }
                    else if (laneState.CurrentDirection == "MAINTENANCE")
                    {
                        defaultStatus = "Làn đang bảo trì";
                    }

                    if (uiLaneIndex == 1)
                    {
                        if (Lane1TrangThai != null && (Lane1TrangThai.Contains("✅") || Lane1TrangThai.Contains("❌") || Lane1TrangThai.Contains("⚠") || Lane1TrangThai.Contains("Từ chối")))
                        {
                            Lane1TrangThai = defaultStatus;
                        }
                    }
                    else
                    {
                        if (Lane2TrangThai != null && (Lane2TrangThai.Contains("✅") || Lane2TrangThai.Contains("❌") || Lane2TrangThai.Contains("⚠") || Lane2TrangThai.Contains("Từ chối")))
                        {
                            Lane2TrangThai = defaultStatus;
                        }
                    }
                });
            });
        }

        private void UpdateLaneStatusColor(int lane, string status)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                System.Windows.Media.Brush brush = null;
                if (string.IsNullOrEmpty(status))
                {
                    brush = Application.Current?.Resources["SuccessBrush"] as System.Windows.Media.Brush;
                }
                else if (status.Contains("❌") || status.Contains("Lỗi") || status.Contains("Làn đang bảo trì"))
                {
                    brush = Application.Current?.Resources["DangerBrush"] as System.Windows.Media.Brush;
                }
                else if (status.Contains("⚠") || status.Contains("Cảnh báo") || status.Contains("bảo trì"))
                {
                    brush = Application.Current?.Resources["WarningBrush"] as System.Windows.Media.Brush;
                }
                else if (status.Contains("vô hiệu hóa") || status.Contains("Vô hiệu hóa"))
                {
                    brush = Application.Current?.Resources["TextSecondaryBrush"] as System.Windows.Media.Brush;
                }
                else
                {
                    brush = Application.Current?.Resources["SuccessBrush"] as System.Windows.Media.Brush;
                }

                if (brush == null)
                {
                    if (status != null && (status.Contains("❌") || status.Contains("Lỗi")))
                        brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                    else if (status != null && (status.Contains("⚠") || status.Contains("bảo trì")))
                        brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                    else if (status != null && status.Contains("vô hiệu hóa"))
                        brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158));
                    else
                        brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }

                if (lane == 1) Lane1StatusColor = brush;
                else Lane2StatusColor = brush;
            });
        }

        private void SetLaneUID(int lane, string uid)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                if (lane == 1) Lane1UID = uid;
                else Lane2UID = uid;
            }));
        }

        private void SetLaneTimeInfo(int lane, DateTime timeIn, TimeSpan duration)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                string vaoStr = $"Vào: {timeIn:HH:mm} │ {duration.Hours}h{duration.Minutes:D2}m";
                string trongStr = $"Thời gian trong bãi: {duration.Days}d {duration.Hours}h{duration.Minutes:D2}m";

                if (lane == 1)
                {
                    Lane1ThoiGianVao = vaoStr;
                    Lane1ThoiGianTrongBai = trongStr;
                }
                else
                {
                    Lane2ThoiGianVao = vaoStr;
                    Lane2ThoiGianTrongBai = trongStr;
                }
            }));
        }

        private void SetLaneFee(int lane, double fee)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                string feeStr = $"💰 {fee:N0} VNĐ";
                if (lane == 1) Lane1Tien = feeStr;
                else Lane2Tien = feeStr;
            }));
        }

        private async Task CaptureAndShowSnapshotsAsync(int uiLaneIndex, int dbLaneId, string? entryFolder = null)
        {
            try
            {
                var lane = await ParkingTopologyService.Instance.GetLaneByIdAsync(dbLaneId);
                if (lane == null) return;

                string direction = lane.Direction ?? "IN";
                string camKey1 = direction.ToUpper() == "IN" ? "Vao1" : "Ra1"; // Overview
                string camKey2 = direction.ToUpper() == "IN" ? "Vao2" : "Ra2"; // Plate
                
                try
                {
                    var cfg = AppConfig.Load().Cameras;
                    var lc = cfg.LaneCameras?.FirstOrDefault(c => c.LaneId == dbLaneId);
                    if (lc != null)
                    {
                        if (!string.IsNullOrEmpty(lc.ToanCanh)) camKey1 = $"Lane_{dbLaneId}_ToanCanh";
                        if (!string.IsNullOrEmpty(lc.BienSo)) camKey2 = $"Lane_{dbLaneId}_BienSo";
                    }
                }
                catch { }

                // Capture Overview Camera
                try
                {
                    using (var mat1 = CameraService.Instance.GetLatestFrame(camKey1))
                    {
                        if (mat1 != null && !mat1.Empty())
                        {
                            var img1 = MatToBitmapSource(mat1);
                            if (img1 != null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    UpdateLaneSnapshot(uiLaneIndex, 1, img1);
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("CaptureSnapshotError", "MainViewModel", $"Failed to capture Overview camera {camKey1}", ex);
                }

                // Capture Plate Camera
                bool showEntrySnap = false;
                try { showEntrySnap = AppConfig.Load().Cameras.ShowEntrySnapAtExit; } catch { }

                if (showEntrySnap && uiLaneIndex == 2 && !string.IsNullOrEmpty(entryFolder) && System.IO.Directory.Exists(entryFolder))
                {
                    try
                    {
                        string platePath = System.IO.Path.Combine(entryFolder, "plate_raw.jpg");
                        if (!System.IO.File.Exists(platePath))
                        {
                            platePath = System.IO.Path.Combine(entryFolder, "plate_crop.jpg");
                        }
                        if (System.IO.File.Exists(platePath))
                        {
                            var img2 = LoadImageFromFile(platePath);
                            if (img2 != null)
                            {
                                Application.Current?.Dispatcher?.Invoke(() =>
                                {
                                    UpdateLaneSnapshot(uiLaneIndex, 2, img2);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("LoadEntryPlateError", "MainViewModel", "Failed to load entry plate snapshot", ex);
                    }
                }
                else
                {
                    try
                    {
                        using (var mat2 = CameraService.Instance.GetLatestFrame(camKey2))
                        {
                            if (mat2 != null && !mat2.Empty())
                            {
                                var img2 = MatToBitmapSource(mat2);
                                if (img2 != null)
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        UpdateLaneSnapshot(uiLaneIndex, 2, img2);
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError("CaptureSnapshotError", "MainViewModel", $"Failed to capture Plate camera {camKey2}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("CaptureAndShowSnapshotsError", "MainViewModel", $"Failed for UI Lane {uiLaneIndex}", ex);
            }
        }

        // ── Tìm kiếm / Chi tiết ──────────────────────────────────────────────────

        private async void TimKiemXe()
        {
            try
            {
                var keyword = TuKhoaTimKiem?.Trim().ToLower();
                var source = await Task.Run(() => 
                {
                    var data = db.LayXeTrongBai().AsEnumerable();
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        data = data.Where(r => r["BienSo"].ToString()!.ToLower().Contains(keyword));
                    }
                    return data.ToList();
                });

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    DanhSachXe.Clear();
                    foreach (var row in source)
                    {
                        DanhSachXe.Add(new Xe
                        {
                            BienSo = row["BienSo"].ToString()!,
                            ThoiGianVao = Convert.ToDateTime(row["ThoiGianVao"])
                        });
                    }
                }));
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("TimKiem", "MainViewModel", "Lỗi tìm kiếm xe", ex);
            }
        }

        private async Task<(int? SiteId, int? ZoneId, int? LaneId, string SiteName, string ZoneName, int MaxCapacity, int? GateId, string GateName)> ResolveTopologyForLaneAsync(int laneIndex)
        {
            try
            {
                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                var lane = lanes.FirstOrDefault(l => l.Id == laneIndex || l.LaneCode == $"LANE-{laneIndex}");
                if (lane == null) return (null, null, null, "", "", 0, null, "");

                int? laneId = lane.Id;
                int? zoneId = lane.ZoneId;
                int? gateId = lane.GateId;
                int? siteId = null;
                string siteName = "";
                string zoneName = "";
                string gateName = "";
                int maxCapacity = 0;

                if (gateId.HasValue)
                {
                    var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                    var gate = gates.FirstOrDefault(g => g.Id == gateId.Value);
                    if (gate != null)
                    {
                        siteId = gate.SiteId;
                        gateName = gate.GateName;
                        
                        var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                        var site = sites.FirstOrDefault(s => s.Id == siteId.Value);
                        if (site != null)
                        {
                            siteName = site.SiteName;
                        }
                    }
                }

                if (zoneId.HasValue)
                {
                    var zones = await ParkingTopologyService.Instance.GetZonesAsync();
                    var zone = zones.FirstOrDefault(z => z.Id == zoneId.Value);
                    if (zone != null)
                    {
                        if (!siteId.HasValue)
                        {
                            siteId = zone.SiteId;
                            siteName = zone.SiteName;
                        }
                        zoneName = zone.ZoneName;
                        maxCapacity = zone.MaxCapacity;
                    }
                }

                return (siteId, zoneId, laneId, siteName, zoneName, maxCapacity, gateId, gateName);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ResolveTopology", "MainViewModel", $"Failed for lane {laneIndex}", ex);
                return (null, null, null, "", "", 0, null, "");
            }
        }

        public void XeChiTiet(Xe xe)
        {
            if (xe == null) return;
            new Views.VehicleDetailWindow(xe).ShowDialog();
        }

        public void UnsubscribeEvents()
        {
            try
            {
                // Unsubscribe from global services to prevent memory leaks and background crashes
                C3200Service.Instance.OnConnectionChanged -= OnC3200ConnectionChanged;
                ConnectionMonitorService.Instance.StatusChanged -= OnConnectionStatusChanged;
                ConnectivityStateService.Instance.PropertyChanged -= OnConnectivityChanged;
            }
            catch { }
        }

        private void OnC3200ConnectionChanged(bool online)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
                TrangThaiKetNoi = online ? "C3200: Online ●" : "C3200: Offline ○");
        }

        private void OnConnectivityChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConnectivityStateService.IsOnline))
            {
                if (ConnectivityStateService.Instance.IsOnline)
                {
                    UpdateVehicleCount();
                }
            }
        }
        public bool IsDeploymentCenterVisible
        {
            get
            {
                var role = QuanLyGiuXe.Models.CurrentUser.Role;
                return string.Equals(role, "SuperAdmin", System.StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(role, "Admin", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ResetLaneCardAndOwnerDetails(int lane)
        {
            if (lane == 1)
            {
                Lane1CardName = "";
                Lane1LoaiXe = "";
                Lane1LoaiVe = "";
                Lane1EmployeeName = "";
                Lane1EmployeeCode = "";
                Lane1EmployeeCompany = "";
                Lane1EmployeeDepartment = "";
                Lane1EmployeePosition = "";
                Lane1EmployeePhone = "";
                Lane1EmployeeEmail = "";
                Lane1EmployeeAvatar = null;
                Lane1HasEmployee = false;
                Lane1OwnerStatusText = "Chờ thẻ...";
                Lane1PlateInImage = null;
                Lane1PlateOutImage = null;
                Lane1PlateInVisibility = Visibility.Collapsed;
                Lane1PlateOutVisibility = Visibility.Collapsed;
            }
            else
            {
                Lane2CardName = "";
                Lane2LoaiXe = "";
                Lane2LoaiVe = "";
                Lane2EmployeeName = "";
                Lane2EmployeeCode = "";
                Lane2EmployeeCompany = "";
                Lane2EmployeeDepartment = "";
                Lane2EmployeePosition = "";
                Lane2EmployeePhone = "";
                Lane2EmployeeEmail = "";
                Lane2EmployeeAvatar = null;
                Lane2HasEmployee = false;
                Lane2OwnerStatusText = "Chờ thẻ...";
                Lane2PlateInImage = null;
                Lane2PlateOutImage = null;
                Lane2PlateInVisibility = Visibility.Collapsed;
                Lane2PlateOutVisibility = Visibility.Collapsed;
            }
        }

        private void SetLanePlateImages(int lane, string? entryFolder, string? exitFolder)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                ImageSource? entryImg = null;
                ImageSource? exitImg = null;

                var resolvedEntryFolder = QuanLyGiuXe.Services.ParkingImageService.ResolveSharedFolderPath(entryFolder);
                if (!string.IsNullOrEmpty(resolvedEntryFolder) && System.IO.Directory.Exists(resolvedEntryFolder))
                {
                    string path = System.IO.Path.Combine(resolvedEntryFolder, "plate_crop.jpg");
                    if (System.IO.File.Exists(path)) entryImg = LoadImageFromFile(path);
                }

                var resolvedExitFolder = QuanLyGiuXe.Services.ParkingImageService.ResolveSharedFolderPath(exitFolder);
                if (!string.IsNullOrEmpty(resolvedExitFolder) && System.IO.Directory.Exists(resolvedExitFolder))
                {
                    string path = System.IO.Path.Combine(resolvedExitFolder, "plate_crop.jpg");
                    if (System.IO.File.Exists(path)) exitImg = LoadImageFromFile(path);
                }

                var showImg = exitImg ?? entryImg;

                if (lane == 1)
                {
                    Lane1PlateInImage = showImg;
                    Lane1PlateInVisibility = showImg != null ? Visibility.Visible : Visibility.Collapsed;
                    Lane1PlateOutImage = exitImg;
                    Lane1PlateOutVisibility = exitImg != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    Lane2PlateInImage = showImg;
                    Lane2PlateInVisibility = showImg != null ? Visibility.Visible : Visibility.Collapsed;
                    Lane2PlateOutImage = exitImg;
                    Lane2PlateOutVisibility = exitImg != null ? Visibility.Visible : Visibility.Collapsed;
                }
            }));
        }

        private static ImageSource? LoadImageFromFile(string path)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        private void LoadOwnerDetailsForLane(int lane, RFIDCard card)
        {
            if (card == null) return;

            string loaiXeName = "";
            string loaiVeName = "";
            try
            {
                var lxList = db.GetLoaiXe();
                var lx = lxList.FirstOrDefault(x => x.Id == card.LoaiXeId);
                if (lx != null) loaiXeName = lx.TenLoai ?? "";
            }
            catch { }

            try
            {
                var lvList = db.GetLoaiVe();
                var lv = lvList.FirstOrDefault(x => x.Id == card.LoaiVeId);
                if (lv != null) loaiVeName = lv.TenLoai ?? "";
            }
            catch { }

            Employee? emp = null;
            if (card.EmployeeId.HasValue)
            {
                try
                {
                    emp = _crudService.GetEmployeeById(card.EmployeeId.Value);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("LoadOwnerDetails", "MainViewModel", $"Error loading employee {card.EmployeeId}", ex);
                }
            }

            if (lane == 1)
            {
                Lane1CardName = card.CardName ?? "";
                Lane1LoaiXe = loaiXeName;
                Lane1LoaiVe = loaiVeName;

                if (emp != null)
                {
                    Lane1EmployeeName = emp.FullName ?? "";
                    Lane1EmployeeCode = emp.EmployeeCode ?? "";
                    Lane1EmployeeCompany = emp.CompanyName ?? "";
                    Lane1EmployeeDepartment = emp.DepartmentName ?? "";
                    Lane1EmployeePosition = emp.PositionName ?? "";
                    Lane1EmployeePhone = emp.Phone ?? "";
                    Lane1EmployeeEmail = emp.Email ?? "";
                    Lane1EmployeeAvatar = ConvertBase64ToImage(emp.Avatar);
                    Lane1HasEmployee = true;
                    Lane1OwnerStatusText = "";
                }
                else
                {
                    Lane1EmployeeName = card.EmployeeName ?? "";
                    Lane1EmployeeCode = card.EmployeeCode ?? "";
                    Lane1EmployeeCompany = "";
                    Lane1EmployeeDepartment = "";
                    Lane1EmployeePosition = "";
                    Lane1EmployeePhone = "";
                    Lane1EmployeeEmail = "";
                    Lane1EmployeeAvatar = null;
                    Lane1HasEmployee = !string.IsNullOrEmpty(card.EmployeeName);
                    Lane1OwnerStatusText = !string.IsNullOrEmpty(card.EmployeeName) ? "" : "Chưa cập nhật thông tin chủ thẻ";
                }
            }
            else
            {
                Lane2CardName = card.CardName ?? "";
                Lane2LoaiXe = loaiXeName;
                Lane2LoaiVe = loaiVeName;

                if (emp != null)
                {
                    Lane2EmployeeName = emp.FullName ?? "";
                    Lane2EmployeeCode = emp.EmployeeCode ?? "";
                    Lane2EmployeeCompany = emp.CompanyName ?? "";
                    Lane2EmployeeDepartment = emp.DepartmentName ?? "";
                    Lane2EmployeePosition = emp.PositionName ?? "";
                    Lane2EmployeePhone = emp.Phone ?? "";
                    Lane2EmployeeEmail = emp.Email ?? "";
                    Lane2EmployeeAvatar = ConvertBase64ToImage(emp.Avatar);
                    Lane2HasEmployee = true;
                    Lane2OwnerStatusText = "";
                }
                else
                {
                    Lane2EmployeeName = card.EmployeeName ?? "";
                    Lane2EmployeeCode = card.EmployeeCode ?? "";
                    Lane2EmployeeCompany = "";
                    Lane2EmployeeDepartment = "";
                    Lane2EmployeePosition = "";
                    Lane2EmployeePhone = "";
                    Lane2EmployeeEmail = "";
                    Lane2EmployeeAvatar = null;
                    Lane2HasEmployee = !string.IsNullOrEmpty(card.EmployeeName);
                    Lane2OwnerStatusText = !string.IsNullOrEmpty(card.EmployeeName) ? "" : "Chưa cập nhật thông tin chủ thẻ";
                }
            }
        }

        public void RefreshCurrentUserInfo()
        {
            OnPropertyChanged(nameof(CurrentUserTen));
            OnPropertyChanged(nameof(CurrentUserUsername));
            OnPropertyChanged(nameof(CurrentUserRole));
            OnPropertyChanged(nameof(IsDeploymentCenterVisible));
        }

        private static System.Windows.Media.ImageSource? MatToBitmapSource(OpenCvSharp.Mat? mat)
        {
            if (mat == null || mat.Empty()) return null;
            try
            {
                using (var bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat))
                {
                    var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
                    var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
                    try
                    {
                        System.Windows.Media.PixelFormat wpfFormat;
                        switch (bmp.PixelFormat)
                        {
                            case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                                wpfFormat = System.Windows.Media.PixelFormats.Bgr24;
                                break;
                            case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                            case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                            case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                                wpfFormat = System.Windows.Media.PixelFormats.Bgr32;
                                break;
                            case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                                wpfFormat = System.Windows.Media.PixelFormats.Gray8;
                                break;
                            default:
                                wpfFormat = System.Windows.Media.PixelFormats.Bgr24;
                                break;
                        }

                        var bitmapSource = System.Windows.Media.Imaging.BitmapSource.Create(
                            bmpData.Width, bmpData.Height,
                            bmp.HorizontalResolution, bmp.VerticalResolution,
                            wpfFormat,
                            null,
                            bmpData.Scan0,
                            bmpData.Stride * bmpData.Height,
                            bmpData.Stride);

                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }
                }
            }
            catch (Exception ex)
            {
                try { LoggingService.Instance.LogError("MatToBitmapSource", "MainViewModel", "Custom convert Mat to BitmapSource failed", ex); } catch { }
                return null;
            }
        }

        public void Dispose()
        {
            UnsubscribeEvents();
        }
    }

    public class ZoneOccupancyInfo : INotifyPropertyChanged
    {
        private string _zoneName = string.Empty;
        public string ZoneName
        {
            get => _zoneName;
            set { _zoneName = value; OnPropertyChanged(nameof(ZoneName)); }
        }

        private int _count;
        public int Count
        {
            get => _count;
            set 
            { 
                _count = value; 
                OnPropertyChanged(nameof(Count)); 
                OnPropertyChanged(nameof(CapacityText));
                OnPropertyChanged(nameof(Percentage));
            }
        }

        private int _maxCapacity;
        public int MaxCapacity
        {
            get => _maxCapacity;
            set 
            { 
                _maxCapacity = value; 
                OnPropertyChanged(nameof(MaxCapacity)); 
                OnPropertyChanged(nameof(CapacityText));
                OnPropertyChanged(nameof(Percentage));
            }
        }

        public string CapacityText => MaxCapacity > 0 ? $"{Count}/{MaxCapacity}" : $"{Count}";
        public double Percentage => MaxCapacity > 0 ? (double)Count / MaxCapacity * 100 : 0;
        public string DisplayIcon => (ZoneName.ToLower().Contains("xe máy") || ZoneName.ToLower().Contains("xe may") || ZoneName.ToLower().Contains("moto")) ? "🛵" : "🚗";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
