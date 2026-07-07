using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RFIDGiaHanHistoryViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _db = new DatabaseService();
        
        private ObservableCollection<GiaHanRFIDLog> _history;
        public ObservableCollection<GiaHanRFIDLog> History
        {
            get => _history;
            set { _history = value; OnPropertyChanged(); }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        private DateTime? _fromDate = DateTime.Now.Date.AddDays(-30);
        public DateTime? FromDate
        {
            get => _fromDate;
            set { _fromDate = value; OnPropertyChanged(); }
        }

        private DateTime? _toDate = DateTime.Now.Date;
        public DateTime? ToDate
        {
            get => _toDate;
            set { _toDate = value; OnPropertyChanged(); }
        }

        public ICommand LoadCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand FilterCommand { get; }

        public RFIDGiaHanHistoryViewModel()
        {
            LoadCommand = new RelayCommand(_ => LoadHistory());
            SearchCommand = new RelayCommand(_ => SearchHistory());
            FilterCommand = new RelayCommand(_ => FilterByDate());
            
            LoadHistory();
        }

        /// <summary>
        /// Tải toàn bộ lịch sử (không filter)
        /// </summary>
        public void LoadHistory()
        {
            var data = _db.GetGiaHanHistory(null, null, null);
            History = new ObservableCollection<GiaHanRFIDLog>(data);
        }

        /// <summary>
        /// Tìm kiếm theo CardUID, Biển số hoặc CardId
        /// </summary>
        public void SearchHistory()
        {
            var data = _db.GetGiaHanHistory(SearchText, FromDate, ToDate);
            History = new ObservableCollection<GiaHanRFIDLog>(data);
        }

        /// <summary>
        /// Lọc theo khoảng ngày
        /// </summary>
        public void FilterByDate()
        {
            var data = _db.GetGiaHanHistory(SearchText, FromDate, ToDate);
            History = new ObservableCollection<GiaHanRFIDLog>(data);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
