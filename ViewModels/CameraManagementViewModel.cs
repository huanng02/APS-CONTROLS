using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OpenCvSharp;
using AForge.Video.DirectShow;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class CameraManagementViewModel : BaseViewModel
    {
        private readonly DispatcherTimer _statusTimer;
        private List<CameraUiModel> _allCameras = new();
        private readonly int? _defaultLaneId;
        private bool _suppressAutoRebuildRtspUrl = false;
        
        public ObservableCollection<CameraUiModel> Cameras { get; } = new();
        public List<LaneConfig> Lanes { get; private set; } = new();
        public List<string> StatusFilterOptions { get; } = new() { "Tất cả", "Online", "Offline" };
        public List<string> ProtocolOptions { get; } = new() { "RTSP", "ONVIF" };
        public List<string> DirectionOptions { get; } = new() { "Entry", "Exit", "Overview" };
        public List<string> ResolutionOptions { get; } = new() { "Mặc định", "1920x1080", "1280x720", "640x480" };

        public CameraManagementViewModel() : this(null, null)
        {
        }

        public CameraManagementViewModel(int? defaultLaneId = null, string? defaultRole = null)
        {
            _defaultLaneId = defaultLaneId;
            // Initializing Commands
            AddNewCommand = new RelayCommand(_ => EnterCreateMode());
            EditCommand = new RelayCommand(_ => EnterEditMode(), _ => SelectedCamera != null);
            DeleteCommand = new RelayCommand(async _ => await ExecuteDeleteAsync(), _ => SelectedCamera != null);
            SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync(), _ => !HasIpError && IsConnectionSuccessful);
            CancelCommand = new RelayCommand(_ => CancelEditOrCreate());
            TestConnectionCommand = new RelayCommand(async _ => await ExecuteTestConnectionAsync());
            RefreshStatusesCommand = new RelayCommand(_ => UpdateConnectionStatuses());
            DiscoverCamerasCommand = new RelayCommand(_ => ExecuteDiscoverCameras());

            // Polling timer for live status updates
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(2);
            _statusTimer.Tick += (s, e) => UpdateConnectionStatuses();
            
            // Initial data load
            _ = LoadDataWithDefaultsAsync(defaultLaneId, defaultRole);
        }

        // Search & Filters properties
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        private string _selectedStatusFilter = "Tất cả";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter != value)
                {
                    _selectedStatusFilter = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        // Selected item
        private CameraUiModel? _selectedCamera;
        public CameraUiModel? SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                if (_selectedCamera != value)
                {
                    _selectedCamera = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDetailMode));
                    
                    if (!IsEditMode && !IsCreateMode)
                    {
                        LoadEditorFieldsFromSelected();
                    }
                }
            }
        }

        // Editor Form properties
        private string _cameraName = string.Empty;
        public string CameraName
        {
            get => _cameraName;
            set { _cameraName = value; OnPropertyChanged(); }
        }

        private string _cameraKey = string.Empty;
        public string CameraKey
        {
            get => _cameraKey;
            set { _cameraKey = value; OnPropertyChanged(); }
        }

        private string _ipValidationMessage = string.Empty;
        public string IpValidationMessage
        {
            get => _ipValidationMessage;
            set 
            { 
                _ipValidationMessage = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HasIpError)); 
            }
        }

        public bool HasIpError => !string.IsNullOrEmpty(IpValidationMessage);

        private bool _isConnectionSuccessful = false;
        public bool IsConnectionSuccessful
        {
            get => _isConnectionSuccessful;
            set 
            { 
                _isConnectionSuccessful = value; 
                OnPropertyChanged(); 
                CommandManager.InvalidateRequerySuggested(); 
            }
        }

        private bool _isLoadingFields = false;

        private string _ipAddress = string.Empty;
        public string IpAddress
        {
            get => _ipAddress;
            set 
            { 
                _ipAddress = value; 
                OnPropertyChanged(); 
                AutoRebuildRtspUrl(); 
                TriggerIpValidation();
                if (!_isLoadingFields) IsConnectionSuccessful = false;
            }
        }

        private void TriggerIpValidation()
        {
            if (!IsCreateMode && !IsEditMode)
            {
                IpValidationMessage = string.Empty;
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            var ip = IpAddress?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(ip))
            {
                IpValidationMessage = string.Empty;
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            if (Direction == "Overview")
            {
                IpValidationMessage = string.Empty;
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            int? currentId = IsEditMode && SelectedCamera != null ? SelectedCamera.Id : (int?)null;
            
            bool isDuplicate = _allCameras.Any(c => c.Id != currentId && 
                                                    c.Direction != "Overview" &&
                                                    !string.IsNullOrEmpty(c.IpAddress) && 
                                                    c.IpAddress.Trim().Equals(ip, StringComparison.OrdinalIgnoreCase));

            if (isDuplicate)
            {
                IpValidationMessage = "A camera with this IP address already exists.";
            }
            else
            {
                IpValidationMessage = string.Empty;
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private int? _port = 554;
        public int? Port
        {
            get => _port;
            set 
            { 
                _port = value; 
                OnPropertyChanged(); 
                AutoRebuildRtspUrl(); 
                if (!_isLoadingFields) IsConnectionSuccessful = false;
            }
        }

        private string _protocol = "RTSP";
        public string Protocol
        {
            get => _protocol;
            set { _protocol = value; OnPropertyChanged(); }
        }

        private string _username = "admin";
        public string Username
        {
            get => _username;
            set 
            { 
                _username = value; 
                OnPropertyChanged(); 
                AutoRebuildRtspUrl(); 
                if (!_isLoadingFields) IsConnectionSuccessful = false;
            }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set 
            { 
                _password = value; 
                OnPropertyChanged(); 
                AutoRebuildRtspUrl(); 
                if (!_isLoadingFields) IsConnectionSuccessful = false;
            }
        }

        private string _rtspUrl = string.Empty;
        public string RtspUrl
        {
            get => _rtspUrl;
            set 
            { 
                _rtspUrl = value; 
                OnPropertyChanged(); 
                if (!_isLoadingFields) IsConnectionSuccessful = false;
            }
        }

        private LaneConfig? _selectedLane;
        public LaneConfig? SelectedLane
        {
            get => _selectedLane;
            set { _selectedLane = value; OnPropertyChanged(); }
        }

        private string _direction = "Overview";
        public string Direction
        {
            get => _direction;
            set 
            { 
                _direction = value; 
                OnPropertyChanged(); 
                TriggerIpValidation();
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private string _resolutionOption = "Mặc định";
        public string ResolutionOption
        {
            get => _resolutionOption;
            set { _resolutionOption = value; OnPropertyChanged(); }
        }

        // Connection Test status
        private string _testConnectionResult = string.Empty;
        public string TestConnectionResult
        {
            get => _testConnectionResult;
            set { _testConnectionResult = value; OnPropertyChanged(); }
        }

        private string _testConnectionColor = "Gray";
        public string TestConnectionColor
        {
            get => _testConnectionColor;
            set { _testConnectionColor = value; OnPropertyChanged(); }
        }

        private bool _isTestingConnection;
        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set { _isTestingConnection = value; OnPropertyChanged(); }
        }

        // View States
        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailMode));
                OnPropertyChanged(nameof(IsFormEditable));
                OnPropertyChanged(nameof(IsLaneSelectionEnabled));
            }
        }

        private bool _isCreateMode;
        public bool IsCreateMode
        {
            get => _isCreateMode;
            set
            {
                _isCreateMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailMode));
                OnPropertyChanged(nameof(IsFormEditable));
                OnPropertyChanged(nameof(IsLaneSelectionEnabled));
            }
        }

        public bool IsDetailMode => SelectedCamera != null && !IsEditMode && !IsCreateMode;
        public bool IsFormEditable => IsEditMode || IsCreateMode;

        private bool _isLaneSelectionLocked;
        public bool IsLaneSelectionLocked
        {
            get => _isLaneSelectionLocked;
            set
            {
                _isLaneSelectionLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLaneSelectionEnabled));
            }
        }

        public bool IsLaneSelectionEnabled => IsFormEditable && !_isLaneSelectionLocked && !_defaultLaneId.HasValue;

        // Commands
        public ICommand AddNewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand RefreshStatusesCommand { get; }
        public ICommand DiscoverCamerasCommand { get; }

        public async Task LoadDataAsync()
        {
            _statusTimer.Stop();

            // Load Lanes
            try
            {
                var dbLanes = ParkingTopologyService.Instance.GetLanes();
                Lanes = dbLanes ?? new List<LaneConfig>();
                OnPropertyChanged(nameof(Lanes));
            }
            catch { }

            // Load Cameras
            var dbCams = await CameraRepository.Instance.GetAllAsync();
            _allCameras = dbCams.Select(c => new CameraUiModel(c)).ToList();

            UpdateConnectionStatuses();
            ApplyFilter();

            _statusTimer.Start();
        }

        public async Task LoadDataWithDefaultsAsync(int? defaultLaneId, string? defaultRole)
        {
            await LoadDataAsync();
            
            if (defaultLaneId.HasValue)
            {
                // Auto enter create/add new mode
                EnterCreateMode();
                
                // Set default lane
                SelectedLane = Lanes.FirstOrDefault(l => l.Id == defaultLaneId.Value);
                IsLaneSelectionLocked = true;
                
                // Set direction/role defaults
                if (defaultRole == "ToanCanh")
                {
                    Direction = "Overview";
                }
                else if (defaultRole == "BienSo")
                {
                    if (SelectedLane?.Direction?.ToUpper() == "OUT")
                    {
                        Direction = "Exit";
                    }
                    else
                    {
                        Direction = "Entry";
                    }
                }
            }
        }

        private void UpdateConnectionStatuses()
        {
            foreach (var cam in _allCameras)
            {
                var state = CameraService.Instance.GetRuntimeState(cam.CameraKey);
                cam.Status = state.IsConnected ? "Online" : "Offline";
                cam.ReconnectCount = state.ReconnectCount;
            }
        }

        private void ApplyFilter()
        {
            Cameras.Clear();
            var query = _allCameras.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();
                query = query.Where(c => c.CameraName.ToLower().Contains(search) || c.IpAddress.ToLower().Contains(search));
            }

            if (SelectedStatusFilter == "Online")
            {
                query = query.Where(c => c.Status == "Online");
            }
            else if (SelectedStatusFilter == "Offline")
            {
                query = query.Where(c => c.Status == "Offline");
            }

            foreach (var c in query)
            {
                Cameras.Add(c);
            }
        }

        private void LoadEditorFieldsFromSelected()
        {
            _isLoadingFields = true;
            try
            {
                TestConnectionResult = string.Empty;
                if (SelectedCamera != null)
                {
                    CameraName = SelectedCamera.CameraName;
                    CameraKey = SelectedCamera.CameraKey;
                    IpAddress = SelectedCamera.IpAddress;
                    Port = SelectedCamera.Port;
                    Protocol = SelectedCamera.Protocol;
                    Username = SelectedCamera.Username;
                    Password = SelectedCamera.Password;
                    RtspUrl = SelectedCamera.RtspUrl;
                    SelectedLane = Lanes.FirstOrDefault(l => l.Id == SelectedCamera.LaneId);
                    Direction = SelectedCamera.Direction;
                    IsActive = SelectedCamera.IsActive;
                    if (SelectedCamera.ResolutionWidth.HasValue && SelectedCamera.ResolutionHeight.HasValue)
                    {
                        ResolutionOption = $"{SelectedCamera.ResolutionWidth}x{SelectedCamera.ResolutionHeight}";
                    }
                    else
                    {
                        ResolutionOption = "Mặc định";
                    }
                }
                else
                {
                    ClearEditorFields();
                }
            }
            finally
            {
                _isLoadingFields = false;
            }
        }

        private void ClearEditorFields()
        {
            _isLoadingFields = true;
            try
            {
                CameraName = string.Empty;
                CameraKey = string.Empty;
                IpAddress = string.Empty;
                Port = 554;
                Protocol = "RTSP";
                Username = "admin";
                Password = string.Empty;
                RtspUrl = string.Empty;
                SelectedLane = null;
                Direction = "Overview";
                IsActive = true;
                ResolutionOption = "Mặc định";
                TestConnectionResult = string.Empty;
            }
            finally
            {
                _isLoadingFields = false;
            }
        }

        private void EnterCreateMode()
        {
            SelectedCamera = null;
            IsCreateMode = true;
            IsEditMode = false;
            ClearEditorFields();
            IpValidationMessage = string.Empty;
            IsConnectionSuccessful = false;
            CameraKey = "Cam_" + Guid.NewGuid().ToString().Substring(0, 8);
            IsLaneSelectionLocked = _defaultLaneId.HasValue;
            if (_defaultLaneId.HasValue)
            {
                SelectedLane = Lanes.FirstOrDefault(l => l.Id == _defaultLaneId.Value);
            }
        }

        private void EnterEditMode()
        {
            if (SelectedCamera == null) return;
            IsCreateMode = false;
            IsEditMode = true;
            IsLaneSelectionLocked = _defaultLaneId.HasValue;
            LoadEditorFieldsFromSelected();
            IpValidationMessage = string.Empty;
            IsConnectionSuccessful = true;
        }

        private void CancelEditOrCreate()
        {
            IsCreateMode = false;
            IsEditMode = false;
            IsLaneSelectionLocked = _defaultLaneId.HasValue;
            LoadEditorFieldsFromSelected();
            IpValidationMessage = string.Empty;
            IsConnectionSuccessful = false;
        }

        private void AutoRebuildRtspUrl()
        {
            if (_suppressAutoRebuildRtspUrl) return;
            if (string.IsNullOrWhiteSpace(IpAddress)) return;
            RtspUrl = $"rtsp://{IpAddress}:{Port ?? 554}/user={Username}&password={Password}&channel=0&stream=0.sdp?real_stream";
        }

        private void ExecuteDiscoverCameras()
        {
            var win = new QuanLyGiuXe.Views.CameraDiscoveryWindow();
            win.Owner = Application.Current.MainWindow;
            if (win.ShowDialog() == true && win.ResultCamera != null)
            {
                var cam = win.ResultCamera;
                _suppressAutoRebuildRtspUrl = true;
                try
                {
                    IpAddress = cam.IpAddress;
                    Port = cam.RtspPort;
                    Protocol = "ONVIF";
                    Username = win.EnteredUsername;
                    Password = win.EnteredPassword;
                    RtspUrl = cam.RtspUrl;
                    
                    if (string.IsNullOrWhiteSpace(CameraName))
                    {
                        CameraName = $"{cam.Manufacturer} {cam.Model}";
                    }
                }
                finally
                {
                    _suppressAutoRebuildRtspUrl = false;
                }
                
                TriggerIpValidation();
            }
        }

        private async Task ExecuteTestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(RtspUrl))
            {
                TestConnectionResult = "⚠️ Vui lòng nhập RTSP URL";
                TestConnectionColor = "Orange";
                return;
            }

            IsTestingConnection = true;
            TestConnectionResult = "⏳ Đang kết nối thử...";
            TestConnectionColor = "Orange";

            string targetUrl = RtspUrl.Trim();

            bool success = await Task.Run(() =>
            {
                VideoCapture? capture = null;
                try
                {
                    if (targetUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
                        targetUrl.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
                        targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        capture = new VideoCapture(targetUrl, VideoCaptureAPIs.FFMPEG);
                    }
                    else if (int.TryParse(targetUrl, out int index))
                    {
                        capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
                    }
                    else
                    {
                        int foundIndex = -1;
                        try
                        {
                            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                            for (int i = 0; i < devices.Count; i++)
                            {
                                if (devices[i].Name.Equals(targetUrl, StringComparison.OrdinalIgnoreCase) ||
                                    devices[i].Name.Contains(targetUrl, StringComparison.OrdinalIgnoreCase))
                                {
                                    foundIndex = i;
                                    break;
                                }
                            }
                        }
                        catch { }

                        if (foundIndex >= 0)
                            capture = new VideoCapture(foundIndex, VideoCaptureAPIs.DSHOW);
                        else
                            capture = new VideoCapture(targetUrl);
                    }

                    using (capture)
                    {
                        return capture != null && capture.IsOpened();
                    }
                }
                catch
                {
                    return false;
                }
            });

            IsTestingConnection = false;
            if (success)
            {
                TestConnectionResult = "✅ Kết nối thành công!";
                TestConnectionColor = "Green";
                IsConnectionSuccessful = true;
            }
            else
            {
                TestConnectionResult = "❌ Kết nối thất bại!";
                TestConnectionColor = "Red";
                IsConnectionSuccessful = false;
            }
        }

        private bool ValidateForm(out string error)
        {
            error = "";
            if (HasIpError)
            {
                error = IpValidationMessage;
                return false;
            }
            if (!IsConnectionSuccessful)
            {
                error = "Vui lòng kiểm tra kết nối camera thành công trước khi lưu.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(CameraName))
            {
                error = "Vui lòng nhập Tên Camera.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(CameraKey))
            {
                error = "Vui lòng nhập Mã Camera.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(IpAddress))
            {
                error = "Vui lòng nhập Địa chỉ IP.";
                return false;
            }
            if (!Port.HasValue)
            {
                error = "Vui lòng nhập Cổng.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(RtspUrl))
            {
                error = "Vui lòng nhập RTSP URL.";
                return false;
            }

            string normName = CameraName.Trim();
            string normKey = CameraKey.Trim();

            if (IsCreateMode)
            {
                if (_allCameras.Any(c => c.CameraName.Equals(normName, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Tên Camera đã tồn tại.";
                    return false;
                }
                if (_allCameras.Any(c => c.CameraKey.Equals(normKey, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Mã Camera đã tồn tại.";
                    return false;
                }
            }
            else if (IsEditMode && SelectedCamera != null)
            {
                if (_allCameras.Any(c => c.Id != SelectedCamera.Id && c.CameraName.Equals(normName, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Tên Camera đã tồn tại ở camera khác.";
                    return false;
                }
                if (_allCameras.Any(c => c.Id != SelectedCamera.Id && c.CameraKey.Equals(normKey, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Mã Camera đã tồn tại ở camera khác.";
                    return false;
                }
            }

            return true;
        }

        private async Task ExecuteSaveAsync()
        {
            if (!ValidateForm(out var error))
            {
                MessageBox.Show(error, "Lỗi kiểm tra dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string confirmMsg = IsCreateMode ? "Bạn có chắc chắn muốn thêm camera mới này?" : "Bạn có chắc chắn muốn lưu các thay đổi này?";
            string confirmTitle = IsCreateMode ? "Xác nhận thêm mới" : "Xác nhận lưu";
            var confirmResult = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;

            int? width = null;
            int? height = null;
            if (!string.IsNullOrEmpty(ResolutionOption) && ResolutionOption != "Mặc định")
            {
                var parts = ResolutionOption.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                {
                    width = w;
                    height = h;
                }
            }

            var entity = new CameraEntity
            {
                CameraName = CameraName.Trim(),
                CameraKey = CameraKey.Trim(),
                IpAddress = IpAddress.Trim(),
                Port = Port,
                Protocol = Protocol,
                Username = Username.Trim(),
                Password = Password,
                RtspUrl = RtspUrl.Trim(),
                LaneId = SelectedLane?.Id,
                Direction = Direction,
                IsActive = IsActive,
                ResolutionWidth = width,
                ResolutionHeight = height,
                CreatedUtc = DateTime.UtcNow
            };

            bool isCreate = IsCreateMode;
            CameraEntity? previous = null;
            if (!isCreate && SelectedCamera != null)
            {
                try
                {
                    previous = await CameraRepository.Instance.GetByIdAsync(SelectedCamera.Id);
                }
                catch { }
            }

            bool success;
            string errorMessage = "Lỗi khi lưu cấu hình camera vào cơ sở dữ liệu.";
            try
            {
                if (isCreate)
                {
                    success = await CameraRepository.Instance.InsertAsync(entity);
                }
                else
                {
                    entity.Id = SelectedCamera!.Id;
                    success = await CameraRepository.Instance.UpdateAsync(entity);
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
            }

            if (success)
            {
                string successMsg = isCreate ? "Thêm camera mới thành công!" : "Lưu cấu hình camera thành công!";

                IsCreateMode = false;
                IsEditMode = false;
                await LoadDataAsync();
                
                // Select the saved camera
                SelectedCamera = Cameras.FirstOrDefault(c => c.CameraKey == entity.CameraKey);

                // Audit the change
                try
                {
                    await ConfigurationAuditService.Instance.AuditChangesAsync("Camera", previous, entity);
                }
                catch { }

                // Sync the configuration immediately to draft and active configs
                try
                {
                    await SyncCamerasToConfigAsync(force: true);
                }
                catch { }

                // Mark pending changes so Development Center knows there are draft changes
                try
                {
                    await DeploymentService.Instance.MarkPendingChangesAsync();
                }
                catch { /* Best effort */ }

                MessageBox.Show(successMsg + "\nVui lòng vào Development Center để triển khai cấu hình mới.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(errorMessage, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteDeleteAsync()
        {
            if (SelectedCamera == null) return;

            var confirmMsg = $"Bạn có chắc chắn muốn xóa camera '{SelectedCamera.CameraName}'?";
            
            bool isAssigned = await CameraRepository.Instance.IsAssignedToAnyLaneAsync(SelectedCamera.Id, SelectedCamera.CameraName, SelectedCamera.RtspUrl);
            if (isAssigned)
            {
                confirmMsg = $"⚠️ CẢNH BÁO: Camera '{SelectedCamera.CameraName}' hiện đang được gán cho một làn xe hoạt động.\n" +
                             "Nếu xóa, cấu hình làn xe đó có thể bị lỗi.\n\n" +
                             "Bạn vẫn chắc chắn muốn xóa camera này?";
            }

            var result = MessageBox.Show(confirmMsg, "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                CameraEntity? previous = null;
                try
                {
                    previous = await CameraRepository.Instance.GetByIdAsync(SelectedCamera.Id);
                }
                catch { }

                bool deleted = await CameraRepository.Instance.DeleteAsync(SelectedCamera.Id);
                if (deleted)
                {
                    // Audit the change
                    try
                    {
                        await ConfigurationAuditService.Instance.AuditChangesAsync<CameraEntity>("Camera", previous, null);
                    }
                    catch { }

                    // Sync the configuration immediately to draft and active configs
                    try
                    {
                        await SyncCamerasToConfigAsync(force: true);
                    }
                    catch { }

                    await LoadDataAsync();
                    SelectedCamera = null;

                    // Mark pending changes so Development Center knows there are draft changes
                    try
                    {
                        await DeploymentService.Instance.MarkPendingChangesAsync();
                    }
                    catch { /* Best effort */ }

                    MessageBox.Show("Xóa camera thành công!\nVui lòng vào Development Center để triển khai thay đổi.", "Thông báo");
                }
                else
                {
                    MessageBox.Show("Lỗi khi xóa camera khỏi cơ sở dữ liệu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task SyncCamerasToConfigAsync(bool force = false)
        {
            await CameraService.Instance.SyncCamerasToConfigAsync(force);
        }
    }

    public class CameraUiModel : BaseViewModel
    {
        public CameraEntity Entity { get; }

        public CameraUiModel(CameraEntity entity)
        {
            Entity = entity;
        }

        public int Id => Entity.Id;
        public string CameraName => Entity.CameraName;
        public string CameraKey => Entity.CameraKey;
        public string IpAddress => Entity.IpAddress;
        public int? Port => Entity.Port;
        public string Protocol => Entity.Protocol;
        public string Username => Entity.Username;
        public string Password => Entity.Password;
        public string RtspUrl => Entity.RtspUrl;
        public int? LaneId => Entity.LaneId;
        public string Direction => Entity.Direction;
        public bool IsActive => Entity.IsActive;
        public int? ResolutionWidth => Entity.ResolutionWidth;
        public int? ResolutionHeight => Entity.ResolutionHeight;
        public DateTime CreatedUtc => Entity.CreatedUtc;
        public string LaneName => Entity.LaneName;

        private string _status = "Offline";
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _reconnectCount;
        public int ReconnectCount
        {
            get => _reconnectCount;
            set
            {
                if (_reconnectCount != value)
                {
                    _reconnectCount = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
