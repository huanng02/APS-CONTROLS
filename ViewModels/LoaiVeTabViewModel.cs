using System.Collections.ObjectModel;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LoaiVeTabViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public bool ShowOnlyNonRenewable { get; set; }
        public ObservableCollection<RFIDCards> Items { get; } = new ObservableCollection<RFIDCards>();

        // load data for this tab (synchronous small dataset)
        public void Load(RFIDCardService service)
        {
            Items.Clear();
            if (service == null) return;
            if (Id == 0)
            {
                var all = service.GetAll();
                if (ShowOnlyNonRenewable)
                {
                    try
                    {
                        var db = new DatabaseService();
                        var nonRenewableIds = new System.Collections.Generic.HashSet<int>(
                            db.GetLoaiVe().Where(x => !x.CoTheGiaHan).Select(x => x.Id)
                        );
                        all = all.Where(x => x.LoaiVeId.HasValue && nonRenewableIds.Contains(x.LoaiVeId.Value)).ToList();
                    }
                    catch { }
                }
                foreach (var it in all) Items.Add(it);
            }
            else
            {
                var list = service.GetByLoaiVe(Id);
                foreach (var it in list) Items.Add(it);
            }
        }
    }
}
