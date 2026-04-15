using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RFIDCardViewModel : INotifyPropertyChanged
    {
        private readonly RFIDCardService service = new RFIDCardService();

        public ObservableCollection<RFIDCard> Items { get; } = new ObservableCollection<RFIDCard>();

        private RFIDCard _selectedItem;
        public RFIDCard SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                if (_selectedItem != null)
                {
                    UID = _selectedItem.UID;
                    BienSo = _selectedItem.BienSo;
                    LoaiVeId = _selectedItem.LoaiVeId;
                    LoaiXeId = _selectedItem.LoaiXeId;
                    TrangThai = _selectedItem.TrangThai;
                }
            }
        }

        private string _uid;
        public string UID { get => _uid; set { _uid = value; OnPropertyChanged(nameof(UID)); } }

        private string _bienSo;
        public string BienSo { get => _bienSo; set { _bienSo = value; OnPropertyChanged(nameof(BienSo)); } }

        private int _loaiVeId;
        public int LoaiVeId { get => _loaiVeId; set { _loaiVeId = value; OnPropertyChanged(nameof(LoaiVeId)); } }

        private int _loaiXeId;
        public int LoaiXeId { get => _loaiXeId; set { _loaiXeId = value; OnPropertyChanged(nameof(LoaiXeId)); } }

        private string _trangThai;
        public string TrangThai { get => _trangThai; set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); } }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }

        public RFIDCardViewModel()
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
            foreach (var it in list) Items.Add(it);
        }

        private void Add()
        {
            service.Add(UID, BienSo, LoaiVeId, LoaiXeId, TrangThai);
            Load();
            Clear();
        }

        private void Update()
        {
            if (SelectedItem == null) return;
            service.Update(SelectedItem.Id, UID, BienSo, LoaiVeId, LoaiXeId, TrangThai);
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
            UID = string.Empty;
            BienSo = string.Empty;
            LoaiVeId = 0;
            LoaiXeId = 0;
            TrangThai = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
