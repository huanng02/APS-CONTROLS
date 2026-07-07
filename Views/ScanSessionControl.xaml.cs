using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class ScanSessionControl : UserControl
    {
        public ScanSessionControl()
        {
            InitializeComponent();
        }

        // Raised when the user clicks the close button so the host can remove this control
        public event EventHandler? RequestClose;

        public async void SetSession(LichSuXe session)
        {
            if (session == null) return;
            PlateText.Text = session.BienSo ?? "-";
            TimeInText.Text = session.ThoiGianVao.ToString("yyyy-MM-dd HH:mm:ss");
            TimeOutText.Text = session.ThoiGianRa.HasValue ? session.ThoiGianRa.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            FeeText.Text = session.Tien.HasValue ? session.Tien.Value.ToString("N0") + " VNĐ" : "-";

            // Load plate images
            string? entryPlatePath = GetPlateCropPath(session.AnhVao);
            string? exitPlatePath = GetPlateCropPath(session.AnhRa);

            imgPlateIn.Source = LoadImageFromFile(entryPlatePath);
            imgPlateOut.Source = LoadImageFromFile(exitPlatePath);

            if (session.ThoiGianRa.HasValue)
            {
                ExitPlatePanel.Visibility = Visibility.Visible;
            }
            else
            {
                ExitPlatePanel.Visibility = Visibility.Collapsed;
            }

            // Default fallback
            UidText.Text = session.CardId.ToString();
            brdEmployeeInfo.Visibility = Visibility.Collapsed;
            brdNoEmployee.Visibility = Visibility.Visible;

            try
            {
                var db = new DatabaseService();
                var card = await db.GetRFIDCardByIdAsync(session.CardId);
                if (card != null)
                {
                    UidText.Text = card.UID;
                    
                    if (card.EmployeeId.HasValue)
                    {
                        var service = new EnterpriseCrudService();
                        Employee? emp = null;
                        try
                        {
                            emp = await Task.Run(() => service.GetEmployeeById(card.EmployeeId.Value));
                        }
                        catch { }

                        if (emp != null)
                        {
                            brdEmployeeInfo.Visibility = Visibility.Visible;
                            brdNoEmployee.Visibility = Visibility.Collapsed;

                            txtEmpName.Text = emp.FullName;
                            txtEmpCode.Text = emp.EmployeeCode;
                            txtEmpCompany.Text = string.IsNullOrWhiteSpace(emp.CompanyName) ? "-" : emp.CompanyName;
                            txtEmpDept.Text = string.IsNullOrWhiteSpace(emp.DepartmentName) ? "-" : emp.DepartmentName;
                            txtEmpPos.Text = string.IsNullOrWhiteSpace(emp.PositionName) ? "-" : emp.PositionName;

                            if (!string.IsNullOrEmpty(emp.Avatar))
                            {
                                imgAvatar.Source = ConvertBase64ToImage(emp.Avatar);
                            }
                            else
                            {
                                imgAvatar.Source = null;
                            }
                        }
                        else if (!string.IsNullOrEmpty(card.EmployeeName))
                        {
                            brdEmployeeInfo.Visibility = Visibility.Visible;
                            brdNoEmployee.Visibility = Visibility.Collapsed;

                            txtEmpName.Text = card.EmployeeName;
                            txtEmpCode.Text = card.EmployeeCode ?? "-";
                            txtEmpCompany.Text = "-";
                            txtEmpDept.Text = "-";
                            txtEmpPos.Text = "-";
                            imgAvatar.Source = null;
                        }
                    }
                }
            }
            catch { }
        }

        private static ImageSource? ConvertBase64ToImage(string? base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] binaryData = Convert.FromBase64String(base64);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(binaryData);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        private static string? GetPlateCropPath(string? folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return null;
            var resolvedFolder = QuanLyGiuXe.Services.ParkingImageService.ResolveSharedFolderPath(folderPath);
            if (System.IO.Directory.Exists(resolvedFolder))
            {
                string path = System.IO.Path.Combine(resolvedFolder, "plate_crop.jpg");
                if (System.IO.File.Exists(path)) return path;
            }
            return null;
        }

        private static ImageSource? LoadImageFromFile(string? path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
