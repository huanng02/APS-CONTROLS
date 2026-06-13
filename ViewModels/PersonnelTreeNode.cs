using System.Collections.ObjectModel;

namespace QuanLyGiuXe.ViewModels
{
    /// <summary>
    /// UI wrapper node for the personnel tree hierarchy.
    /// Wraps Companies, Departments, Positions, Employees, and RFIDCards
    /// for hierarchical explorer presentation.
    /// </summary>
    public class PersonnelTreeNode : BaseViewModel
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _icon = string.Empty;
        /// <summary>Unicode icon character for the node (🏢📁🏷️👤💳)</summary>
        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        private string _nodeType = string.Empty;
        /// <summary>Node type: "Company", "Department", "Position", "Employee", "Card", "OrphanGroup"</summary>
        public string NodeType
        {
            get => _nodeType;
            set { _nodeType = value; OnPropertyChanged(); }
        }

        private object? _dataItem;
        /// <summary>The actual data model object (Company, Department, Position, Employee, RFIDCards)</summary>
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
        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; OnPropertyChanged(); }
        }

        private string _badge = string.Empty;
        public string Badge
        {
            get => _badge;
            set { _badge = value; OnPropertyChanged(); }
        }

        public ObservableCollection<PersonnelTreeNode> Children { get; set; } = new ObservableCollection<PersonnelTreeNode>();

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }
    }
}
