using System.Collections.ObjectModel;

namespace QuanLyGiuXe.ViewModels
{
    /// <summary>
    /// UI wrapper node for the topology tree hierarchy.
    /// Wraps existing models (ParkingSite, ParkingZone, LaneConfig, C3ControllerConfig, ReaderLaneMapping)
    /// without modifying them. This is purely a presentation-layer construct.
    /// </summary>
    public class TopologyTreeNode : BaseViewModel
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _icon = string.Empty;
        /// <summary>Unicode icon character for the node (🏢📍🚗📡🧠)</summary>
        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        private string _nodeType = string.Empty;
        /// <summary>Node type: "Site", "Zone", "Lane", "Controller", "Reader"</summary>
        public string NodeType
        {
            get => _nodeType;
            set { _nodeType = value; OnPropertyChanged(); }
        }

        private object? _dataItem;
        /// <summary>The actual data model object (ParkingSite, ParkingZone, LaneConfig, etc.)</summary>
        public object? DataItem
        {
            get => _dataItem;
            set { _dataItem = value; OnPropertyChanged(); }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private string _subtitle = string.Empty;
        /// <summary>Secondary info displayed below the name (e.g., "3 Zones • 8 Lanes")</summary>
        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; OnPropertyChanged(); }
        }

        private string _badge = string.Empty;
        /// <summary>Small badge text (e.g., "IN", "OUT", IP address)</summary>
        public string Badge
        {
            get => _badge;
            set { _badge = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TopologyTreeNode> Children { get; set; } = new ObservableCollection<TopologyTreeNode>();

        /// <summary>
        /// Whether this node is visible (used for search filtering).
        /// </summary>
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }
    }
}
