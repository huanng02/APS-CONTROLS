using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    internal class UserProfileViewModel : INotifyPropertyChanged
    {
        private readonly UserManagementService _service = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(name));
        }

        // ========================
        // Properties
        // ========================

        private string _ten = "";
        public string Ten
        {
            get => _ten;
            set
            {
                _ten = value;
                OnPropertyChanged(nameof(Ten));
            }
        }

        private string _username = "";
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        private string _role = "";
        public string Role
        {
            get => _role;
            set
            {
                _role = value;
                OnPropertyChanged(nameof(Role));
            }
        }

        // ========================
        // Command
        // ========================

        public ICommand SaveCommand { get; }

        public Action? CloseAction { get; set; }

        // ========================
        // Constructor
        // ========================

        public UserProfileViewModel()
        {
            LoadCurrentUser();

            SaveCommand = new RelayCommand(async _ =>
            {
                await SaveAsync();
            });
        }

        // ========================
        // Load User
        // ========================

        private void LoadCurrentUser()
        {
            Ten = CurrentUser.Ten ?? "";
            Username = CurrentUser.Username ?? "";
            Role = CurrentUser.Role ?? "";
        }

        // ========================
        // Save
        // ========================

        private async Task SaveAsync()
        {
            var result = await _service
                .UpdateCurrentUserProfileAsync(
                    CurrentUser.Id,
                    Ten,
                    Username);

            if (!result.Success)
            {
                MessageBox.Show(
                    result.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // Update realtime session
            CurrentUser.Ten = Ten;
            CurrentUser.Username = Username;

            MessageBox.Show(
                result.Message,
                "Thành công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            CloseAction?.Invoke();
        }
    }
}