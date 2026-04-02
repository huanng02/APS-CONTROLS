using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LichSuViewModel : INotifyPropertyChanged
    {
        DatabaseService db = new DatabaseService();

        public ObservableCollection<LichSuXe> DanhSachLichSu { get; set; }

        private List<LichSuXe> TatCaLichSu = new List<LichSuXe>();

        public LichSuViewModel()
        {
            DanhSachLichSu = new ObservableCollection<LichSuXe>();

            var list = db.LayLichSu();

            foreach (var xe in list)
            {
                DanhSachLichSu.Add(xe);
                TatCaLichSu.Add(xe);
            }
        }

        private string _tuKhoaTimKiem;

        public string TuKhoaTimKiem
        {
            get { return _tuKhoaTimKiem; }
            set
            {
                _tuKhoaTimKiem = value;
                OnPropertyChanged(nameof(TuKhoaTimKiem));
                TimKiem();
            }
        }

        private void TimKiem()
        {
            DanhSachLichSu.Clear();

            var ketQua = TatCaLichSu
                .Where(x => x.BienSo.ToLower().Contains(_tuKhoaTimKiem.ToLower()))
                .ToList();

            foreach (var xe in ketQua)
            {
                DanhSachLichSu.Add(xe);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}