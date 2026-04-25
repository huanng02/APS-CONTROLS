using System;
using System.Globalization;
using System.Windows.Data;
using System.Collections;
using System.Linq;
using System.Windows.Data;

namespace QuanLyGiuXe.Converters
{
    // MultiValueConverter: [0] = current item, [1] = IList (source collection)
    public class ItemToGlobalIndexConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values == null || values.Length < 2) return "";
                var item = values[0];
                var list = values[1] as IList;
                if (item == null || list == null) return "";

                // If there's a CollectionView applied (filter/sort), find index within that view
                try
                {
                    var view = CollectionViewSource.GetDefaultView(list);
                    if (view != null)
                    {
                        var idx = view.Cast<object>().ToList().IndexOf(item);
                        if (idx >= 0) return (idx + 1).ToString();
                    }
                }
                catch { }

                var idx2 = list.IndexOf(item);
                if (idx2 < 0) return "";
                return (idx2 + 1).ToString();
            }
            catch { }
            return "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
