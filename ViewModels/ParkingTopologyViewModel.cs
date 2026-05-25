using System;
using System.Collections.ObjectModel;
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
        public ObservableCollection<ParkingSite> Sites { get; set; } = new ObservableCollection<ParkingSite>();
        public ObservableCollection<ParkingZone> Zones { get; set; } = new ObservableCollection<ParkingZone>();
        public ObservableCollection<LaneConfig> Lanes { get; set; } = new ObservableCollection<LaneConfig>();
        public ObservableCollection<C3ControllerConfig> Controllers { get; set; } = new ObservableCollection<C3ControllerConfig>();

        private ParkingSite _selectedSite;
        public ParkingSite SelectedSite
        {
            get => _selectedSite;
            set { _selectedSite = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
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

        public ICommand AddSiteCommand { get; }
        public ICommand EditSiteCommand { get; }
        public ICommand DeleteSiteCommand { get; }

        public ICommand AddZoneCommand { get; }
        public ICommand EditZoneCommand { get; }
        public ICommand DeleteZoneCommand { get; }

        public ICommand AddLaneCommand { get; }
        public ICommand EditLaneCommand { get; }
        public ICommand DeleteLaneCommand { get; }

        public ICommand AddControllerCommand { get; }
        public ICommand EditControllerCommand { get; }
        public ICommand DeleteControllerCommand { get; }

        public ParkingTopologyViewModel()
        {
            AddSiteCommand = new RelayCommand(async _ => await AddSite());
            EditSiteCommand = new RelayCommand(async _ => await EditSite(), _ => SelectedSite != null);
            DeleteSiteCommand = new RelayCommand(async _ => await DeleteSite(), _ => SelectedSite != null);

            AddZoneCommand = new RelayCommand(async _ => await AddZone());
            EditZoneCommand = new RelayCommand(async _ => await EditZone(), _ => SelectedZone != null);
            DeleteZoneCommand = new RelayCommand(async _ => await DeleteZone(), _ => SelectedZone != null);

            AddLaneCommand = new RelayCommand(async _ => await AddLane());
            EditLaneCommand = new RelayCommand(async _ => await EditLane(), _ => SelectedLane != null);
            DeleteLaneCommand = new RelayCommand(async _ => await DeleteLane(), _ => SelectedLane != null);

            AddControllerCommand = new RelayCommand(async _ => await AddController());
            EditControllerCommand = new RelayCommand(async _ => await EditController(), _ => SelectedController != null);
            DeleteControllerCommand = new RelayCommand(async _ => await DeleteController(), _ => SelectedController != null);

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                var zones = await ParkingTopologyService.Instance.GetZonesAsync();
                var lanes = await ParkingTopologyService.Instance.GetLanesAsync();
                var controllers = await ParkingTopologyService.Instance.GetControllersAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Sites.Clear();
                    foreach (var s in sites) Sites.Add(s);

                    Zones.Clear();
                    foreach (var z in zones) Zones.Add(z);

                    Lanes.Clear();
                    foreach (var l in lanes) Lanes.Add(l);

                    Controllers.Clear();
                    foreach (var c in controllers) Controllers.Add(c);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải dữ liệu: " + ex.Message);
            }
        }

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
    }
}
