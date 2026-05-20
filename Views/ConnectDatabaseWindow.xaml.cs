using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class ConnectDatabaseWindow : Window
    {
        private ConnectDatabaseViewModel _viewModel;

        public ConnectDatabaseWindow()
        {
            InitializeComponent();
            _viewModel = new ConnectDatabaseViewModel();
            this.DataContext = _viewModel;
            
            // Initial load for password
            txtPassword.Password = _viewModel.Password;

            _viewModel.CloseAction = () =>
            {
                this.DialogResult = _viewModel.DialogResult;
                this.Close();
            };
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ConnectDatabaseViewModel vm)
            {
                vm.Password = ((PasswordBox)sender).Password;
            }
        }
    }
}
