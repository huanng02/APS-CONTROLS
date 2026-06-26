using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe.ViewModels
{
    public class ParkingTopologyViewModel : BaseViewModel
    {
        // ──────────────────────────────────────────────
        // EXISTING collections (preserved as-is)
        // ──────────────────────────────────────────────
        public ObservableCollection<ParkingSite> Sites { get; set; } = new ObservableCollection<ParkingSite>();
        public ObservableCollection<ParkingGate> Gates { get; set; } = new ObservableCollection<ParkingGate>();
        public ObservableCollection<ParkingZone> Zones { get; set; } = new ObservableCollection<ParkingZone>();
        public ObservableCollection<LaneConfig> Lanes { get; set; } = new ObservableCollection<LaneConfig>();
        public ObservableCollection<C3ControllerConfig> Controllers { get; set; } = new ObservableCollection<C3ControllerConfig>();

        // ──────────────────────────────────────────────
        // EXISTING selected items (preserved as-is)
        // ──────────────────────────────────────────────
        private ParkingSite _selectedSite;
        public ParkingSite SelectedSite
        {
            get => _selectedSite;
            set { _selectedSite = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private ParkingGate _selectedGate;
        public ParkingGate SelectedGate
        {
            get => _selectedGate;
            set { _selectedGate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private ParkingZone _selectedZone;
        public ParkingZone SelectedZone
        {
            get => _selectedZone;
            set { _selectedZone = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private LaneConfig _selectedLane;
        public LaneConfig SelectedLane
        {
            get => _selectedLane;
            set { _selectedLane = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private C3ControllerConfig _selectedController;
        public C3ControllerConfig SelectedController
        {
            get => _selectedController;
            set { _selectedController = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // ──────────────────────────────────────────────
        // NEW: Tree & UI State
        // ──────────────────────────────────────────────
        public ObservableCollection<TopologyTreeNode> TreeNodes { get; set; } = new ObservableCollection<TopologyTreeNode>();

        private TopologyTreeNode _selectedNode;
        public TopologyTreeNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsSiteSelected));
                OnPropertyChanged(nameof(IsGateSelected));
                OnPropertyChanged(nameof(IsZoneSelected));
                OnPropertyChanged(nameof(IsLaneSelected));
                OnPropertyChanged(nameof(IsControllerSelected));
                OnPropertyChanged(nameof(IsReaderSelected));
                OnPropertyChanged(nameof(SelectedNodeType));
                UpdateSelectedItemFromNode();
                UpdateDetailInfo();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set { _isEmpty = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterTree();
            }
        }

        // ──────────────────────────────────────────────
        // NEW: Detail panel computed properties
        // ──────────────────────────────────────────────
        public bool HasSelection => SelectedNode != null;
        public bool IsSiteSelected => SelectedNode?.NodeType == "Site";
        public bool IsGateSelected => SelectedNode?.NodeType == "Gate";
        public bool IsZoneSelected => SelectedNode?.NodeType == "Zone";
        public bool IsLaneSelected => SelectedNode?.NodeType == "Lane";
        public bool IsControllerSelected => SelectedNode?.NodeType == "Controller";
        public bool IsReaderSelected => SelectedNode?.NodeType == "Reader";
        public string SelectedNodeType => SelectedNode?.NodeType ?? string.Empty;

        // Gate detail
        private string _detailGateName = string.Empty;
        public string DetailGateName { get => _detailGateName; set { _detailGateName = value; OnPropertyChanged(); } }

        private string _detailGateCode = string.Empty;
        public string DetailGateCode { get => _detailGateCode; set { _detailGateCode = value; OnPropertyChanged(); } }

        private string _detailGateDescription = string.Empty;
        public string DetailGateDescription { get => _detailGateDescription; set { _detailGateDescription = value; OnPropertyChanged(); } }

        private bool _detailGateIsActive;
        public bool DetailGateIsActive { get => _detailGateIsActive; set { _detailGateIsActive = value; OnPropertyChanged(); } }

        private int _detailGateLanes;
        public int DetailGateLanes { get => _detailGateLanes; set { _detailGateLanes = value; OnPropertyChanged(); } }

        private int _detailGateControllers;
        public int DetailGateControllers { get => _detailGateControllers; set { _detailGateControllers = value; OnPropertyChanged(); } }

        // Site detail
        private string _detailSiteName = string.Empty;
        public string DetailSiteName { get => _detailSiteName; set { _detailSiteName = value; OnPropertyChanged(); } }

        private string _detailSiteCode = string.Empty;
        public string DetailSiteCode { get => _detailSiteCode; set { _detailSiteCode = value; OnPropertyChanged(); } }

        private string _detailSiteDescription = string.Empty;
        public string DetailSiteDescription { get => _detailSiteDescription; set { _detailSiteDescription = value; OnPropertyChanged(); } }

        private bool _detailSiteIsActive;
        public bool DetailSiteIsActive { get => _detailSiteIsActive; set { _detailSiteIsActive = value; OnPropertyChanged(); } }

        private int _detailTotalZones;
        public int DetailTotalZones { get => _detailTotalZones; set { _detailTotalZones = value; OnPropertyChanged(); } }

        private int _detailTotalLanes;
        public int DetailTotalLanes { get => _detailTotalLanes; set { _detailTotalLanes = value; OnPropertyChanged(); } }

        private int _detailTotalControllers;
        public int DetailTotalControllers { get => _detailTotalControllers; set { _detailTotalControllers = value; OnPropertyChanged(); } }

        private int _detailActiveControllers;
        public int DetailActiveControllers { get => _detailActiveControllers; set { _detailActiveControllers = value; OnPropertyChanged(); } }

        // Zone detail
        private string _detailZoneName = string.Empty;
        public string DetailZoneName { get => _detailZoneName; set { _detailZoneName = value; OnPropertyChanged(); } }

        private string _detailZoneCode = string.Empty;
        public string DetailZoneCode { get => _detailZoneCode; set { _detailZoneCode = value; OnPropertyChanged(); } }

        private string _detailParentSite = string.Empty;
        public string DetailParentSite { get => _detailParentSite; set { _detailParentSite = value; OnPropertyChanged(); } }

        private int _detailZoneCapacity;
        public int DetailZoneCapacity { get => _detailZoneCapacity; set { _detailZoneCapacity = value; OnPropertyChanged(); } }

        private int _detailZoneLanes;
        public int DetailZoneLanes { get => _detailZoneLanes; set { _detailZoneLanes = value; OnPropertyChanged(); } }

        private int _detailZoneActiveLanes;
        public int DetailZoneActiveLanes { get => _detailZoneActiveLanes; set { _detailZoneActiveLanes = value; OnPropertyChanged(); } }

        private int _detailZoneControllers;
        public int DetailZoneControllers { get => _detailZoneControllers; set { _detailZoneControllers = value; OnPropertyChanged(); } }

        private bool _detailZoneIsActive;
        public bool DetailZoneIsActive { get => _detailZoneIsActive; set { _detailZoneIsActive = value; OnPropertyChanged(); } }

        // Lane detail
        private string _detailLaneName = string.Empty;
        public string DetailLaneName { get => _detailLaneName; set { _detailLaneName = value; OnPropertyChanged(); } }

        private string _detailLaneCode = string.Empty;
        public string DetailLaneCode { get => _detailLaneCode; set { _detailLaneCode = value; OnPropertyChanged(); } }

        private string _detailLaneDirection = string.Empty;
        public string DetailLaneDirection { get => _detailLaneDirection; set { _detailLaneDirection = value; OnPropertyChanged(); } }

        private string _detailLaneZone = string.Empty;
        public string DetailLaneZone { get => _detailLaneZone; set { _detailLaneZone = value; OnPropertyChanged(); } }

        private bool _detailLaneIsActive;
        public bool DetailLaneIsActive { get => _detailLaneIsActive; set { _detailLaneIsActive = value; OnPropertyChanged(); } }

        private string _detailLaneLoaiXeName = string.Empty;
        public string DetailLaneLoaiXeName { get => _detailLaneLoaiXeName; set { _detailLaneLoaiXeName = value; OnPropertyChanged(); } }

        private int? _detailLaneDisplayIndex;
        public int? DetailLaneDisplayIndex { get => _detailLaneDisplayIndex; set { _detailLaneDisplayIndex = value; OnPropertyChanged(); } }

        private ObservableCollection<ReaderLaneMapping> _detailLaneReaders = new();
        public ObservableCollection<ReaderLaneMapping> DetailLaneReaders { get => _detailLaneReaders; set { _detailLaneReaders = value; OnPropertyChanged(); } }

        private ObservableCollection<CameraEntity> _detailLaneCameras = new();
        public ObservableCollection<CameraEntity> DetailLaneCameras { get => _detailLaneCameras; set { _detailLaneCameras = value; OnPropertyChanged(); } }

        // Lane Synchronization badge properties
        private string _laneSyncBadgeText = string.Empty;
        public string LaneSyncBadgeText { get => _laneSyncBadgeText; set { _laneSyncBadgeText = value; OnPropertyChanged(); } }

        private System.Windows.Media.Brush _laneSyncBadgeBrush = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush LaneSyncBadgeBrush { get => _laneSyncBadgeBrush; set { _laneSyncBadgeBrush = value; OnPropertyChanged(); } }

        private System.Windows.Media.Brush _laneSyncBadgeBackground = System.Windows.Media.Brushes.Transparent;
        public System.Windows.Media.Brush LaneSyncBadgeBackground { get => _laneSyncBadgeBackground; set { _laneSyncBadgeBackground = value; OnPropertyChanged(); } }

        private string _laneSyncBadgeTooltip = string.Empty;
        public string LaneSyncBadgeTooltip { get => _laneSyncBadgeTooltip; set { _laneSyncBadgeTooltip = value; OnPropertyChanged(); } }

        private bool _isLaneSyncBadgeVisible;
        public bool IsLaneSyncBadgeVisible { get => _isLaneSyncBadgeVisible; set { _isLaneSyncBadgeVisible = value; OnPropertyChanged(); } }

        // Controller detail
        private string _detailControllerName = string.Empty;
        public string DetailControllerName { get => _detailControllerName; set { _detailControllerName = value; OnPropertyChanged(); } }

        private string _detailControllerIp = string.Empty;
        public string DetailControllerIp { get => _detailControllerIp; set { _detailControllerIp = value; OnPropertyChanged(); } }

        private string _detailControllerServerIp = string.Empty;
        public string DetailControllerServerIp { get => _detailControllerServerIp; set { _detailControllerServerIp = value; OnPropertyChanged(); } }

        private string _detailControllerPcIp = string.Empty;
        public string DetailControllerPcIp { get => _detailControllerPcIp; set { _detailControllerPcIp = value; OnPropertyChanged(); } }

        private string _detailControllerZone = string.Empty;
        public string DetailControllerZone { get => _detailControllerZone; set { _detailControllerZone = value; OnPropertyChanged(); } }

        private bool _detailControllerIsActive;
        public bool DetailControllerIsActive { get => _detailControllerIsActive; set { _detailControllerIsActive = value; OnPropertyChanged(); } }

        // Reader detail
        private int _detailReaderNo;
        public int DetailReaderNo { get => _detailReaderNo; set { _detailReaderNo = value; OnPropertyChanged(); } }

        private string _detailReaderDirection = string.Empty;
        public string DetailReaderDirection { get => _detailReaderDirection; set { _detailReaderDirection = value; OnPropertyChanged(); } }

        private string _detailReaderLane = string.Empty;
        public string DetailReaderLane { get => _detailReaderLane; set { _detailReaderLane = value; OnPropertyChanged(); } }

        private bool _detailReaderEnabled;
        public bool DetailReaderEnabled { get => _detailReaderEnabled; set { _detailReaderEnabled = value; OnPropertyChanged(); } }

        // ── Validation Summary Counts ──────────────────────────────────────────────
        private int _infoCount;
        public int InfoCount
        {
            get => _infoCount;
            set { _infoCount = value; OnPropertyChanged(); }
        }

        private int _warningCount;
        public int WarningCount
        {
            get => _warningCount;
            set { _warningCount = value; OnPropertyChanged(); }
        }

        private int _errorCount;
        public int ErrorCount
        {
            get => _errorCount;
            set { _errorCount = value; OnPropertyChanged(); }
        }

        // ── Validation Grouped Issue Collections ───────────────────────────────────
        public ObservableCollection<ValidationIssue> ErrorIssues { get; } = new ObservableCollection<ValidationIssue>();
        public ObservableCollection<ValidationIssue> WarningIssues { get; } = new ObservableCollection<ValidationIssue>();
        public ObservableCollection<ValidationIssue> InfoIssues { get; } = new ObservableCollection<ValidationIssue>();

        // ── Validation Status ──────────────────────────────────────────────────────
        private bool _isValidationValid;
        public bool IsValidationValid
        {
            get => _isValidationValid;
            set { _isValidationValid = value; OnPropertyChanged(); }
        }

        private bool _isValidationLoading;
        public bool IsValidationLoading
        {
            get => _isValidationLoading;
            set { _isValidationLoading = value; OnPropertyChanged(); }
        }

        private string _validationStatusText = "Chưa kiểm tra";
        public string ValidationStatusText
        {
            get => _validationStatusText;
            set { _validationStatusText = value; OnPropertyChanged(); }
        }

        private bool _hasValidationRun;
        public bool HasValidationRun
        {
            get => _hasValidationRun;
            set { _hasValidationRun = value; OnPropertyChanged(); }
        }

        private TopologyValidationResult _latestValidationResult;
        public ICommand ValidateTopologyCommand { get; }
        public ICommand ShowValidationDetailsCommand { get; }

        // ──────────────────────────────────────────────
        // EXISTING commands (preserved as-is)
        // ──────────────────────────────────────────────
        public ICommand AddSiteCommand { get; }
        public ICommand EditSiteCommand { get; }
        public ICommand DeleteSiteCommand { get; }

        public ICommand AddGateCommand { get; }
        public ICommand EditGateCommand { get; }
        public ICommand DeleteGateCommand { get; }

        public ICommand AddZoneCommand { get; }
        public ICommand EditZoneCommand { get; }
        public ICommand DeleteZoneCommand { get; }

        public ICommand AddLaneCommand { get; }
        public ICommand EditLaneCommand { get; }
        public ICommand DeleteLaneCommand { get; }

        public ICommand AddControllerCommand { get; }
        public ICommand EditControllerCommand { get; }
        public ICommand DeleteControllerCommand { get; }

        // ──────────────────────────────────────────────
        // NEW commands
        // ──────────────────────────────────────────────
        public ICommand RefreshCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand EditSelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }

        // ══════════════════════════════════════════════
        // CONSTRUCTOR
        // ══════════════════════════════════════════════
        public ParkingTopologyViewModel()
        {
            // Existing commands
            AddSiteCommand = new RelayCommand(async _ => await AddSite());
            EditSiteCommand = new RelayCommand(async _ => await EditSite(), _ => SelectedSite != null);
            DeleteSiteCommand = new RelayCommand(async _ => await DeleteSite(), _ => SelectedSite != null);

            AddGateCommand = new RelayCommand(async _ => await AddGate());
            EditGateCommand = new RelayCommand(async _ => await EditGate(), _ => SelectedGate != null);
            DeleteGateCommand = new RelayCommand(async _ => await DeleteGate(), _ => SelectedGate != null);

            AddZoneCommand = new RelayCommand(async _ => await AddZone());
            EditZoneCommand = new RelayCommand(async _ => await EditZone(), _ => SelectedZone != null);
            DeleteZoneCommand = new RelayCommand(async _ => await DeleteZone(), _ => SelectedZone != null);

            AddLaneCommand = new RelayCommand(async _ => await AddLane());
            EditLaneCommand = new RelayCommand(async _ => await EditLane(), _ => SelectedLane != null);
            DeleteLaneCommand = new RelayCommand(async _ => await DeleteLane(), _ => SelectedLane != null);

            AddControllerCommand = new RelayCommand(async _ => await AddController());
            EditControllerCommand = new RelayCommand(async _ => await EditController(), _ => SelectedController != null);
            DeleteControllerCommand = new RelayCommand(async _ => await DeleteController(), _ => SelectedController != null);

            // New commands
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
            ExpandAllCommand = new RelayCommand(_ => SetAllExpanded(true));
            CollapseAllCommand = new RelayCommand(_ => SetAllExpanded(false));
            EditSelectedCommand = new RelayCommand(async _ => await EditSelected(), _ => SelectedNode != null && SelectedNode.NodeType != "Reader");
            DeleteSelectedCommand = new RelayCommand(async _ => await DeleteSelected(), _ => SelectedNode != null && SelectedNode.NodeType != "Reader");
            ValidateTopologyCommand = new RelayCommand(_ => ExecuteValidation());
            ShowValidationDetailsCommand = new RelayCommand(_ => ExecuteShowValidationDetails());

            _ = LoadDataAsync();
        }

        // ──────────────────────────────────────────────
        // DATA LOADING
        // ──────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                var gates = await ParkingTopologyService.Instance.GetGatesAsync();
                var zones = await ParkingTopologyService.Instance.GetZonesAsync();
                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Sites.Clear();
                    foreach (var s in sites) Sites.Add(s);

                    Gates.Clear();
                    foreach (var g in gates) Gates.Add(g);

                    Zones.Clear();
                    foreach (var z in zones) Zones.Add(z);

                    Lanes.Clear();
                    foreach (var l in lanes) Lanes.Add(l);

                    Controllers.Clear();
                    foreach (var c in controllers) Controllers.Add(c);

                    BuildTreeNodes();
                    IsEmpty = TreeNodes.Count == 0;
                    IsLoading = false;
                });
                ExecuteValidation();
            }
            catch (Exception ex)
            {
                IsLoading = false;
                MessageBox.Show("Lỗi tải dữ liệu: " + ex.Message);
            }
        }

        // ──────────────────────────────────────────────
        // TREE BUILDING
        // ──────────────────────────────────────────────
        private void BuildTreeNodes()
        {
            TreeNodes.Clear();
            var readerMappings = ReaderLaneMappingService.Instance.GetAll();

            foreach (var site in Sites)
            {
                var siteGates = Gates.Where(g => g.SiteId == site.Id).ToList();
                var siteZones = Zones.Where(z => z.SiteId == site.Id).ToList();

                var siteNode = new TopologyTreeNode
                {
                    Name = site.SiteName,
                    Icon = "🏢",
                    NodeType = "Site",
                    DataItem = site,
                    IsActive = site.IsActive,
                    IsExpanded = true,
                    Badge = site.SiteCode,
                    Subtitle = $"{siteGates.Count} cổng • {siteZones.Count} phân khu"
                };

                // Group 1: PHYSICAL GATES
                var gatesGroupNode = new TopologyTreeNode
                {
                    Name = "Cổng kiểm soát vật lý",
                    Icon = "🚪",
                    NodeType = "GateGroup",
                    IsActive = true,
                    IsExpanded = true,
                    Subtitle = $"{siteGates.Count} cổng kiểm soát"
                };

                foreach (var gate in siteGates)
                {
                    var gateLanes = Lanes.Where(l => l.GateId == gate.Id).ToList();
                    var gateControllers = Controllers.Where(c => c.GateId == gate.Id).ToList();

                    var gateNode = new TopologyTreeNode
                    {
                        Name = gate.GateName,
                        Icon = "🚪",
                        NodeType = "Gate",
                        DataItem = gate,
                        IsActive = gate.IsActive,
                        IsExpanded = true,
                        Badge = gate.GateCode,
                        Subtitle = $"{gateLanes.Count} làn • {gateControllers.Count} controller"
                    };

                    foreach (var lane in gateLanes)
                    {
                        var laneReaders = readerMappings.Where(r => r.LaneId == lane.Id).ToList();
                        string targetZone = !string.IsNullOrEmpty(lane.ZoneName) ? $" • Phân khu: {lane.ZoneName}" : "";

                        var laneNode = new TopologyTreeNode
                        {
                            Name = lane.LaneName,
                            Icon = lane.Direction == "IN" ? "🚗" : "🚙",
                            NodeType = "Lane",
                            DataItem = lane,
                            IsActive = lane.IsActive,
                            Badge = lane.Direction,
                            Subtitle = $"{lane.LaneCode} • {laneReaders.Count} reader{targetZone}"
                        };

                        foreach (var reader in laneReaders)
                        {
                            var readerNode = new TopologyTreeNode
                            {
                                Name = $"Reader #{reader.ReaderNo}",
                                Icon = "📡",
                                NodeType = "Reader",
                                DataItem = reader,
                                IsActive = reader.IsEnabled,
                                Badge = reader.Direction
                            };
                            laneNode.Children.Add(readerNode);
                        }

                        gateNode.Children.Add(laneNode);
                    }

                    foreach (var controller in gateControllers)
                    {
                        var controllerNode = new TopologyTreeNode
                        {
                            Name = controller.ControllerName,
                            Icon = "🧠",
                            NodeType = "Controller",
                            DataItem = controller,
                            IsActive = controller.IsActive,
                            Badge = controller.IpAddress
                        };
                        gateNode.Children.Add(controllerNode);
                    }

                    gatesGroupNode.Children.Add(gateNode);
                }

                if (gatesGroupNode.Children.Any())
                {
                    siteNode.Children.Add(gatesGroupNode);
                }

                // Group 2: LOGICAL ZONES
                var zonesGroupNode = new TopologyTreeNode
                {
                    Name = "Phân khu đỗ xe (Logic)",
                    Icon = "🅿️",
                    NodeType = "ZoneGroup",
                    IsActive = true,
                    IsExpanded = true,
                    Subtitle = $"{siteZones.Count} phân khu sức chứa"
                };

                foreach (var zone in siteZones)
                {
                    var zoneLanes = Lanes.Where(l => l.ZoneId == zone.Id).ToList();

                    var zoneNode = new TopologyTreeNode
                    {
                        Name = zone.ZoneName,
                        Icon = "📍",
                        NodeType = "Zone",
                        DataItem = zone,
                        IsActive = zone.IsActive,
                        IsExpanded = false,
                        Badge = zone.ZoneCode,
                        Subtitle = $"{zoneLanes.Count} làn xe • Sức chứa: {zone.MaxCapacity}"
                    };

                    zonesGroupNode.Children.Add(zoneNode);
                }

                if (zonesGroupNode.Children.Any())
                {
                    siteNode.Children.Add(zonesGroupNode);
                }

                TreeNodes.Add(siteNode);
            }

            // Orphan Lanes (unassigned to any Gate)
            var orphanLanes = Lanes.Where(l => !l.GateId.HasValue).ToList();
            if (orphanLanes.Any())
            {
                var orphanNode = new TopologyTreeNode
                {
                    Name = "Làn xe chưa gán Cổng",
                    Icon = "⚠",
                    NodeType = "OrphanGroup",
                    IsActive = true,
                    IsExpanded = true,
                    Subtitle = $"{orphanLanes.Count} làn chưa gán cổng vật lý"
                };

                foreach (var lane in orphanLanes)
                {
                    var laneReaders = readerMappings.Where(r => r.LaneId == lane.Id).ToList();
                    var laneNode = new TopologyTreeNode
                    {
                        Name = lane.LaneName,
                        Icon = lane.Direction == "IN" ? "🚗" : "🚙",
                        NodeType = "Lane",
                        DataItem = lane,
                        IsActive = lane.IsActive,
                        Badge = lane.Direction,
                        Subtitle = $"{lane.LaneCode} • {laneReaders.Count} reader"
                    };

                    foreach (var reader in laneReaders)
                    {
                        var readerNode = new TopologyTreeNode
                        {
                            Name = $"Reader #{reader.ReaderNo}",
                            Icon = "📡",
                            NodeType = "Reader",
                            DataItem = reader,
                            IsActive = reader.IsEnabled,
                            Badge = reader.Direction
                        };
                        laneNode.Children.Add(readerNode);
                    }

                    orphanNode.Children.Add(laneNode);
                }

                TreeNodes.Add(orphanNode);
            }

            // Orphan Controllers (unassigned to any Gate)
            var orphanControllers = Controllers.Where(c => !c.GateId.HasValue).ToList();
            if (orphanControllers.Any())
            {
                var orphanCtrlNode = new TopologyTreeNode
                {
                    Name = "Controller chưa gán Cổng",
                    Icon = "⚠",
                    NodeType = "OrphanControllerGroup",
                    IsActive = true,
                    IsExpanded = true,
                    Subtitle = $"{orphanControllers.Count} controller chưa gán cổng vật lý"
                };

                foreach (var ctrl in orphanControllers)
                {
                    var ctrlNode = new TopologyTreeNode
                    {
                        Name = ctrl.ControllerName,
                        Icon = "🧠",
                        NodeType = "Controller",
                        DataItem = ctrl,
                        IsActive = ctrl.IsActive,
                        Badge = ctrl.IpAddress
                    };
                    orphanCtrlNode.Children.Add(ctrlNode);
                }

                TreeNodes.Add(orphanCtrlNode);
            }
        }

        // ──────────────────────────────────────────────
        // TREE SEARCH / FILTER
        // ──────────────────────────────────────────────
        private void FilterTree()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SetAllVisible(TreeNodes, true);
                return;
            }

            var searchLower = SearchText.ToLowerInvariant();
            foreach (var node in TreeNodes)
            {
                FilterNode(node, searchLower);
            }
        }

        private bool FilterNode(TopologyTreeNode node, string searchLower)
        {
            bool selfMatch = node.Name.ToLowerInvariant().Contains(searchLower) ||
                             (node.Badge?.ToLowerInvariant().Contains(searchLower) ?? false);

            bool childMatch = false;
            foreach (var child in node.Children)
            {
                if (FilterNode(child, searchLower))
                    childMatch = true;
            }

            node.IsVisible = selfMatch || childMatch;
            if (childMatch) node.IsExpanded = true;
            return node.IsVisible;
        }

        private void SetAllVisible(ObservableCollection<TopologyTreeNode> nodes, bool visible)
        {
            foreach (var node in nodes)
            {
                node.IsVisible = visible;
                SetAllVisible(node.Children, visible);
            }
        }

        // ──────────────────────────────────────────────
        // TREE EXPAND/COLLAPSE
        // ──────────────────────────────────────────────
        private void SetAllExpanded(bool expanded)
        {
            foreach (var node in TreeNodes)
                SetNodeExpanded(node, expanded);
        }

        private void SetNodeExpanded(TopologyTreeNode node, bool expanded)
        {
            node.IsExpanded = expanded;
            foreach (var child in node.Children)
                SetNodeExpanded(child, expanded);
        }

        // ──────────────────────────────────────────────
        // NODE SELECTION → Selected Item sync
        // ──────────────────────────────────────────────
        private void UpdateSelectedItemFromNode()
        {
            // Sync the tree selection with existing SelectedXxx properties
            // so that existing Edit/Delete commands still work
            SelectedSite = null;
            SelectedGate = null;
            SelectedZone = null;
            SelectedLane = null;
            SelectedController = null;
            DetailLaneCameras.Clear();

            if (SelectedNode == null) return;

            switch (SelectedNode.NodeType)
            {
                case "Site":
                    SelectedSite = SelectedNode.DataItem as ParkingSite;
                    break;
                case "Gate":
                    SelectedGate = SelectedNode.DataItem as ParkingGate;
                    break;
                case "Zone":
                    SelectedZone = SelectedNode.DataItem as ParkingZone;
                    break;
                case "Lane":
                    SelectedLane = SelectedNode.DataItem as LaneConfig;
                    break;
                case "Controller":
                    SelectedController = SelectedNode.DataItem as C3ControllerConfig;
                    break;
            }
        }

        // ──────────────────────────────────────────────
        // DETAIL INFO UPDATE
        // ──────────────────────────────────────────────
        private void UpdateDetailInfo()
        {
            if (SelectedNode == null) return;

            switch (SelectedNode.NodeType)
            {
                case "Site":
                    UpdateSiteDetail(SelectedNode.DataItem as ParkingSite);
                    break;
                case "Gate":
                    UpdateGateDetail(SelectedNode.DataItem as ParkingGate);
                    break;
                case "Zone":
                    UpdateZoneDetail(SelectedNode.DataItem as ParkingZone);
                    break;
                case "Lane":
                    UpdateLaneDetail(SelectedNode.DataItem as LaneConfig);
                    break;
                case "Controller":
                    UpdateControllerDetail(SelectedNode.DataItem as C3ControllerConfig);
                    break;
                case "Reader":
                    UpdateReaderDetail(SelectedNode.DataItem as ReaderLaneMapping);
                    break;
            }
        }

        private void UpdateSiteDetail(ParkingSite site)
        {
            if (site == null) return;
            DetailSiteName = site.SiteName;
            DetailSiteCode = site.SiteCode;
            DetailSiteDescription = site.Description;
            DetailSiteIsActive = site.IsActive;

            var siteGates = Gates.Where(g => g.SiteId == site.Id).ToList();
            DetailTotalZones = Zones.Count(z => z.SiteId == site.Id);
            DetailTotalLanes = Lanes.Count(l => siteGates.Any(g => g.Id == l.GateId));
            var siteControllers = Controllers.Where(c => siteGates.Any(g => g.Id == c.GateId)).ToList();
            DetailTotalControllers = siteControllers.Count;
            DetailActiveControllers = siteControllers.Count(c => c.IsActive);
        }

        private void UpdateGateDetail(ParkingGate gate)
        {
            if (gate == null) return;
            DetailGateName = gate.GateName;
            DetailGateCode = gate.GateCode;
            DetailGateDescription = gate.Description;
            DetailGateIsActive = gate.IsActive;

            DetailGateLanes = Lanes.Count(l => l.GateId == gate.Id);
            DetailGateControllers = Controllers.Count(c => c.GateId == gate.Id);
        }

        private void UpdateZoneDetail(ParkingZone zone)
        {
            if (zone == null) return;
            DetailZoneName = zone.ZoneName;
            DetailZoneCode = zone.ZoneCode;
            DetailParentSite = zone.SiteName;
            DetailZoneCapacity = zone.MaxCapacity;
            DetailZoneIsActive = zone.IsActive;

            var zoneLanes = Lanes.Where(l => l.ZoneId == zone.Id).ToList();
            DetailZoneLanes = zoneLanes.Count;
            DetailZoneActiveLanes = zoneLanes.Count(l => l.IsActive);
            DetailZoneControllers = Controllers.Count(c => c.ZoneId == zone.Id);
        }

        private async void UpdateLaneDetail(LaneConfig lane)
        {
            if (lane == null) return;
            DetailLaneName = lane.LaneName;
            DetailLaneCode = lane.LaneCode;
            DetailLaneDirection = lane.Direction;
            DetailLaneZone = lane.ZoneName;
            DetailLaneIsActive = lane.IsActive;
            DetailLaneLoaiXeName = lane.LoaiXeName;
            DetailLaneDisplayIndex = lane.DisplayIndex;

            var readers = ReaderLaneMappingService.Instance.GetMappingsByLane(lane.Id);
            DetailLaneReaders = new ObservableCollection<ReaderLaneMapping>(readers);

            try
            {
                var allCams = await CameraRepository.Instance.GetAllAsync();
                var laneCams = allCams.Where(c => c.LaneId == lane.Id).ToList();
                DetailLaneCameras = new ObservableCollection<CameraEntity>(laneCams);
            }
            catch
            {
                DetailLaneCameras = new ObservableCollection<CameraEntity>();
            }

            // Compute analysis for dynamic badge properties
            var analysis = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(lane.Id);
            LaneSyncBadgeText = analysis.BadgeText;
            LaneSyncBadgeTooltip = analysis.BadgeTooltip;
            IsLaneSyncBadgeVisible = analysis.Badge != LaneDirectionBadge.None;

            var converter = new System.Windows.Media.BrushConverter();
            System.Windows.Media.Brush GetBrush(string hex) => (System.Windows.Media.Brush)converter.ConvertFromString(hex);

            switch (analysis.Badge)
            {
                case LaneDirectionBadge.AutoIn:
                    LaneSyncBadgeBrush = GetBrush("#2E7D32");
                    LaneSyncBadgeBackground = GetBrush("#E8F5E9");
                    break;
                case LaneDirectionBadge.AutoOut:
                    LaneSyncBadgeBrush = GetBrush("#D84315");
                    LaneSyncBadgeBackground = GetBrush("#FFF3E0");
                    break;
                case LaneDirectionBadge.Mixed:
                    LaneSyncBadgeBrush = GetBrush("#C62828");
                    LaneSyncBadgeBackground = GetBrush("#FFEBEE");
                    break;
                default:
                    LaneSyncBadgeBrush = System.Windows.Media.Brushes.Transparent;
                    LaneSyncBadgeBackground = System.Windows.Media.Brushes.Transparent;
                    break;
            }
        }

        private void UpdateControllerDetail(C3ControllerConfig controller)
        {
            if (controller == null) return;
            DetailControllerName = controller.ControllerName;
            DetailControllerIp = controller.IpAddress;
            DetailControllerServerIp = controller.ServerIp;
            DetailControllerPcIp = controller.PcIp;
            DetailControllerZone = controller.GateName; // repurposed Zone to GateName display in UI details panel
            DetailControllerIsActive = controller.IsActive;
        }

        private void UpdateReaderDetail(ReaderLaneMapping reader)
        {
            if (reader == null) return;
            DetailReaderNo = reader.ReaderNo;
            DetailReaderDirection = reader.Direction;
            DetailReaderEnabled = reader.IsEnabled;

            var lane = Lanes.FirstOrDefault(l => l.Id == reader.LaneId);
            DetailReaderLane = lane?.LaneName ?? $"Lane #{reader.LaneId}";
        }

        // ──────────────────────────────────────────────
        // CONTEXT ACTIONS (Edit/Delete based on selection)
        // ──────────────────────────────────────────────
        private async Task EditSelected()
        {
            if (SelectedNode == null) return;
            switch (SelectedNode.NodeType)
            {
                case "Site": await EditSite(); break;
                case "Gate": await EditGate(); break;
                case "Zone": await EditZone(); break;
                case "Lane": await EditLane(); break;
                case "Controller": await EditController(); break;
            }
        }

        private async Task DeleteSelected()
        {
            if (SelectedNode == null) return;
            switch (SelectedNode.NodeType)
            {
                case "Site": await DeleteSite(); break;
                case "Gate": await DeleteGate(); break;
                case "Zone": await DeleteZone(); break;
                case "Lane": await DeleteLane(); break;
                case "Controller": await DeleteController(); break;
            }
        }

        // ══════════════════════════════════════════════
        // EXISTING CRUD METHODS (preserved exactly as-is)
        // ══════════════════════════════════════════════

        // --- SITE ---
        private async Task AddSite()
        {
            var newItem = new ParkingSite();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Site" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveSiteAsync(newItem);
                    if (success)
                        MessageBox.Show("Thêm Site thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu Site vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi thêm Site", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task EditSite()
        {
            var dialog = new GenericAddEditWindow(SelectedSite) { Title = "Sửa Site" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveSiteAsync(SelectedSite);
                    if (success)
                        MessageBox.Show("Sửa Site thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu thay đổi vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi sửa Site", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task DeleteSite()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa site này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.DeleteSiteAsync(SelectedSite.Id);
                    if (success)
                    {
                        MessageBox.Show("Xóa Site thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Đã xóa Site trong bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // --- ZONE ---
        private async Task AddZone()
        {
            var newItem = new ParkingZone();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Zone" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveZoneAsync(newItem);
                    if (success)
                        MessageBox.Show("Thêm Zone thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu Zone vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi thêm Zone", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task EditZone()
        {
            var dialog = new GenericAddEditWindow(SelectedZone) { Title = "Sửa Zone" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveZoneAsync(SelectedZone);
                    if (success)
                        MessageBox.Show("Sửa Zone thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu thay đổi vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi sửa Zone", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task DeleteZone()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa zone này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.DeleteZoneAsync(SelectedZone.Id);
                    if (success)
                    {
                        MessageBox.Show("Xóa Zone thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Đã xóa Zone trong bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // --- GATE ---
        private async Task AddGate()
        {
            var newItem = new ParkingGate();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Cổng" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveGateAsync(newItem);
                    if (success)
                        MessageBox.Show("Thêm Cổng kiểm soát thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu Cổng vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi thêm Cổng", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task EditGate()
        {
            var dialog = new GenericAddEditWindow(SelectedGate) { Title = "Sửa Cổng" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveGateAsync(SelectedGate);
                    if (success)
                        MessageBox.Show("Sửa Cổng kiểm soát thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu thay đổi vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi sửa Cổng", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task DeleteGate()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa cổng này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.DeleteGateAsync(SelectedGate.Id);
                    if (success)
                    {
                        MessageBox.Show("Xóa Cổng kiểm soát thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Đã xóa Cổng trong bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // --- LANE ---
        private async Task AddLane()
        {
            var newItem = new LaneConfig();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Làn" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveLaneAsync(newItem);
                    if (success)
                        MessageBox.Show("Thêm Làn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu Làn vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi thêm Làn", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task EditLane()
        {
            var dialog = new GenericAddEditWindow(SelectedLane) { Title = "Sửa Làn" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveLaneAsync(SelectedLane);
                    if (success)
                        MessageBox.Show("Sửa Làn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu thay đổi vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi sửa Làn", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task DeleteLane()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa làn này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.DeleteLaneAsync(SelectedLane.Id);
                    if (success)
                    {
                        MessageBox.Show("Xóa Làn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Đã xóa Làn trong bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // --- CONTROLLER ---
        private async Task AddController()
        {
            var newItem = new C3ControllerConfig();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Controller" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveControllerAsync(newItem);
                    if (success)
                        MessageBox.Show("Thêm Controller thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu Controller vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi thêm Controller", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task EditController()
        {
            var dialog = new GenericAddEditWindow(SelectedController) { Title = "Sửa Controller" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.SaveControllerAsync(SelectedController);
                    if (success)
                        MessageBox.Show("Sửa Controller thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Đã lưu thay đổi vào bộ nhớ tạm (Offline). Dữ liệu sẽ được đồng bộ lên Server sau.", "Thông báo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await LoadDataAsync();
                    RunPostSaveValidation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi sửa Controller", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task DeleteController()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa controller này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await ParkingTopologyService.Instance.DeleteControllerAsync(SelectedController.Id);
                    if (success)
                    {
                        MessageBox.Show("Xóa Controller thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RunPostSaveValidation()
        {
            try
            {
                var validationResult = TopologyValidationService.Instance.ValidateTopology();
                if (validationResult.Errors.Count > 0)
                {
                    var dialog = new TopologyValidationDialog(validationResult);
                    dialog.Owner = Application.Current?.MainWindow;
                    dialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("POST_SAVE_VALIDATE", "UI", $"Validation error: {ex.Message}");
            }
        }

        private async void ExecuteValidation()
        {
            if (IsValidationLoading) return;

            IsValidationLoading = true;
            ValidationStatusText = "Đang kiểm tra...";

            try
            {
                var result = await Task.Run(() =>
                    TopologyValidationService.Instance.ValidateTopology());

                _latestValidationResult = result;

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // Clear previous results
                    ErrorIssues.Clear();
                    WarningIssues.Clear();
                    InfoIssues.Clear();

                    // Populate grouped collections
                    foreach (var issue in result.Errors)
                        ErrorIssues.Add(issue);

                    foreach (var issue in result.Warnings)
                        WarningIssues.Add(issue);

                    foreach (var issue in result.Infos)
                        InfoIssues.Add(issue);

                    // Update counts
                    ErrorCount = result.Errors.Count;
                    WarningCount = result.Warnings.Count;
                    InfoCount = result.Infos.Count;

                    // Update status
                    IsValidationValid = result.IsValid;
                    HasValidationRun = true;

                    if (result.IsValid)
                    {
                        ValidationStatusText = "✓ Cấu hình hợp lệ";
                    }
                    else
                    {
                        ValidationStatusText = "⚠ Cần xử lý trước khi vận hành";
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    ValidationStatusText = $"Kiểm tra thất bại: {ex.Message}";
                    HasValidationRun = true;
                });
            }
            finally
            {
                IsValidationLoading = false;
            }
        }

        private void ExecuteShowValidationDetails()
        {
            if (_latestValidationResult == null)
            {
                _latestValidationResult = TopologyValidationService.Instance.ValidateTopology();
            }

            var dialog = new TopologyValidationDialog(_latestValidationResult);
            dialog.Owner = Application.Current?.MainWindow;
            dialog.ShowDialog();
        }
    }
}
