using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using AForge.Video.DirectShow;

namespace QuanLyGiuXe
{
    public partial class C3200SettingsWindow : Window
    {
        private AppConfig _cfg;
        private bool _isInitializing = false;
        private List<LaneConfig> _lanes;
        private List<ParkingSite> _sites;
        private List<LoaiXe> _vehicleTypes;
        
        private FilterInfoCollection _cameras;
        private readonly List<(int LaneId, ComboBox cbToanCanh, ComboBox cbBienSo)> _laneCameraCombos = new();
        private readonly Dictionary<string, string> _cameraNameToUrlMap = new();
        private const string AutoOption = "(Tự động)";

        public C3200SettingsWindow()
        {
            InitializeComponent();
            _cfg = AppConfig.LoadDraft();

            _cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            IpBox.Text = _cfg.ZKTeco.IpAddress;
            PortBox.Text = _cfg.ZKTeco.TcpPort.ToString();
            PwdBox.Text = _cfg.ZKTeco.Password;
            TimeoutBox.Text = _cfg.ZKTeco.Timeout.ToString();
            BarrierBox.Text = _cfg.ZKTeco.BarrierDuration.ToString();
            CooldownBox.Text = _cfg.ZKTeco.CardCooldownMs.ToString();

            var b1 = this.FindName("Button1ActionCombo") as ComboBox;
            var b2 = this.FindName("Button2ActionCombo") as ComboBox;   
            if (b1 != null && b2 != null)
            {
                var act1 = _cfg.ZKTeco.Button1Action ?? "OpenThisDoor";
                var act2 = _cfg.ZKTeco.Button2Action ?? "OpenThisDoor";
                for (int i = 0; i < b1.Items.Count; i++)
                {
                    if ((b1.Items[i] as ComboBoxItem)?.Tag?.ToString() == act1) { b1.SelectedIndex = i; break; }
                }
                for (int i = 0; i < b2.Items.Count; i++)
                {
                    if ((b2.Items[i] as ComboBoxItem)?.Tag?.ToString() == act2) { b2.SelectedIndex = i; break; }
                }
            }
            LoadControllerTypeSelection();
        }

        // =============================
        // INITIALIZE CONFIGURATION (SEQUENTIAL & ROBUST)
        // =============================
        private async Task InitializeConfigurationAsync()
        {
            try
            {
                _isInitializing = true;
 
                // Load all data
                _sites = await ParkingTopologyService.Instance.GetSitesAsync();
                var allGates = await ParkingTopologyService.Instance.GetGatesAsync();
                var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                _lanes = await ParkingTopologyService.Instance.GetLanesAsync();
 
                // Find active controller based on saved IP
                var activeController = allControllers.FirstOrDefault(c => c.IpAddress == _cfg.ZKTeco.IpAddress);
                ParkingGate activeGate = null;
                ParkingSite activeSite = null;
 
                if (activeController != null)
                {
                    activeGate = allGates.FirstOrDefault(g => g.Id == activeController.GateId);
                    if (activeGate != null)
                    {
                        activeSite = _sites.FirstOrDefault(s => s.Id == activeGate.SiteId);
                    }
                }
 
                // If no active controller is found, fallback to first controller/gate/site
                if (activeController == null && _sites.Any())
                {
                    activeSite = _sites.FirstOrDefault();
                    if (activeSite != null)
                    {
                        var siteGates = allGates.Where(g => g.SiteId == activeSite.Id).ToList();
                        if (siteGates.Any())
                        {
                            activeGate = siteGates.FirstOrDefault();
                            if (activeGate != null)
                            {
                                var gateControllers = allControllers.Where(c => c.GateId == activeGate.Id).ToList();
                                if (gateControllers.Any())
                                {
                                    activeController = gateControllers.FirstOrDefault();
                                }
                            }
                        }
                    }
                }
 
                // 1. Populate and select Site
                SiteCombo.ItemsSource = _sites;
                SiteCombo.DisplayMemberPath = "SiteName";
                SiteCombo.SelectedValuePath = "Id";
 
                if (activeSite != null)
                {
                    SiteCombo.SelectedValue = activeSite.Id;
                }
                else if (_sites.Any())
                {
                    SiteCombo.SelectedIndex = 0;
                }
 
                // 2. Populate and select Gate (bound to ZoneCombo control for compatibility) for the selected Site
                int selectedSiteId = SiteCombo.SelectedValue != null ? Convert.ToInt32(SiteCombo.SelectedValue) : 0;
                var gatesForSite = allGates.Where(g => g.SiteId == selectedSiteId).ToList();
                ZoneCombo.ItemsSource = gatesForSite;
                ZoneCombo.DisplayMemberPath = "GateName";
                ZoneCombo.SelectedValuePath = "Id";
 
                if (activeGate != null && gatesForSite.Any(g => g.Id == activeGate.Id))
                {
                    ZoneCombo.SelectedValue = activeGate.Id;
                }
                else if (gatesForSite.Any())
                {
                    ZoneCombo.SelectedIndex = 0;
                }
                else
                {
                    ZoneCombo.SelectedIndex = -1;
                }
 
                // 3. Populate and select Controller for the selected Gate
                int selectedGateId = ZoneCombo.SelectedValue != null ? Convert.ToInt32(ZoneCombo.SelectedValue) : 0;
                var controllersForGate = allControllers.Where(c => c.GateId == selectedGateId).ToList();
                TopologyCombo.ItemsSource = controllersForGate;
                TopologyCombo.DisplayMemberPath = "ControllerName";
                TopologyCombo.SelectedValuePath = "Id";
 
                if (activeController != null && controllersForGate.Any(c => c.Id == activeController.Id))
                {
                    TopologyCombo.SelectedValue = activeController.Id;
                }
                else if (controllersForGate.Any())
                {
                    TopologyCombo.SelectedIndex = 0;
                }
                else
                {
                    TopologyCombo.SelectedIndex = -1;
                }
 
                // 4. Populate and select Lanes for the selected Gate
                var lanesForGate = _lanes.Where(l => l.GateId == selectedGateId).ToList();
                R1LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);
                R2LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);
                R3LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);
                R4LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);
 
                R1LaneCombo.DisplayMemberPath = "LaneName";
                R1LaneCombo.SelectedValuePath = "Id";
                R2LaneCombo.DisplayMemberPath = "LaneName";
                R2LaneCombo.SelectedValuePath = "Id";
                R3LaneCombo.DisplayMemberPath = "LaneName";
                R3LaneCombo.SelectedValuePath = "Id";
                R4LaneCombo.DisplayMemberPath = "LaneName";
                R4LaneCombo.SelectedValuePath = "Id";
 
                // 5. Load vehicle types for lane-type combos
                _vehicleTypes = new DatabaseService().GetLoaiXe();
                var mixedItem = new LoaiXe { Id = 0, TenLoai = "🔀 Hỗn hợp (tất cả)", TrangThai = "Active" };
                
                var r1VtList = new List<LoaiXe> { mixedItem }; r1VtList.AddRange(_vehicleTypes);
                var r2VtList = new List<LoaiXe> { mixedItem }; r2VtList.AddRange(_vehicleTypes);
                var r3VtList = new List<LoaiXe> { mixedItem }; r3VtList.AddRange(_vehicleTypes);
                var r4VtList = new List<LoaiXe> { mixedItem }; r4VtList.AddRange(_vehicleTypes);
                
                R1VehicleTypeCombo.ItemsSource = r1VtList;
                R2VehicleTypeCombo.ItemsSource = r2VtList;
                R3VehicleTypeCombo.ItemsSource = r3VtList;
                R4VehicleTypeCombo.ItemsSource = r4VtList;
                
                R1VehicleTypeCombo.SelectedIndex = 0;
                R2VehicleTypeCombo.SelectedIndex = 0;
                R3VehicleTypeCombo.SelectedIndex = 0;
                R4VehicleTypeCombo.SelectedIndex = 0;

                // Load reader mappings (saved selections)
                LoadReaderSelection();
                await PopulateLaneCamerasUIAsync(lanesForGate);
 
                // Make sure IP box shows current saved IP
                IpBox.Text = _cfg.ZKTeco.IpAddress;
 
                _isInitializing = false;
            }
            catch (Exception ex)
            {
                _isInitializing = false;
                MessageBox.Show($"InitializeConfiguration failed: {ex.Message}");
            }
        }

        // =============================
        // SITE CHANGED
        // =============================
        private async void SiteCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_isInitializing || SiteCombo.SelectedValue == null)
                return;

            try
            {
                int siteId = Convert.ToInt32(SiteCombo.SelectedValue);

                var allGates = await ParkingTopologyService.Instance.GetGatesAsync();

                var gates = allGates
                    .Where(g => g.SiteId == siteId)
                    .ToList();

                ZoneCombo.ItemsSource = gates;
                ZoneCombo.DisplayMemberPath = "GateName";
                ZoneCombo.SelectedValuePath = "Id";

                if (gates.Any())
                    ZoneCombo.SelectedIndex = 0;
                else
                    ZoneCombo.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load gates failed: {ex.Message}");
            }
        }

        // =============================
        // GATE CHANGED
        // =============================
        private async void ZoneCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_isInitializing || ZoneCombo.SelectedValue == null)
                return;

            try
            {
                int gateId = Convert.ToInt32(ZoneCombo.SelectedValue);

                // load controllers by GateId
                var controllers = await ParkingTopologyService.Instance.GetControllersByGateAsync(gateId);

                TopologyCombo.ItemsSource = controllers;
                TopologyCombo.DisplayMemberPath = "ControllerName";
                TopologyCombo.SelectedValuePath = "Id";

                if (controllers.Any())
                    TopologyCombo.SelectedIndex = 0;
                else
                    TopologyCombo.SelectedIndex = -1;

                // load lanes by GateId
                var allLanes = await ParkingTopologyService.Instance.GetLanesAsync();

                var lanes = allLanes
                    .Where(l => l.GateId == gateId)
                    .ToList();

                R1LaneCombo.ItemsSource = new List<LaneConfig>(lanes);
                R2LaneCombo.ItemsSource = new List<LaneConfig>(lanes);
                R3LaneCombo.ItemsSource = new List<LaneConfig>(lanes);
                R4LaneCombo.ItemsSource = new List<LaneConfig>(lanes);

                R1LaneCombo.DisplayMemberPath = "LaneName";
                R1LaneCombo.SelectedValuePath = "Id";

                R2LaneCombo.DisplayMemberPath = "LaneName";
                R2LaneCombo.SelectedValuePath = "Id";

                R3LaneCombo.DisplayMemberPath = "LaneName";
                R3LaneCombo.SelectedValuePath = "Id";

                R4LaneCombo.DisplayMemberPath = "LaneName";
                R4LaneCombo.SelectedValuePath = "Id";

                LoadReaderSelection();
                await PopulateLaneCamerasUIAsync(lanes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load gate failed: {ex.Message}");
            }
        }

        // =============================
        // TOPOLOGY CHANGED
        // =============================
        private void TopologyCombo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            try
            {
                if (TopologyCombo.SelectedItem is C3ControllerConfig controller)
                {
                    IpBox.Text = controller.IpAddress;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load controller failed: {ex.Message}");
            }
        }

        // =============================
        // WINDOW LOADED
        // =============================
        private async void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            await InitializeConfigurationAsync();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Reset tất cả cài đặt về mặc định?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            _cfg = new AppConfig();
            _cfg.Save();

            // reload UI values from defaults
            IpBox.Text = _cfg.ZKTeco.IpAddress;
            PortBox.Text = _cfg.ZKTeco.TcpPort.ToString();
            PwdBox.Text = _cfg.ZKTeco.Password;
            TimeoutBox.Text = _cfg.ZKTeco.Timeout.ToString();
            BarrierBox.Text = _cfg.ZKTeco.BarrierDuration.ToString();
            CooldownBox.Text = _cfg.ZKTeco.CardCooldownMs.ToString();

            var b1c = this.FindName("Button1ActionCombo") as ComboBox;
            var b2c = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1c != null) b1c.SelectedIndex = 1; // default OpenThisDoor
            if (b2c != null) b2c.SelectedIndex = 1;
            LoadControllerTypeSelection();
            MessageBox.Show("Đã reset về mặc định", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ControllerTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCapabilitySummary();
        }

        private void UpdateControllerTypeSelection(ControllerType type)
        {
            if (ControllerTypeCombo == null) return;
            string typeStr = type.ToString();
            for (int i = 0; i < ControllerTypeCombo.Items.Count; i++)
            {
                if ((ControllerTypeCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == typeStr)
                {
                    ControllerTypeCombo.SelectedIndex = i;
                    break;
                }
            }
            UpdateCapabilitySummary();
        }

        private void UpdateCapabilitySummary()
        {
            if (ControllerTypeCombo == null || ReaderCountText == null || RelayCountText == null || MaxLanesText == null) return;
            
            if (ControllerTypeCombo.SelectedItem is ComboBoxItem selectedItem && 
                Enum.TryParse<ControllerType>(selectedItem.Tag?.ToString(), out var selectedType))
            {
                var capability = ControllerCapabilityRegistry.GetCapability(selectedType);
                ReaderCountText.Text = capability.ReaderCount.ToString();
                RelayCountText.Text = capability.RelayCount.ToString();
                MaxLanesText.Text = capability.MaxSupportedLanes.ToString();
            }
        }

        private void LoadControllerTypeSelection()
        {
            if (ControllerTypeCombo == null) return;
            var selectedTypeStr = _cfg.ZKTeco.ControllerType.ToString();
            for (int i = 0; i < ControllerTypeCombo.Items.Count; i++)
            {
                if ((ControllerTypeCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == selectedTypeStr)
                {
                    ControllerTypeCombo.SelectedIndex = i;
                    break;
                }
            }
            if (ControllerTypeCombo.SelectedIndex == -1)
            {
                ControllerTypeCombo.SelectedIndex = 0;
            }
            UpdateCapabilitySummary();
        }

        private async Task PopulateLaneCamerasUIAsync(List<LaneConfig> lanes)
        {
            if (LaneCamerasContainer == null) return;

            LaneCamerasContainer.Children.Clear();
            _laneCameraCombos.Clear();

            if (lanes == null || lanes.Count == 0) return;

            List<Models.CameraConfig> dbCameras = new();
            try
            {
                dbCameras = await ParkingTopologyService.Instance.GetCamerasAsync();
            }
            catch { }

            // Populate the camera name to URL/Path map
            _cameraNameToUrlMap.Clear();
            for (int i = 0; i < _cameras.Count; i++)
            {
                _cameraNameToUrlMap[_cameras[i].Name] = _cameras[i].Name;
            }
            foreach (var dbCam in dbCameras)
            {
                if (!string.IsNullOrEmpty(dbCam.CameraName))
                {
                    _cameraNameToUrlMap[dbCam.CameraName] = dbCam.RtspUrl;
                }
            }

            foreach (var lane in lanes)
            {
                Border border = new Border
                {
                    BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5E7EB")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(15),
                    Margin = new Thickness(5),
                    Background = System.Windows.Media.Brushes.White
                };

                System.Windows.Media.Effects.DropShadowEffect shadow = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = 0.03
                };
                border.Effect = shadow;

                StackPanel sp = new StackPanel();

                TextBlock header = new TextBlock
                {
                    Text = $"🛣️ LÀN: {lane.LaneName} (Chiều {lane.Direction})",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E3A8A")),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                sp.Children.Add(header);

                // Row 1: Cam Toàn Cảnh
                Grid grid1 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                grid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                grid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // Test button column
                grid1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); // Add button column

                TextBlock lbl1 = new TextBlock
                {
                    Text = "Cam Toàn Cảnh:",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4B5563")),
                    FontSize = 12
                };
                grid1.Children.Add(lbl1);
                Grid.SetColumn(lbl1, 0);

                ComboBox cb1 = new ComboBox
                {
                    Height = 32,
                    IsEditable = true
                };
                
                var modernComboStyle = TryFindResource("ModernComboBox") as Style;
                if (modernComboStyle != null)
                {
                    cb1.Style = modernComboStyle;
                }

                grid1.Children.Add(cb1);
                Grid.SetColumn(cb1, 1);

                Button btnTest1 = new Button
                {
                    Content = "🔌 Xem thử",
                    Style = TryFindResource("ModernOutlineButton") as Style,
                    Height = 30,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 10,
                    Padding = new Thickness(2)
                };
                btnTest1.Click += (s, e) => TestCameraInline(cb1);
                grid1.Children.Add(btnTest1);
                Grid.SetColumn(btnTest1, 2);

                Button btnAdd1 = new Button
                {
                    Content = "➕ Thêm",
                    Style = TryFindResource("ModernButton") as Style,
                    Height = 30,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 10,
                    Padding = new Thickness(2),
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981")),
                    Foreground = System.Windows.Media.Brushes.White
                };
                btnAdd1.Click += (s, e) => AddCameraInline(cb1, lane.Id, "ToanCanh");
                grid1.Children.Add(btnAdd1);
                Grid.SetColumn(btnAdd1, 3);

                sp.Children.Add(grid1);

                // Row 2: Cam Biển Số
                Grid grid2 = new Grid();
                grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // Test button column
                grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); // Add button column

                TextBlock lbl2 = new TextBlock
                {
                    Text = "Cam Biển Số:",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4B5563")),
                    FontSize = 12
                };
                grid2.Children.Add(lbl2);
                Grid.SetColumn(lbl2, 0);

                ComboBox cb2 = new ComboBox
                {
                    Height = 32,
                    IsEditable = true
                };

                if (modernComboStyle != null)
                {
                    cb2.Style = modernComboStyle;
                }

                grid2.Children.Add(cb2);
                Grid.SetColumn(cb2, 1);

                Button btnTest2 = new Button
                {
                    Content = "🔌 Xem thử",
                    Style = TryFindResource("ModernOutlineButton") as Style,
                    Height = 30,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 10,
                    Padding = new Thickness(2)
                };
                btnTest2.Click += (s, e) => TestCameraInline(cb2);
                grid2.Children.Add(btnTest2);
                Grid.SetColumn(btnTest2, 2);

                Button btnAdd2 = new Button
                {
                    Content = "➕ Thêm",
                    Style = TryFindResource("ModernButton") as Style,
                    Height = 30,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 10,
                    Padding = new Thickness(2),
                    Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981")),
                    Foreground = System.Windows.Media.Brushes.White
                };
                btnAdd2.Click += (s, e) => AddCameraInline(cb2, lane.Id, "BienSo");
                grid2.Children.Add(btnAdd2);
                Grid.SetColumn(btnAdd2, 3);

                sp.Children.Add(grid2);

                border.Child = sp;

                // Populate combos
                cb1.Items.Add(AutoOption);
                cb2.Items.Add(AutoOption);
                for (int i = 0; i < _cameras.Count; i++)
                {
                    cb1.Items.Add(_cameras[i].Name);
                    cb2.Items.Add(_cameras[i].Name);
                }
                foreach (var dbCam in dbCameras)
                {
                    if (!string.IsNullOrEmpty(dbCam.CameraName))
                    {
                        if (dbCam.LaneId == null || dbCam.LaneId == lane.Id)
                        {
                            if (!cb1.Items.Contains(dbCam.CameraName)) cb1.Items.Add(dbCam.CameraName);
                            if (!cb2.Items.Contains(dbCam.CameraName)) cb2.Items.Add(dbCam.CameraName);
                        }
                    }
                }

                // Load saved values
                var savedSetting = _cfg.Cameras.LaneCameras?.FirstOrDefault(lc => lc.LaneId == lane.Id);
                string savedToanCanh = savedSetting?.ToanCanh ?? "";
                string savedBienSo = savedSetting?.BienSo ?? "";

                // Fallback to legacy settings by direction if no dynamic settings exist
                if (savedSetting == null)
                {
                    if (lane.Direction?.ToUpper() == "IN")
                    {
                        savedToanCanh = _cfg.Cameras.VaoToanCanh;
                        savedBienSo = _cfg.Cameras.VaoBienSo;
                    }
                    else
                    {
                        savedToanCanh = _cfg.Cameras.RaToanCanh;
                        savedBienSo = _cfg.Cameras.RaBienSo;
                    }
                }

                var match1 = FindCameraMatch(savedToanCanh);
                if (match1 != null) cb1.SelectedItem = match1;
                else if (!string.IsNullOrEmpty(savedToanCanh)) cb1.Text = savedToanCanh;
                else cb1.SelectedItem = AutoOption;

                var match2 = FindCameraMatch(savedBienSo);
                if (match2 != null) cb2.SelectedItem = match2;
                else if (!string.IsNullOrEmpty(savedBienSo)) cb2.Text = savedBienSo;
                else cb2.SelectedItem = AutoOption;

                _laneCameraCombos.Add((lane.Id, cb1, cb2));
                LaneCamerasContainer.Children.Add(border);
            }
        }

        private void TestCameraInline(ComboBox cb)
        {
            var selectedText = cb.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(selectedText) || selectedText == "(Tự động)")
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập một camera để xem thử!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string urlOrName = selectedText;
            if (_cameraNameToUrlMap.TryGetValue(selectedText, out var mappedUrl))
            {
                urlOrName = mappedUrl;
            }

            var win = new Views.AddCameraWindow(selectedText, urlOrName);
            win.Owner = this;
            win.ShowDialog();
        }

        private async void AddCameraInline(ComboBox cb, int laneId, string cameraRole)
        {
            var lane = _lanes?.FirstOrDefault(l => l.Id == laneId);
            var win = new Views.CameraSettingsWindow(laneId, cameraRole);
            win.Owner = this;
            win.ShowDialog();

            // Refresh the camera list and dropdowns
            int selectedGateId = ZoneCombo.SelectedValue != null ? Convert.ToInt32(ZoneCombo.SelectedValue) : 0;
            var lanesForGate = _lanes?.Where(l => l.GateId == selectedGateId).ToList() ?? new List<LaneConfig>();
            await PopulateLaneCamerasUIAsync(lanesForGate);

            // Auto-select the newly added camera for this lane/role
            try
            {
                var dbCameras = await ParkingTopologyService.Instance.GetCamerasAsync();
                string targetDirection = cameraRole == "ToanCanh" ? "Overview" : (lane?.Direction?.ToUpper() == "OUT" ? "Exit" : "Entry");
                var latestCam = dbCameras.Where(c => c.LaneId == laneId && c.Direction == targetDirection)
                                         .OrderByDescending(c => c.Id)
                                         .FirstOrDefault();
                if (latestCam != null)
                {
                    cb.Text = latestCam.CameraName;
                }
            }
            catch { }
        }

        private async void AddCamera_Click(object sender, RoutedEventArgs e)
        {
            var win = new Views.CameraSettingsWindow();
            win.Owner = this;
            win.ShowDialog();

            int selectedGateId = ZoneCombo.SelectedValue != null ? Convert.ToInt32(ZoneCombo.SelectedValue) : 0;
            var lanesForGate = _lanes?.Where(l => l.GateId == selectedGateId).ToList() ?? new List<LaneConfig>();
            await PopulateLaneCamerasUIAsync(lanesForGate);
        }

        private string NormalizeRtspUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                string normalized = url.ToLower().Trim();
                normalized = normalized.Replace("_password=", "password=")
                                       .Replace("&password=", "password=")
                                       .Replace("_channel=", "channel=")
                                       .Replace("&channel=", "channel=")
                                       .Replace("_stream=", "stream=")
                                       .Replace("&stream=", "stream=")
                                       .Replace("&onvif=", "onvif=")
                                       .Replace("_onvif=", "onvif=");
                return normalized;
            }
            catch { return url; }
        }

        private string? FindCameraMatch(string cfgValue)
        {
            if (string.IsNullOrEmpty(cfgValue)) return null;
            
            // Try exact match
            foreach (var kvp in _cameraNameToUrlMap)
            {
                if (kvp.Value.Equals(cfgValue, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            // Try normalized match
            string normCfg = NormalizeRtspUrl(cfgValue);
            foreach (var kvp in _cameraNameToUrlMap)
            {
                if (NormalizeRtspUrl(kvp.Value).Equals(normCfg, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            for (int i = 0; i < _cameras.Count; i++)
                if (_cameras[i].Name.Contains(cfgValue, StringComparison.OrdinalIgnoreCase))
                    return _cameras[i].Name;
            return null;
        }

        private string PickCamera(ComboBox cb)
        {
            var text = cb.Text?.Trim() ?? "";
            if (text == "(Tự động)" || string.IsNullOrEmpty(text)) return "";
            if (_cameraNameToUrlMap.TryGetValue(text, out var url))
            {
                return url;
            }
            return text;
        }


        private bool _isSyncingCombos = false;

        private void LoadReaderSelection()
        {
            var mappings = ReaderLaneMappingService.Instance.GetAllDraft();
 
            _isSyncingCombos = true;
 
            void BindReaderLane(int readerNo, ComboBox laneCombo, ComboBox vehicleTypeCombo)
            {
                var map = mappings.FirstOrDefault(m => m.ReaderNo == readerNo);
                if (map != null)
                {
                    laneCombo.SelectedValue = map.LaneId;
                }
            }

            BindReaderLane(1, R1LaneCombo, R1VehicleTypeCombo);
            BindReaderLane(2, R2LaneCombo, R2VehicleTypeCombo);
            BindReaderLane(3, R3LaneCombo, R3VehicleTypeCombo);
            BindReaderLane(4, R4LaneCombo, R4VehicleTypeCombo);
 
            _isSyncingCombos = false;
 
            // Sync vehicle type combos based on selected lane
            SyncVehicleTypeCombo(R1LaneCombo, R1VehicleTypeCombo);
            SyncVehicleTypeCombo(R2LaneCombo, R2VehicleTypeCombo);
            SyncVehicleTypeCombo(R3LaneCombo, R3VehicleTypeCombo);
            SyncVehicleTypeCombo(R4LaneCombo, R4VehicleTypeCombo);
 
            void BindReader(
                int readerNo,
                ComboBox dirCombo,
                CheckBox enableCheck)
            {
                var map = mappings
                    .FirstOrDefault(m => m.ReaderNo == readerNo);
 
                if (map != null)
                {
                    foreach (ComboBoxItem item in dirCombo.Items)
                    {
                        if (item.Tag?.ToString() == map.Direction)
                        {
                            dirCombo.SelectedItem = item;
                            break;
                        }
                    }
 
                    enableCheck.IsChecked = map.IsEnabled;
                }
                else
                {
                    dirCombo.SelectedIndex = 0;
                    enableCheck.IsChecked = true;
                }
            }
 
            BindReader(1, R1DirCombo, R1EnableCheck);
            BindReader(2, R2DirCombo, R2EnableCheck);
            BindReader(3, R3DirCombo, R3EnableCheck);
            BindReader(4, R4DirCombo, R4EnableCheck);
        }
 
        private void SetComboValue(ComboBox combo, string tag)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if ((combo.Items[i] as ComboBoxItem)?.Tag?.ToString() == tag)
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }
 
        private void R1LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isSyncingCombos) return;
            SyncVehicleTypeCombo(R1LaneCombo, R1VehicleTypeCombo);
        }

        private void R2LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isSyncingCombos) return;
            SyncVehicleTypeCombo(R2LaneCombo, R2VehicleTypeCombo);
        }

        private void R3LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isSyncingCombos) return;
            SyncVehicleTypeCombo(R3LaneCombo, R3VehicleTypeCombo);
        }

        private void R4LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isSyncingCombos) return;
            SyncVehicleTypeCombo(R4LaneCombo, R4VehicleTypeCombo);
        }
 
        private void SyncVehicleTypeCombo(ComboBox laneCombo, ComboBox vehicleTypeCombo)
        {
            if (laneCombo.SelectedItem is LaneConfig selectedLane)
            {
                if (selectedLane.LoaiXeId.HasValue && selectedLane.LoaiXeId.Value > 0)
                {
                    vehicleTypeCombo.SelectedValue = selectedLane.LoaiXeId.Value;
                }
                else
                {
                    vehicleTypeCombo.SelectedIndex = 0; // Hỗn hợp
                }
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                C3200Service.Instance.Configure(IpBox.Text,
                    int.TryParse(PortBox.Text, out var p) ? p : 4370,
                    PwdBox.Text,
                    int.TryParse(TimeoutBox.Text, out var t) ? t : 3000,
                    int.TryParse(BarrierBox.Text, out var b) ? b : 5);

                var ok = await C3200Service.Instance.ConnectAsync();
                if (ok)
                {
                    int lockCount = C3200Service.Instance.GetLockCount();
                    string typeMsg = "";
                    if (lockCount == 2)
                    {
                        typeMsg = "\n\nNhận diện thiết bị: C3-200 (2 rơ-le / cổng).";
                        UpdateControllerTypeSelection(ControllerType.C3200);
                    }
                    else if (lockCount == 4)
                    {
                        typeMsg = "\n\nNhận diện thiết bị: C3-400 (4 rơ-le / cổng).";
                        UpdateControllerTypeSelection(ControllerType.C3400);
                    }
                    else if (lockCount == 1)
                    {
                        typeMsg = "\n\nNhận diện thiết bị: C3-100 (1 rơ-le / cổng).";
                    }
                    else
                    {
                        typeMsg = $"\n\nKhông nhận dạng được dòng tủ (LockCount={lockCount}).";
                    }

                    MessageBox.Show($"Kết nối thành công!{typeMsg}", "Test kết nối", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Kết nối thất bại: {C3200Service.Instance.LastError}", "Test kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Test kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestConnectionDetailed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = IpBox.Text;
                int port = int.TryParse(PortBox.Text, out var p) ? p : 4370;
                string pwd = PwdBox.Text;
                int timeout = int.TryParse(TimeoutBox.Text, out var t) ? t : 3000;

                var res = Services.C3200Service.TestConnectDetailed(ip, port, pwd, timeout);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine(res.Success ? "Connection: SUCCESS" : "Connection: FAILED");
                sb.AppendLine($"SDK Error: {res.SdkError}");
                sb.AppendLine("Diagnostic:");
                sb.AppendLine(res.Diagnostic ?? "(no diagnostic)");
                sb.AppendLine("Params Tried:");
                foreach (var tparam in res.TriedParams)
                {
                    var masked = tparam;
                    masked = System.Text.RegularExpressions.Regex.Replace(masked, ",password=[^,]*", ",password=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    masked = System.Text.RegularExpressions.Regex.Replace(masked, ",passwd=[^,]*", ",passwd=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    sb.AppendLine(" - " + masked);
                }
                sb.AppendLine($"DLL Arch: {res.DllArch}");
                sb.AppendLine($"Process Arch: {(Environment.Is64BitProcess ? "x64" : "x86")} ");

                try { LoggingService.Instance.LogInfo("C3200TestDetailed", "C3200SettingsWindow", sb.ToString(), userId: Environment.UserName); } catch { }

                MessageBox.Show(sb.ToString(), "C3200 Detailed Test", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while testing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var prevMappings = ReaderLaneMappingService.Instance.GetAllDraft();
                var prevControllerType = _cfg.ZKTeco.ControllerType;
                var prevIp = _cfg.ZKTeco.IpAddress;
                var prevPort = _cfg.ZKTeco.TcpPort;
                var prevPwd = _cfg.ZKTeco.Password;
                var prevTimeout = _cfg.ZKTeco.Timeout;
                var prevBarrier = _cfg.ZKTeco.BarrierDuration;
                var prevCooldown = _cfg.ZKTeco.CardCooldownMs;
                var prevForceIn = _cfg.ZKTeco.ForceAllIn;
                var prevForceOut = _cfg.ZKTeco.ForceAllOut;
                var prevBtn1 = _cfg.ZKTeco.Button1Action;
                var prevBtn2 = _cfg.ZKTeco.Button2Action;

                var prevVehicleTypes = new Dictionary<int, (int? id, string name)>();
                void CapturePrevVehicleType(int readerNo, ComboBox laneCombo)
                {
                    if (laneCombo.SelectedItem is LaneConfig lane)
                    {
                        prevVehicleTypes[readerNo] = (lane.LoaiXeId, lane.LoaiXeName);
                    }
                    else
                    {
                        prevVehicleTypes[readerNo] = (null, "Hỗn hợp");
                    }
                }
                CapturePrevVehicleType(1, R1LaneCombo);
                CapturePrevVehicleType(2, R2LaneCombo);
                CapturePrevVehicleType(3, R3LaneCombo);
                CapturePrevVehicleType(4, R4LaneCombo);

                string targetIp = IpBox.Text;
                int targetPort = int.TryParse(PortBox.Text, out var p) ? p : _cfg.ZKTeco.TcpPort;
                string targetPwd = PwdBox.Text;
                int targetTimeout = int.TryParse(TimeoutBox.Text, out var t) ? t : _cfg.ZKTeco.Timeout;
                int targetBarrier = int.TryParse(BarrierBox.Text, out var b) ? b : _cfg.ZKTeco.BarrierDuration;
                int targetCooldown = int.TryParse(CooldownBox.Text, out var c) ? c : _cfg.ZKTeco.CardCooldownMs;

                // Configure service to test connection with new values
                C3200Service.Instance.Configure(targetIp, targetPort, targetPwd, targetTimeout, targetBarrier);

                ControllerType activeType = ControllerType.C3200; // default/fallback
                if (ControllerTypeCombo?.SelectedItem is ComboBoxItem typeItem &&
                    Enum.TryParse<ControllerType>(typeItem.Tag?.ToString(), out var parsedType))
                {
                    activeType = parsedType;
                }

                // Auto-detect actual model by connecting
                bool isConnected = await C3200Service.Instance.ConnectAsync();
                if (isConnected)
                {
                    int lockCount = C3200Service.Instance.GetLockCount();
                    if (lockCount == 2)
                    {
                        activeType = ControllerType.C3200;
                        UpdateControllerTypeSelection(ControllerType.C3200);
                    }
                    else if (lockCount == 4)
                    {
                        activeType = ControllerType.C3400;
                        UpdateControllerTypeSelection(ControllerType.C3400);
                    }
                }
                else
                {
                    var confirm = MessageBox.Show(
                        $"Không thể kết nối đến tủ điều khiển để tự động nhận dạng loại thiết bị.\n\nHệ thống sẽ tiếp tục kiểm tra và lưu cấu hình với loại tủ hiện tại: {activeType}.\n\nBạn có muốn tiếp tục lưu không?",
                        "Cảnh báo kết nối",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (confirm != MessageBoxResult.Yes)
                    {
                        return; // Abort save
                    }
                }

                _cfg.ZKTeco.IpAddress = targetIp;
                _cfg.ZKTeco.TcpPort = targetPort;
                _cfg.ZKTeco.Password = targetPwd;
                _cfg.ZKTeco.Timeout = targetTimeout;
                _cfg.ZKTeco.BarrierDuration = targetBarrier;
                _cfg.ZKTeco.CardCooldownMs = targetCooldown;

                // ForceMode obsolete
                _cfg.ZKTeco.ForceAllIn = false;
                _cfg.ZKTeco.ForceAllOut = false;

                var b1 = this.FindName("Button1ActionCombo") as ComboBox;
                var b2 = this.FindName("Button2ActionCombo") as ComboBox;
                if (b1?.SelectedItem is ComboBoxItem bi1) _cfg.ZKTeco.Button1Action = bi1.Tag?.ToString() ?? _cfg.ZKTeco.Button1Action;
                if (b2?.SelectedItem is ComboBoxItem bi2) _cfg.ZKTeco.Button2Action = bi2.Tag?.ToString() ?? _cfg.ZKTeco.Button2Action;

                // save reader mappings
                var newMappings = new List<ReaderLaneMapping>();

                void ExtractReaderMap(int readerNo, ComboBox laneCombo, ComboBox dirCombo, CheckBox enableCheck)
                {
                    int mappedLane = readerNo; // safe default
                    if (laneCombo.SelectedValue is int selVal)
                    {
                        mappedLane = selVal;
                    }
                    else if (laneCombo.SelectedValue != null && int.TryParse(laneCombo.SelectedValue.ToString(), out var parsed))
                    {
                        mappedLane = parsed;
                    }

                    string direction = "IN"; // safe default
                    if (dirCombo.SelectedItem is ComboBoxItem dirItem)
                    {
                        direction = dirItem.Tag?.ToString() ?? "IN";
                    }

                    newMappings.Add(new ReaderLaneMapping
                    {
                        ReaderNo = readerNo,
                        LaneId = mappedLane,
                        Direction = direction,
                        IsEnabled = enableCheck.IsChecked == true
                    });
                }

                ExtractReaderMap(1, R1LaneCombo, R1DirCombo, R1EnableCheck);
                ExtractReaderMap(2, R2LaneCombo, R2DirCombo, R2EnableCheck);
                ExtractReaderMap(3, R3LaneCombo, R3DirCombo, R3EnableCheck);
                ExtractReaderMap(4, R4LaneCombo, R4DirCombo, R4EnableCheck);

                var validationResult = ControllerLaneValidationService.Instance.ValidateLaneCapacity(activeType, newMappings);
                if (!validationResult.IsValid)
                {
                    MessageBox.Show(validationResult.ErrorMessage, "Lỗi cấu hình làn", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // Block save
                }

                _cfg.ZKTeco.ControllerType = activeType;
                ReaderLaneMappingService.Instance.UpdateDraftMappings(newMappings);

                // Auto-sync lane direction based on consistency analysis
                try
                {
                    var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                    foreach (var lane in lanes)
                    {
                        var analysis = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(lane.Id);
                        var currentDir = lane.Direction.ToLaneDirection();
                        if (!analysis.AllowedDirections.Contains(currentDir) && analysis.IsConsistent && analysis.AutoDirection.HasValue)
                        {
                            string autoDirStr = analysis.AutoDirection.Value.ToDbString();
                            lane.Direction = autoDirStr;
                            await ParkingTopologyService.Instance.SaveLaneAsync(lane);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try { LoggingService.Instance.LogError("Save_Click_AutoSync", "C3200Settings", "Failed to auto-sync lane directions", ex); } catch { }
                }

                // Save vehicle type to lanes
                try
                {
                    await SaveLaneVehicleType(R1LaneCombo, R1VehicleTypeCombo);
                    await SaveLaneVehicleType(R2LaneCombo, R2VehicleTypeCombo);
                    await SaveLaneVehicleType(R3LaneCombo, R3VehicleTypeCombo);
                    await SaveLaneVehicleType(R4LaneCombo, R4VehicleTypeCombo);
                }
                catch (Exception vtEx)
                {
                    try { LoggingService.Instance.LogError("SaveVehicleType", "C3200Settings", "Failed to save lane vehicle type", vtEx); } catch { }
                }

                var changes = new System.Text.StringBuilder();
                var auditTasks = new List<Task>();
                void AddChange(string name, object oldV, object newV)
                {
                    string oldStr = oldV?.ToString() ?? string.Empty;
                    string newStr = newV?.ToString() ?? string.Empty;
                    if (oldStr != newStr)
                    {
                        if (changes.Length > 0) changes.Append("; ");
                        changes.Append($"{name}: '{oldStr}' -> '{newStr}'");
                        auditTasks.Add(ConfigurationAuditService.Instance.RecordChangeAsync(
                            "Controller Config",
                            "ZKTeco Settings",
                            name,
                            oldStr,
                            newStr
                        ));
                    }
                }

                AddChange("Loại thiết bị", prevControllerType, _cfg.ZKTeco.ControllerType);
                AddChange("IpAddress", prevIp, _cfg.ZKTeco.IpAddress);
                AddChange("TcpPort", prevPort, _cfg.ZKTeco.TcpPort);
                AddChange("Password", string.IsNullOrEmpty(prevPwd) ? "(empty)" : "(redacted)", string.IsNullOrEmpty(_cfg.ZKTeco.Password) ? "(empty)" : "(redacted)");
                AddChange("Timeout", prevTimeout, _cfg.ZKTeco.Timeout);
                AddChange("BarrierDuration", prevBarrier, _cfg.ZKTeco.BarrierDuration);
                AddChange("CardCooldownMs", prevCooldown, _cfg.ZKTeco.CardCooldownMs);
                AddChange("ForceAllIn", prevForceIn, _cfg.ZKTeco.ForceAllIn);
                AddChange("ForceAllOut", prevForceOut, _cfg.ZKTeco.ForceAllOut);
                AddChange("Button1Action", prevBtn1, _cfg.ZKTeco.Button1Action);
                AddChange("Button2Action", prevBtn2, _cfg.ZKTeco.Button2Action);

                if (auditTasks.Count > 0)
                {
                    await Task.WhenAll(auditTasks);
                }

                // Check for reader mapping changes
                for (int r = 1; r <= 4; r++)
                {
                    var prevMap = prevMappings.FirstOrDefault(m => m.ReaderNo == r);
                    var newMap = newMappings.FirstOrDefault(m => m.ReaderNo == r);

                    if (newMap != null)
                    {
                        bool isMapChanged = false;
                        if (prevMap == null)
                        {
                            isMapChanged = true;
                        }
                        else if (prevMap.LaneId != newMap.LaneId ||
                                 prevMap.Direction != newMap.Direction ||
                                 prevMap.IsEnabled != newMap.IsEnabled)
                        {
                            isMapChanged = true;
                        }

                        if (isMapChanged)
                        {
                            string oldMapStr = prevMap != null 
                                ? $"Làn {prevMap.LaneId} ({prevMap.Direction}, {(prevMap.IsEnabled ? "Bật" : "Tắt")})" 
                                : "Chưa cấu hình";
                            string newMapStr = $"Làn {newMap.LaneId} ({newMap.Direction}, {(newMap.IsEnabled ? "Bật" : "Tắt")})";
                            
                            if (changes.Length > 0) changes.Append("; ");
                            changes.Append($"Đầu đọc {r}: {oldMapStr} -> {newMapStr}");
                        }
                    }
                }

                // Check for vehicle type changes
                void CheckVehicleTypeChange(int readerNo, ComboBox laneCombo, ComboBox vehicleTypeCombo)
                {
                    if (laneCombo.SelectedItem is LaneConfig lane && vehicleTypeCombo.SelectedItem is LoaiXe selectedType)
                    {
                        int? newLoaiXeId = selectedType.Id > 0 ? selectedType.Id : (int?)null;
                        string newLoaiXeName = selectedType.Id > 0 ? selectedType.TenLoai : "Hỗn hợp";
                        
                        if (prevVehicleTypes.TryGetValue(readerNo, out var prevVal))
                        {
                            if (prevVal.id != newLoaiXeId)
                            {
                                string oldName = string.IsNullOrEmpty(prevVal.name) ? "Hỗn hợp" : prevVal.name;
                                if (changes.Length > 0) changes.Append("; ");
                                changes.Append($"Loại xe Làn {lane.LaneCode} (Đầu đọc {readerNo}): '{oldName}' -> '{newLoaiXeName}'");
                            }
                        }
                    }
                }
                CheckVehicleTypeChange(1, R1LaneCombo, R1VehicleTypeCombo);
                CheckVehicleTypeChange(2, R2LaneCombo, R2VehicleTypeCombo);
                CheckVehicleTypeChange(3, R3LaneCombo, R3VehicleTypeCombo);
                CheckVehicleTypeChange(4, R4LaneCombo, R4VehicleTypeCombo);

                // Validate no camera is assigned to multiple lanes (including database assignments of other gates)
                try
                {
                    var dbCamerasForValidation = await ParkingTopologyService.Instance.GetCamerasAsync();
                    var currentLaneIdsForValidation = _laneCameraCombos.Select(item => item.LaneId).ToHashSet();
                    var assignedCameras = new Dictionary<string, int>();

                    // Populate assigned cameras with database assignments of lanes NOT in current configuration
                    foreach (var dbCam in dbCamerasForValidation)
                    {
                        if (dbCam.LaneId.HasValue && !currentLaneIdsForValidation.Contains(dbCam.LaneId.Value))
                        {
                            if (!string.IsNullOrEmpty(dbCam.RtspUrl))
                            {
                                assignedCameras[dbCam.RtspUrl] = dbCam.LaneId.Value;
                            }
                            if (!string.IsNullOrEmpty(dbCam.CameraName))
                            {
                                assignedCameras[dbCam.CameraName] = dbCam.LaneId.Value;
                            }
                        }
                    }

                    foreach (var item in _laneCameraCombos)
                    {
                        var toanCanh = PickCamera(item.cbToanCanh);
                        var bienSo = PickCamera(item.cbBienSo);

                        if (!string.IsNullOrEmpty(toanCanh))
                        {
                            if (assignedCameras.TryGetValue(toanCanh, out var otherLaneId) && otherLaneId != item.LaneId)
                            {
                                var otherLane = _lanes?.FirstOrDefault(l => l.Id == otherLaneId);
                                var currentLane = _lanes?.FirstOrDefault(l => l.Id == item.LaneId);
                                MessageBox.Show($"Camera '{item.cbToanCanh.Text}' đã được gán cho làn '{otherLane?.LaneName ?? otherLaneId.ToString()}'. Không thể gán cho làn '{currentLane?.LaneName ?? item.LaneId.ToString()}'.", "Lỗi cấu hình camera", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            assignedCameras[toanCanh] = item.LaneId;
                            if (!string.IsNullOrEmpty(item.cbToanCanh.Text))
                            {
                                assignedCameras[item.cbToanCanh.Text] = item.LaneId;
                            }
                        }

                        if (!string.IsNullOrEmpty(bienSo))
                        {
                            if (assignedCameras.TryGetValue(bienSo, out var otherLaneId) && otherLaneId != item.LaneId)
                            {
                                var otherLane = _lanes?.FirstOrDefault(l => l.Id == otherLaneId);
                                var currentLane = _lanes?.FirstOrDefault(l => l.Id == item.LaneId);
                                MessageBox.Show($"Camera '{item.cbBienSo.Text}' đã được gán cho làn '{otherLane?.LaneName ?? otherLaneId.ToString()}'. Không thể gán cho làn '{currentLane?.LaneName ?? item.LaneId.ToString()}'.", "Lỗi cấu hình camera", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            assignedCameras[bienSo] = item.LaneId;
                            if (!string.IsNullOrEmpty(item.cbBienSo.Text))
                            {
                                assignedCameras[item.cbBienSo.Text] = item.LaneId;
                            }
                        }
                    }
                }
                catch (Exception valEx)
                {
                    try { LoggingService.Instance.LogError("Save_Click_CameraValidation", "C3200Settings", "Failed to validate camera assignments", valEx); } catch { }
                }

                // Save lane camera assignments
                _cfg.Cameras.LaneCameras.Clear();
                foreach (var item in _laneCameraCombos)
                {
                    _cfg.Cameras.LaneCameras.Add(new LaneCameraSetting
                    {
                        LaneId = item.LaneId,
                        ToanCanh = PickCamera(item.cbToanCanh),
                        BienSo = PickCamera(item.cbBienSo)
                    });
                }

                // Keep legacy fallback settings updated for backward compatibility
                var firstInLane = _laneCameraCombos.FirstOrDefault(x => {
                    var l = _lanes?.FirstOrDefault(lane => lane.Id == x.LaneId);
                    return l?.Direction?.ToUpper() == "IN";
                });
                var firstOutLane = _laneCameraCombos.FirstOrDefault(x => {
                    var l = _lanes?.FirstOrDefault(lane => lane.Id == x.LaneId);
                    return l?.Direction?.ToUpper() == "OUT";
                });

                if (firstInLane != default)
                {
                    _cfg.Cameras.VaoToanCanh = PickCamera(firstInLane.cbToanCanh);
                    _cfg.Cameras.VaoBienSo = PickCamera(firstInLane.cbBienSo);
                }
                if (firstOutLane != default)
                {
                    _cfg.Cameras.RaToanCanh = PickCamera(firstOutLane.cbToanCanh);
                    _cfg.Cameras.RaBienSo = PickCamera(firstOutLane.cbBienSo);
                }

                _cfg.SaveDraft();

                // Sync camera assignments to SQLite database
                try
                {
                    var dbCameras = await ParkingTopologyService.Instance.GetCamerasAsync();
                    var currentLaneIds = _laneCameraCombos.Select(item => item.LaneId).ToHashSet();

                    foreach (var dbCam in dbCameras)
                    {
                        // Check if this database camera is assigned in the current gate configuration UI
                        var assignedLaneCombo = _laneCameraCombos.FirstOrDefault(item => 
                            (!string.IsNullOrEmpty(dbCam.RtspUrl) && PickCamera(item.cbToanCanh) == dbCam.RtspUrl) ||
                            (!string.IsNullOrEmpty(dbCam.CameraName) && item.cbToanCanh.Text == dbCam.CameraName) ||
                            (!string.IsNullOrEmpty(dbCam.RtspUrl) && PickCamera(item.cbBienSo) == dbCam.RtspUrl) ||
                            (!string.IsNullOrEmpty(dbCam.CameraName) && item.cbBienSo.Text == dbCam.CameraName)
                        );

                        if (assignedLaneCombo.cbToanCanh != null)
                        {
                            // Assigned to a lane in the UI
                            int targetLaneId = assignedLaneCombo.LaneId;
                            string targetDirection;
                            
                            if ((!string.IsNullOrEmpty(dbCam.RtspUrl) && PickCamera(assignedLaneCombo.cbToanCanh) == dbCam.RtspUrl) ||
                                (!string.IsNullOrEmpty(dbCam.CameraName) && assignedLaneCombo.cbToanCanh.Text == dbCam.CameraName))
                            {
                                targetDirection = "Overview";
                            }
                            else
                            {
                                var lane = _lanes?.FirstOrDefault(l => l.Id == targetLaneId);
                                targetDirection = lane?.Direction?.ToUpper() == "OUT" ? "Exit" : "Entry";
                            }

                            if (dbCam.LaneId != targetLaneId || dbCam.Direction != targetDirection)
                            {
                                dbCam.LaneId = targetLaneId;
                                dbCam.Direction = targetDirection;
                                await ParkingTopologyService.Instance.SaveCameraAsync(dbCam);
                            }
                        }
                        else
                        {
                            // Not assigned in the current gate configuration UI
                            // If it was previously assigned to one of the lanes currently configured, we unassign it
                            if (dbCam.LaneId.HasValue && currentLaneIds.Contains(dbCam.LaneId.Value))
                            {
                                dbCam.LaneId = null;
                                await ParkingTopologyService.Instance.SaveCameraAsync(dbCam);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    try { LoggingService.Instance.LogError("Save_Click_CameraSync", "C3200Settings", "Failed to sync camera assignments to DB", ex); } catch { }
                }

                // Restore active configuration in the service since we only saved to draft
                try
                {
                    var activeConfig = AppConfig.Load();
                    C3200Service.Instance.Configure(
                        activeConfig.ZKTeco.IpAddress,
                        activeConfig.ZKTeco.TcpPort,
                        activeConfig.ZKTeco.Password,
                        activeConfig.ZKTeco.Timeout,
                        activeConfig.ZKTeco.BarrierDuration
                    );
                    _ = Task.Run(async () =>
                    {
                        try { await C3200Service.Instance.ConnectAsync(); } catch { }
                    });
                }
                catch (Exception ex)
                {
                    try { LoggingService.Instance.LogError("Save_Click_RestoreActive", "C3200Settings", "Failed to restore active controller config", ex); } catch { }
                }

                if (changes.Length > 0)
                {
                    string activeSiteName = (SiteCombo.SelectedItem as ParkingSite)?.SiteName ?? SiteCombo.Text ?? "(Chưa chọn)";
                    string activeGateName = (ZoneCombo.SelectedItem as ParkingGate)?.GateName ?? ZoneCombo.Text ?? "(Chưa chọn)";
                    string activeControllerName = (TopologyCombo.SelectedItem as C3ControllerConfig)?.ControllerName ?? TopologyCombo.Text ?? "(Chưa chọn)";
                    string auditDetails = $"Cấu hình tủ ZKTeco thay đổi tại Site: '{activeSiteName}', Cổng: '{activeGateName}', Controller: '{activeControllerName}'. Chi tiết thay đổi: {changes}";

                    try 
                    { 
                        LoggingService.Instance.LogAudit(
                            "CONFIG_CHANGED_UI", 
                            "C3200Settings", 
                            "config_draft.json", 
                            null, 
                            new { Diffs = changes.ToString() }, 
                            source: "C3200SettingsWindow", 
                            details: auditDetails
                        ); 
                    } 
                    catch { }
                }

                // Mark pending changes in deployment service so Development Center knows there are new changes
                try
                {
                    await DeploymentService.Instance.MarkPendingChangesAsync();
                }
                catch (Exception depEx)
                {
                    try { LoggingService.Instance.LogError("Save_Click_MarkPending", "C3200Settings", "Failed to mark pending changes", depEx); } catch { }
                }

                await RefreshSiteSelectionAsync();

                MessageBox.Show("Đã lưu cấu hình vào bản nháp thành công!\n\nLưu ý: Cấu hình mới chưa được áp dụng ngay vào ứng dụng hiện tại. Bạn cần vào mục Development Center (Trung tâm phát triển) để tiến hành triển khai (Deploy) thì cấu hình mới có hiệu lực.", "Lưu cấu hình", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { LoggingService.Instance.LogError("Save_Click", "C3200Settings", "Crash during save", ex); } catch { }
                MessageBox.Show($"Lỗi khi lưu cấu hình: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        

        private async Task RefreshSiteSelectionAsync()
        {
            try
            {
                _isInitializing = true;

                var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();

                var activeController = controllers.FirstOrDefault(c => c.IpAddress == _cfg.ZKTeco.IpAddress);
                ParkingSite activeSite = null;
                ParkingGate activeGate = null;

                if (activeController != null)
                {
                    activeGate = gates.FirstOrDefault(g => g.Id == activeController.GateId);
                    if (activeGate != null)
                    {
                        activeSite = sites.FirstOrDefault(s => s.Id == activeGate.SiteId);
                    }
                }
                if (activeSite == null && sites.Any())
                {
                    activeSite = sites.First();
                }

                // Update Site combo
                SiteCombo.ItemsSource = sites;
                if (activeSite != null)
                {
                    SiteCombo.SelectedValue = activeSite.Id;
                }
                else if (sites.Any())
                {
                    SiteCombo.SelectedIndex = 0;
                }

                // Manually cascade: update Gate combo for selected site
                int selectedSiteId = SiteCombo.SelectedValue != null ? Convert.ToInt32(SiteCombo.SelectedValue) : 0;
                var gatesForSite = gates.Where(g => g.SiteId == selectedSiteId).ToList();
                ZoneCombo.ItemsSource = gatesForSite;
                ZoneCombo.DisplayMemberPath = "GateName";
                ZoneCombo.SelectedValuePath = "Id";

                if (activeGate != null && gatesForSite.Any(g => g.Id == activeGate.Id))
                {
                    ZoneCombo.SelectedValue = activeGate.Id;
                }
                else if (gatesForSite.Any())
                {
                    ZoneCombo.SelectedIndex = 0;
                }

                // Manually cascade: update Controller combo for selected gate
                int selectedGateId = ZoneCombo.SelectedValue != null ? Convert.ToInt32(ZoneCombo.SelectedValue) : 0;
                var controllersForGate = controllers.Where(c => c.GateId == selectedGateId).ToList();
                TopologyCombo.ItemsSource = controllersForGate;
                TopologyCombo.DisplayMemberPath = "ControllerName";
                TopologyCombo.SelectedValuePath = "Id";

                if (activeController != null && controllersForGate.Any(c => c.Id == activeController.Id))
                {
                    TopologyCombo.SelectedValue = activeController.Id;
                }
                else if (controllersForGate.Any())
                {
                    TopologyCombo.SelectedIndex = 0;
                }

                // Manually cascade: update Lane combos for selected gate
                var allLanes = await ParkingTopologyService.Instance.GetLanesAsync();
                var lanesForGate = allLanes.Where(l => l.GateId == selectedGateId).ToList();

                R1LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);
                R2LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);
                R3LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);
                R4LaneCombo.ItemsSource = new List<LaneConfig>(lanesForGate);

                // Reload reader selections
                LoadReaderSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh site selection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isInitializing = false;
            }
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Restore active config to service when window is closed
            try
            {
                var activeConfig = AppConfig.Load();
                C3200Service.Instance.Configure(
                    activeConfig.ZKTeco.IpAddress,
                    activeConfig.ZKTeco.TcpPort,
                    activeConfig.ZKTeco.Password,
                    activeConfig.ZKTeco.Timeout,
                    activeConfig.ZKTeco.BarrierDuration
                );
                
                _ = Task.Run(async () =>
                {
                    try { await C3200Service.Instance.ConnectAsync(); } catch { }
                });
            }
            catch { }
        }

        private async Task SaveLaneVehicleType(ComboBox laneCombo, ComboBox vehicleTypeCombo)
        {
            if (laneCombo.SelectedItem is LaneConfig lane && vehicleTypeCombo.SelectedItem is LoaiXe selectedType)
            {
                int? newLoaiXeId = selectedType.Id > 0 ? selectedType.Id : (int?)null;
                
                // Only save if changed
                if (lane.LoaiXeId != newLoaiXeId)
                {
                    lane.LoaiXeId = newLoaiXeId;
                    lane.LoaiXeName = selectedType.Id > 0 ? selectedType.TenLoai : string.Empty;
                    await ParkingTopologyService.Instance.SaveLaneAsync(lane);
                    LoggingService.Instance.LogInfo("SaveVehicleType", "C3200Settings", 
                        $"Lane {lane.LaneCode} LoaiXeId set to {(newLoaiXeId.HasValue ? newLoaiXeId.Value.ToString() : "NULL (Mixed)")}");
                }
            }
        }
    }
}
