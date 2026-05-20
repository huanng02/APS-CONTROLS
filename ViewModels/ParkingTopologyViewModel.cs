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

        public ParkingSite SelectedSite { get; set; }
        public ParkingZone SelectedZone { get; set; }
        public LaneConfig SelectedLane { get; set; }
        public C3ControllerConfig SelectedController { get; set; }

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
                await ParkingTopologyService.Instance.SaveSiteAsync(newItem);
                await LoadDataAsync();
            }
        }

        private async Task EditSite()
        {
            var dialog = new GenericAddEditWindow(SelectedSite) { Title = "Sửa Site" };
            if (dialog.ShowDialog() == true)
            {
                await ParkingTopologyService.Instance.SaveSiteAsync(SelectedSite);
                await LoadDataAsync();
            }
        }

        private async Task DeleteSite()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa site này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await ParkingTopologyService.Instance.DeleteSiteAsync(SelectedSite.Id);
                await LoadDataAsync();
            }
        }

        // --- ZONE ---
        private async Task AddZone()
        {
            var newItem = new ParkingZone();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Zone" };
            if (dialog.ShowDialog() == true)
            {
                await ParkingTopologyService.Instance.SaveZoneAsync(newItem);
                await LoadDataAsync();
            }
        }

        private async Task EditZone()
        {
            var dialog = new GenericAddEditWindow(SelectedZone) { Title = "Sửa Zone" };
            if (dialog.ShowDialog() == true)
            {
                await ParkingTopologyService.Instance.SaveZoneAsync(SelectedZone);
                await LoadDataAsync();
            }
        }

        private async Task DeleteZone()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa zone này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await ParkingTopologyService.Instance.DeleteZoneAsync(SelectedZone.Id);
                await LoadDataAsync();
            }
        }

        // --- LANE ---
        private async Task AddLane()
        {
            var newItem = new LaneConfig();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Làn" };
            if (dialog.ShowDialog() == true)
            {
                await ParkingTopologyService.Instance.SaveLaneAsync(newItem);
                await LoadDataAsync();
            }
        }

        private async Task EditLane()
        {
            var dialog = new GenericAddEditWindow(SelectedLane) { Title = "Sửa Làn" };
            if (dialog.ShowDialog() == true)
            {
                await ParkingTopologyService.Instance.SaveLaneAsync(SelectedLane);
                await LoadDataAsync();
            }
        }

        private async Task DeleteLane()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa làn này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await ParkingTopologyService.Instance.DeleteLaneAsync(SelectedLane.Id);
                await LoadDataAsync();
            }
        }

        // --- CONTROLLER ---
        private async Task AddController()
        {
            var newItem = new C3ControllerConfig();
            var dialog = new GenericAddEditWindow(newItem) { Title = "Thêm Controller" };
            if (dialog.ShowDialog() == true)
            {
                await ParkingTopologyService.Instance.SaveControllerAsync(newItem);
                await LoadDataAsync();
            }
        }

        private async Task EditController()
        {
            var dialog = new GenericAddEditWindow(SelectedController) { Title = "Sửa Controller" };
            if (dialog.ShowDialog() == true)
            {
                await ParkingTopologyService.Instance.SaveControllerAsync(SelectedController);
                await LoadDataAsync();
            }
        }

        private async Task DeleteController()
        {
            if (MessageBox.Show("Bạn có chắc muốn xóa controller này?", "Xác nhận", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await ParkingTopologyService.Instance.DeleteControllerAsync(SelectedController.Id);
                await LoadDataAsync();
            }
        }
    }
}
