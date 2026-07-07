using System.Windows.Controls;

namespace QuanLyGiuXe.Views
{
    public partial class PersonnelAndCardTabsView : UserControl
    {
        public PersonnelAndCardTabsView()
        {
            InitializeComponent();
            TabNonRenewable.Content = new RFIDCardView(true);
        }
    }
}
