using System;
using System.Windows.Input;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class PersonnelAndCardTabsViewModel : BaseViewModel
    {
        private int _activeTabIndex;
        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (_activeTabIndex != value)
                {
                    _activeTabIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public PersonnelExplorerViewModel PersonnelViewModel { get; }
        public RFIDCardViewModel NonRenewableCardViewModel { get; }

        public PersonnelAndCardTabsViewModel()
        {
            PersonnelViewModel = new PersonnelExplorerViewModel();
            NonRenewableCardViewModel = new RFIDCardViewModel(true);
        }
    }
}
