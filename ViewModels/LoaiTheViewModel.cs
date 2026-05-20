using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LoaiTheViewModel : INotifyPropertyChanged
    {
        private readonly LoaiTheService service = new LoaiTheService();

        public ObservableCollection<LoaiThe> Items { get; set; } = new ObservableCollection<LoaiThe>();

        private LoaiThe _selectedItem;
        public LoaiThe SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));

                if (_selectedItem != null)
                {
                    TenLoaiThe = _selectedItem.TenLoaiThe;
                    GiaTien = _selectedItem.GiaTien;
                    TrangThai = _selectedItem.TrangThai;
                }
            }
        }

        private string _tenLoaiThe;
        public string TenLoaiThe
        {
            get => _tenLoaiThe;
            set { _tenLoaiThe = value; OnPropertyChanged(nameof(TenLoaiThe)); }
        }

        private decimal _giaTien;
        public decimal GiaTien
        {
            get => _giaTien;
            set { _giaTien = value; OnPropertyChanged(nameof(GiaTien)); }
        }

        private string _trangThai;
        public string TrangThai
        {
            get => _trangThai;
            set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); }
        }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }

        public LoaiTheViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(_ => Update());
            DeleteCommand = new RelayCommand(_ => Delete());
            ClearCommand = new RelayCommand(_ => Clear());

            Load();
        }

        private void Load()
        {
            Items.Clear();
            var list = service.GetAll();
            foreach (var item in list)
                Items.Add(item);
        }

        private void Add()
        {
            service.Add(TenLoaiThe, GiaTien, TrangThai);
            Load();
            Clear();
        }

        private void Update()
        {
            if (SelectedItem == null) return;

            service.Update(SelectedItem.Id, TenLoaiThe, GiaTien, TrangThai);
            Load();
        }

        private void Delete()
        {
            if (SelectedItem == null) return;

            service.Delete(SelectedItem.Id);
            Load();
            Clear();
        }

        private void Clear()
        {
            SelectedItem = null;
            TenLoaiThe = string.Empty;
            GiaTien = 0m;
            TrangThai = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
