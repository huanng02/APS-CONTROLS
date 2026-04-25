using System.Windows;
using System.Windows.Controls;
using AForge.Video.DirectShow;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class CameraSettingsWindow : Window
    {
        private const string AutoOption = "(Tự động)";
        private readonly FilterInfoCollection _cameras;
        private readonly ComboBox[] _combos;

        public CameraSettingsWindow()
        {
            InitializeComponent();

            _cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            _combos = [cbVaoToanCanh, cbVaoBienSo, cbRaToanCanh, cbRaBienSo];
            LoadCameras();
        }

        private void LoadCameras()
        {
            var cfg = AppConfig.Load().Cameras;
            string[] saved = [cfg.VaoToanCanh, cfg.VaoBienSo, cfg.RaToanCanh, cfg.RaBienSo];

            foreach (var cb in _combos)
            {
                cb.Items.Add(AutoOption);
                for (int i = 0; i < _cameras.Count; i++)
                    cb.Items.Add(_cameras[i].Name);
            }

            txtCameraCount.Text = $"📷 Phát hiện {_cameras.Count} camera trên máy";

            for (int i = 0; i < _combos.Length; i++)
                _combos[i].SelectedItem = FindMatch(saved[i]) ?? AutoOption;
        }

        private string? FindMatch(string cfgName)
        {
            if (string.IsNullOrEmpty(cfgName)) return null;
            for (int i = 0; i < _cameras.Count; i++)
                if (_cameras[i].Name.Contains(cfgName, StringComparison.OrdinalIgnoreCase))
                    return _cameras[i].Name;
            return null;
        }

        private static string Pick(ComboBox cb) =>
            cb.SelectedItem?.ToString() is string s && s != AutoOption ? s : "";

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var config = AppConfig.Load();
            config.Cameras.VaoToanCanh = Pick(cbVaoToanCanh);
            config.Cameras.VaoBienSo = Pick(cbVaoBienSo);
            config.Cameras.RaToanCanh = Pick(cbRaToanCanh);
            config.Cameras.RaBienSo = Pick(cbRaBienSo);
            config.Save();
            MessageBox.Show("✅ Đã lưu. Khởi động lại app để áp dụng.", "Thành công");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
