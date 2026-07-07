using System.ComponentModel;

namespace QuanLyGiuXe.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
