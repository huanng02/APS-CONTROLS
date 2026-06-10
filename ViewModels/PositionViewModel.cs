using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe.ViewModels
{
    public class PositionViewModel : BaseViewModel
    {
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public ObservableCollection<Position> Items { get; } = new ObservableCollection<Position>();

        private Position? _selectedItem;
        public Position? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public PositionViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(param => Edit(param));
            DeleteCommand = new RelayCommand(param => Delete(param));

            Load();
        }

        public void Load()
        {
            try
            {
                var list = _service.GetPositions();
                Items.Clear();
                foreach (var item in list)
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách chức vụ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Add()
        {
            var dialog = new PositionDialog(new Position())
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                Load();
            }
        }

        private void Edit(object? param)
        {
            var pos = param as Position ?? SelectedItem;
            if (pos == null)
            {
                MessageBox.Show("Vui lòng chọn chức vụ cần chỉnh sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var clone = new Position
            {
                Id = pos.Id,
                PositionName = pos.PositionName,
                Status = pos.Status,
                IsDeleted = pos.IsDeleted
            };

            var dialog = new PositionDialog(clone)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                Load();
            }
        }

        private void Delete(object? param)
        {
            var pos = param as Position ?? SelectedItem;
            if (pos == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa chức vụ '{pos.PositionName}'?", 
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                if (_service.DeletePosition(pos.Id, out string error))
                {
                    MessageBox.Show("Xóa chức vụ thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Load();
                }
                else
                {
                    MessageBox.Show(error, "Lỗi xóa dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
