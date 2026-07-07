using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class RealtimeEventFeedView : UserControl
    {
        public RealtimeEventFeedView()
        {
            InitializeComponent();
            this.Loaded += RealtimeEventFeedView_Loaded;
            this.Unloaded += RealtimeEventFeedView_Unloaded;
        }

        private void RealtimeEventFeedView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is RealtimeEventFeedViewModel vm)
            {
                vm.DisplayEvents.CollectionChanged += DisplayEvents_CollectionChanged;
                
                // Immediately scroll to end on load to see latest events
                if (vm.AutoScroll)
                {
                    ScrollFeed.ScrollToEnd();
                }
            }
        }

        private void RealtimeEventFeedView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is RealtimeEventFeedViewModel vm)
            {
                vm.DisplayEvents.CollectionChanged -= DisplayEvents_CollectionChanged;
            }
        }

        private void DisplayEvents_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                if (DataContext is RealtimeEventFeedViewModel vm && vm.AutoScroll)
                {
                    // Delay slightly or scroll immediately to let layout update
                    ScrollFeed.ScrollToEnd();
                }
            }
        }
    }
}
