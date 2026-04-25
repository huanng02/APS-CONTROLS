using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class ExcelImportExportViewModel : BaseViewModel
    {
        private readonly ExcelImportService _importService = new ExcelImportService();
        private readonly ExcelExportService _exportService = new ExcelExportService();
        private readonly DatabaseService _db = new DatabaseService();

        public ObservableCollection<RFIDCardImportModel> PreviewRows { get; } = new();

        private int _progress;
        public int Progress 
        { 
            get => _progress; 
            set { _progress = value; OnPropertyChanged(nameof(Progress)); } 
        }

        private string _statusSummary = string.Empty;
        public string StatusSummary 
        { 
            get => _statusSummary; 
            set { _statusSummary = value; OnPropertyChanged(nameof(StatusSummary)); } 
        }

        public ICommand ImportExcelCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ConfirmImportCommand { get; }

        private CancellationTokenSource? _cts;

        public ExcelImportExportViewModel()
        {
            ImportExcelCommand = new RelayCommand(async _ => await ImportExcel());
            ExportExcelCommand = new RelayCommand(async _ => await ExportExcel());
            ConfirmImportCommand = new RelayCommand(async _ => await ConfirmImport());

            // lazy load: do not call DB in ctor. Provide async refresh method if needed.
        }

        private async Task ImportExcel()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Excel Files|*.xlsx;*.xls" };
            if (dlg.ShowDialog() != true) return;

            _cts = new CancellationTokenSource();
            var progress = new Progress<int>(p => Progress = p);

            var rows = await _importService.ReadExcelAsync(dlg.FileName, progress, _cts.Token);
            PreviewRows.Clear();

            // validate duplicates in file
            var groups = rows.GroupBy(r => r.CardUID).ToDictionary(g => g.Key, g => g.Count());
            // Get existing UIDs from DB via existing GetRFIDCards method
            var existingUids = _db.GetRFIDCards().Select(x => x.UID ?? x.BienSo ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // load lookups for display (LoaiXe, LoaiVe)
            var loaiXeMap = new Dictionary<int, string>();
            var loaiVeMap = new Dictionary<int, string>();
            try
            {
                loaiXeMap = _db.GetLoaiXe().ToDictionary(x => x.Id, x => x.TenLoai ?? string.Empty);
                loaiVeMap = _db.GetLoaiVe().ToDictionary(x => x.Id, x => x.TenLoai ?? string.Empty);
            }
            catch { }

            foreach (var r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.CardUID))
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = "CardUID missing";
                }
                else if (groups[r.CardUID] > 1)
                {
                    r.Status = ImportStatus.DUPLICATE_FILE;
                    r.StatusMessage = "Duplicate in file";
                }
                else if (existingUids.Contains(r.CardUID))
                {
                    r.Status = ImportStatus.DUPLICATE_DB;
                    r.StatusMessage = "Exists in DB";
                }
                else if (r.NgayDangKy.HasValue && r.NgayHetHan.HasValue && r.NgayDangKy > r.NgayHetHan)
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = "NgayDangKy > NgayHetHan";
                }
                else
                {
                    r.Status = ImportStatus.VALID;
                    r.StatusMessage = "OK";
                }

                // populate display texts
                if (r.LoaiXeId.HasValue && loaiXeMap.TryGetValue(r.LoaiXeId.Value, out var lx)) r.LoaiXeText = lx;
                if (r.LoaiVeId.HasValue && loaiVeMap.TryGetValue(r.LoaiVeId.Value, out var lv)) r.LoaiVeText = lv;

                PreviewRows.Add(r);
            }
        }

        private async Task ConfirmImport()
        {
            // default strategy: SKIP duplicates
            var toInsert = PreviewRows.Where(r => r.Status == ImportStatus.VALID).ToList();
            if (!toInsert.Any())
            {
                StatusSummary = "No valid rows to import.";
                return;
            }

            // map LoaiXe/LoaiVe from text to id using ImportMappingService (ensures we don't insert invalid FK)
            var mapper = new ImportMappingService();
            var loaiXeMap = mapper.LoadLoaiXeMap();
            var loaiVeMap = mapper.LoadLoaiVeMap();

            // apply mapping and default dates
            var rowsReady = new List<RFIDCardImportModel>();
            foreach (var r in toInsert)
            {
                // try map LoaiXe/LoaiVe using any available text fields
                string lxText = string.IsNullOrWhiteSpace(r.LoaiXeTextRaw) ? r.LoaiXeText : r.LoaiXeTextRaw;
                string lvText = string.IsNullOrWhiteSpace(r.LoaiVeTextRaw) ? r.LoaiVeText : r.LoaiVeTextRaw;

                var lxId = mapper.MapLoaiXe(lxText ?? string.Empty, loaiXeMap);
                var lvId = mapper.MapLoaiVe(lvText ?? string.Empty, loaiVeMap);

                if (!lxId.HasValue)
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = $"LoaiXe '{lxText}' not recognized";
                    continue;
                }
                if (!lvId.HasValue)
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = $"LoaiVe '{lvText}' not recognized";
                    continue;
                }

                r.LoaiXeId = lxId;
                r.LoaiVeId = lvId;

                // default NgayDangKy to today if missing
                if (!r.NgayDangKy.HasValue) r.NgayDangKy = DateTime.Today;
                // If LoaiVe is monthly, default NgayHetHan = NgayDangKy + 1 month when missing
                var nv = ImportNormalization.Normalize(lvText ?? string.Empty);
                if ((nv.Contains("THANG") || nv.Contains("VE THANG") || nv.Contains("MONTH")) && !r.NgayHetHan.HasValue)
                {
                    r.NgayHetHan = r.NgayDangKy?.AddMonths(1);
                }

                rowsReady.Add(r);
            }

            if (!rowsReady.Any())
            {
                StatusSummary = "No rows left after mapping/validation.";
                return;
            }
            // map to RFIDCard model
            var models = toInsert.Select(r => new RFIDCard
            {
                UID = r.CardUID,
                BienSo = r.BienSo,
                LoaiVeId = r.LoaiVeId ?? 0,
                LoaiXeId = r.LoaiXeId ?? 0,
                // Preserve NgayDangKy if provided; otherwise default to now so records have a sensible registration date
                NgayTao = r.NgayDangKy ?? DateTime.Now,
                NgayHetHan = r.NgayHetHan,
                TrangThai = r.TrangThai
            }).ToList();

            // bulk upsert via DB service with progress reporting
            var progress = new Progress<int>(p => Progress = p);

            try
            {
                var result = await Task.Run(() => _db.BulkUpsertRFIDCards(models, batchSize: 1000, progress: progress, updateExisting: false));
                StatusSummary = $"Inserted: {result.Inserted}, Updated: {result.Updated}";

                // refresh UI: append inserted items to PreviewRows and optionally to global DB list
                // We'll append the inserted ones to Items in main view later; here we at least report counts.
            }
            catch (Exception ex)
            {
                StatusSummary = "Import failed: " + ex.Message;
                try { LoggingService.Instance.LogError("ImportFailed", "ExcelImportExportViewModel", ex.Message, ex); } catch { }
            }
        }

        private async Task ExportExcel()
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var cards = _db.GetRFIDCards();
            await Task.Run(() => _exportService.ExportRFIDCardsToExcel(cards, folder));
        }
    }
}
