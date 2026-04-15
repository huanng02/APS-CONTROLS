using System;
using System.Collections.ObjectModel;
using System.Linq;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class QuanLyTheViewModel
    {
        private readonly DatabaseService db = new DatabaseService();

        public ObservableCollection<RFIDCards> DanhSachRFID { get; set; }

        public QuanLyTheViewModel()
        {
            DanhSachRFID = new ObservableCollection<RFIDCards>();
            LoadData();
        }

        // ================= FILTER =================

        private string _filterUID;
        public string FilterUID
        {
            get => _filterUID;
            set
            {
                _filterUID = value;
                LoadData();
            }
        }

        private string _filterBienSo;
        public string FilterBienSo
        {
            get => _filterBienSo;
            set
            {
                _filterBienSo = value;
                LoadData();
            }
        }

        private string _filterLoaiThe = "All";
        public string FilterLoaiThe
        {
            get => _filterLoaiThe;
            set
            {
                _filterLoaiThe = value;
                LoadData();
            }
        }

        private string _filterTrangThai = "All";
        public string FilterTrangThai
        {
            get => _filterTrangThai;
            set
            {
                _filterTrangThai = value;
                LoadData();
            }
        }

        // ================= LOAD DATA =================

        public void LoadData()
        {
            var data = db.LayDanhSachRFIDCardss();

            var query = data.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterUID))
                query = query.Where(x => x.CardUID != null &&
                                         x.CardUID.Contains(FilterUID));

            if (!string.IsNullOrWhiteSpace(FilterBienSo))
                query = query.Where(x => x.BienSo != null &&
                                         x.BienSo.Contains(FilterBienSo));

            if (!string.IsNullOrWhiteSpace(FilterLoaiThe) &&
                FilterLoaiThe != "All")
                query = query.Where(x => x.LoaiThe == FilterLoaiThe);

            if (!string.IsNullOrWhiteSpace(FilterTrangThai) &&
                FilterTrangThai != "All")
                query = query.Where(x => x.TrangThai == FilterTrangThai);

            DanhSachRFID.Clear();

            foreach (var item in query)
                DanhSachRFID.Add(item);
        }

        // ================= MANUAL ACTIONS =================

        public void Refresh()
        {
            LoadData();
        }

        public void ResetFilter()
        {
            FilterUID = "";
            FilterBienSo = "";
            FilterLoaiThe = "All";
            FilterTrangThai = "All";

            LoadData();
        }
    }
}