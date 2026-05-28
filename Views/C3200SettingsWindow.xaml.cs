using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyGiuXe
{
    public partial class C3200SettingsWindow : Window
    {
        private AppConfig _cfg;
        private bool _isInitializing = false;
        private List<LaneConfig> _lanes;
        private List<ParkingSite> _sites;
        private List<LoaiXe> _vehicleTypes;

        public C3200SettingsWindow()
        {
            InitializeComponent();
            _cfg = AppConfig.Load();

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
                Door1LaneCombo.ItemsSource = lanesForGate;
                Door2LaneCombo.ItemsSource = lanesForGate;
 
                Door1LaneCombo.DisplayMemberPath = "LaneName";
                Door1LaneCombo.SelectedValuePath = "Id";
 
                Door2LaneCombo.DisplayMemberPath = "LaneName";
                Door2LaneCombo.SelectedValuePath = "Id";
 
                // 5. Load vehicle types for lane-type combos
                _vehicleTypes = new DatabaseService().GetLoaiXe();
                var mixedItem = new LoaiXe { Id = 0, TenLoai = "🔀 Hỗn hợp (tất cả)", TrangThai = "Active" };
                var door1VtList = new List<LoaiXe> { mixedItem };
                door1VtList.AddRange(_vehicleTypes);
                var door2VtList = new List<LoaiXe> { mixedItem };
                door2VtList.AddRange(_vehicleTypes);
                Door1VehicleTypeCombo.ItemsSource = door1VtList;
                Door2VehicleTypeCombo.ItemsSource = door2VtList;
                Door1VehicleTypeCombo.SelectedIndex = 0;
                Door2VehicleTypeCombo.SelectedIndex = 0;

                // Load reader mappings (saved selections)
                LoadReaderSelection();
 
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

                Door1LaneCombo.ItemsSource = lanes;
                Door2LaneCombo.ItemsSource = lanes;

                Door1LaneCombo.DisplayMemberPath = "LaneName";
                Door1LaneCombo.SelectedValuePath = "Id";

                Door2LaneCombo.DisplayMemberPath = "LaneName";
                Door2LaneCombo.SelectedValuePath = "Id";

                LoadReaderSelection();
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
            MessageBox.Show("Đã reset về mặc định", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private bool _isSyncingCombos = false;

        private void LoadReaderSelection()
        {
            var mappings = ReaderLaneMappingService.Instance.GetAll();

            _isSyncingCombos = true;

            // Reader 1-2 = Door 1
            var door1Mapping = mappings
                .FirstOrDefault(m => m.ReaderNo == 1);

            if (door1Mapping != null)
            {
                Door1LaneCombo.SelectedValue = door1Mapping.LaneId;
            }

            // Reader 3-4 = Door 2
            var door2Mapping = mappings
                .FirstOrDefault(m => m.ReaderNo == 3);

            if (door2Mapping != null)
            {
                Door2LaneCombo.SelectedValue = door2Mapping.LaneId;
            }

            _isSyncingCombos = false;

            // Sync vehicle type combos based on selected lane
            SyncVehicleTypeCombo(Door1LaneCombo, Door1VehicleTypeCombo);
            SyncVehicleTypeCombo(Door2LaneCombo, Door2VehicleTypeCombo);

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

        private void Door1LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isSyncingCombos)
                return;

            if (Door1LaneCombo.SelectedValue == null)
                return;

            int door1Lane =
                Convert.ToInt32(Door1LaneCombo.SelectedValue);

            _isSyncingCombos = true;

            foreach (var item in Door2LaneCombo.Items)
            {
                dynamic lane = item;

                if (lane.Id != door1Lane)
                {
                    Door2LaneCombo.SelectedItem = item;
                    break;
                }
            }

            _isSyncingCombos = false;

            // Sync vehicle type for Door1
            SyncVehicleTypeCombo(Door1LaneCombo, Door1VehicleTypeCombo);
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

        private void Door2LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isSyncingCombos)
                return;

            if (Door2LaneCombo.SelectedValue == null)
                return;

            int door2Lane =
                Convert.ToInt32(Door2LaneCombo.SelectedValue);

            _isSyncingCombos = true;

            foreach (var item in Door1LaneCombo.Items)
            {
                dynamic lane = item;    

                if (lane.Id != door2Lane)
                {
                    Door1LaneCombo.SelectedItem = item;
                    break;
                }
            }

            _isSyncingCombos = false;

            // Sync vehicle type for Door2
            SyncVehicleTypeCombo(Door2LaneCombo, Door2VehicleTypeCombo);
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
                MessageBox.Show(ok ? "Kết nối thành công" : $"Kết nối thất bại: {C3200Service.Instance.LastError}", "Test kết nối");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Test kết nối");
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

            _cfg.ZKTeco.IpAddress = IpBox.Text;
            _cfg.ZKTeco.TcpPort = int.TryParse(PortBox.Text, out var p) ? p : _cfg.ZKTeco.TcpPort;
            _cfg.ZKTeco.Password = PwdBox.Text;
            _cfg.ZKTeco.Timeout = int.TryParse(TimeoutBox.Text, out var t) ? t : _cfg.ZKTeco.Timeout;
            _cfg.ZKTeco.BarrierDuration = int.TryParse(BarrierBox.Text, out var b) ? b : _cfg.ZKTeco.BarrierDuration;
            _cfg.ZKTeco.CardCooldownMs = int.TryParse(CooldownBox.Text, out var c) ? c : _cfg.ZKTeco.CardCooldownMs;
            
            // ForceMode obsolete
            _cfg.ZKTeco.ForceAllIn = false;
            _cfg.ZKTeco.ForceAllOut = false;

            var b1 = this.FindName("Button1ActionCombo") as ComboBox;
            var b2 = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1?.SelectedItem is ComboBoxItem bi1) _cfg.ZKTeco.Button1Action = bi1.Tag?.ToString() ?? _cfg.ZKTeco.Button1Action;
            if (b2?.SelectedItem is ComboBoxItem bi2) _cfg.ZKTeco.Button2Action = bi2.Tag?.ToString() ?? _cfg.ZKTeco.Button2Action;

            // save reader mappings
            int laneForDoor1 = 1;
            if (Door1LaneCombo.SelectedValue != null)
            {
                laneForDoor1 = (int)Door1LaneCombo.SelectedValue;
            }
            int laneForDoor2 = 1;
            if (Door2LaneCombo.SelectedValue != null)
            {
                laneForDoor2 = (int)Door2LaneCombo.SelectedValue;
            }
            else
            {
                laneForDoor2 = (laneForDoor1 == 1) ? 2 : 1;
            }

            var newMappings = new List<ReaderLaneMapping>();
            
            void ExtractReaderMap(int readerNo, int mappedLane, ComboBox dirCombo, CheckBox enableCheck)
            {
                newMappings.Add(new ReaderLaneMapping
                {
                    ReaderNo = readerNo,
                    LaneId = mappedLane,
                    Direction = ((ComboBoxItem)dirCombo.SelectedItem)?.Tag?.ToString() ?? "IN",
                    IsEnabled = enableCheck.IsChecked == true
                });
            }

            ExtractReaderMap(1, laneForDoor1, R1DirCombo, R1EnableCheck);
            ExtractReaderMap(2, laneForDoor1, R2DirCombo, R2EnableCheck);
            ExtractReaderMap(3, laneForDoor2, R3DirCombo, R3EnableCheck);
            ExtractReaderMap(4, laneForDoor2, R4DirCombo, R4EnableCheck);

            ReaderLaneMappingService.Instance.UpdateMappings(newMappings);

            // Save vehicle type to lanes (async fire-and-forget)
            try
            {
                await SaveLaneVehicleType(Door1LaneCombo, Door1VehicleTypeCombo);
                await SaveLaneVehicleType(Door2LaneCombo, Door2VehicleTypeCombo);
            }
            catch (Exception vtEx)
            {
                try { LoggingService.Instance.LogError("SaveVehicleType", "C3200Settings", "Failed to save lane vehicle type", vtEx); } catch { }
            }

            try
            {
                var changes = new System.Text.StringBuilder();
                void AddChange(string name, object oldV, object newV)
                {
                    if ((oldV?.ToString() ?? string.Empty) != (newV?.ToString() ?? string.Empty))
                    {
                        if (changes.Length > 0) changes.Append("; ");
                        changes.Append($"{name}: '{oldV}' -> '{newV}'");
                    }
                }

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
                AddChange("ReaderMappings", "updated", "updated");

                _cfg.Save();

                C3200Service.Instance.Configure(_cfg.ZKTeco.IpAddress, _cfg.ZKTeco.TcpPort,
                    _cfg.ZKTeco.Password, _cfg.ZKTeco.Timeout, _cfg.ZKTeco.BarrierDuration);

                // Reset the connection monitor status cache to discard the old IP address cache immediately
                ConnectionMonitorService.Instance.ResetState();

                // Reconnect to C3-200 Controller immediately in the background with the new configuration
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await C3200Service.Instance.ConnectAsync();
                    }
                    catch (Exception connEx)
                    {
                        try { LoggingService.Instance.LogError("ConfigChangeReconnect", "C3200Settings", "Failed to reconnect to C3200 after config change", connEx); } catch { }
                    }
                });

                if (changes.Length > 0)
                {
                    try { LoggingService.Instance.LogAudit("CONFIG_CHANGED_UI", "C3200Settings", "config.json", null, new { Diffs = changes.ToString() }, source: "C3200SettingsWindow", details: $"Config updated via UI: {changes}"); } catch { }
                }

                await RefreshSiteSelectionAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("Saved", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        

        private async Task RefreshSiteSelectionAsync()
        {
            try
            {
                var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();

                var activeController = controllers.FirstOrDefault(c => c.IpAddress == _cfg.ZKTeco.IpAddress);
                ParkingSite activeSite = null;
                if (activeController != null)
                {
                    var activeGate = gates.FirstOrDefault(g => g.Id == activeController.GateId);
                    if (activeGate != null)
                    {
                        activeSite = sites.FirstOrDefault(s => s.Id == activeGate.SiteId);
                    }
                }
                if (activeSite == null && sites.Any())
                {
                    activeSite = sites.First();
                }

                if (activeSite != null)
                {
                    SiteCombo.SelectedValue = activeSite.Id;
                }
                else if (sites.Any())
                {
                    SiteCombo.SelectedIndex = 0;
                }
                // SelectionChanged will fire automatically
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh site selection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

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
