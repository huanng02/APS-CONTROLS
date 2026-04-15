using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyGiuXe.Views
{
    public partial class LoaiXeView : UserControl
    {
        public LoaiXeView()
        {
            InitializeComponent();
            DataContext = new LoaiXeViewModel();
        }
    }
}
