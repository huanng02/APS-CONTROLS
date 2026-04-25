using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class ExcelHelper
    {
        public static List<RFIDCardImportModel> ReadRFIDCardsPreview(string path)
        {
            var list = new List<RFIDCardImportModel>();
            using (var wb = new XLWorkbook(path))
            {
                var ws = wb.Worksheets.First();
                var rows = ws.RangeUsed().RowsUsed().Skip(1);
                foreach (var r in rows)
                {
                    var m = new RFIDCardImportModel
                    {
                        CardUID = r.Cell(2).GetString().Trim(),
                        BienSo = r.Cell(3).GetString().Trim(),
                        LoaiXeId = ParseInt(r.Cell(4).GetString()),
                        LoaiVeId = ParseInt(r.Cell(5).GetString()),
                        NgayDangKy = ParseDate(r.Cell(6).GetString()),
                        NgayHetHan = ParseDate(r.Cell(7).GetString()),
                        TrangThai = r.Cell(8).GetString().Trim()
                    };
                    list.Add(m);
                }
            }
            return list;
        }

        private static int? ParseInt(string s) => int.TryParse(s, out var v) ? v : (int?)null;
        private static DateTime? ParseDate(string s) => DateTime.TryParse(s, out var d) ? d : (DateTime?)null;

        public static void WriteRFIDCards(string path, IEnumerable<RFIDCard> cards)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("RFIDCards");
            var headers = new[] { "Id", "CardUID", "BienSo", "LoaiXeId", "LoaiVeId", "NgayDangKy", "NgayHetHan", "TrangThai" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

            var headerRange = ws.Range(1,1,1,headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(44,62,80);
            headerRange.Style.Font.FontColor = XLColor.White;
            ws.SheetView.FreezeRows(1);

            int r = 2;
            foreach (var c in cards)
            {
                ws.Cell(r,1).Value = c.Id;
                ws.Cell(r,2).Value = c.UID;
                ws.Cell(r,3).Value = c.BienSo;
                ws.Cell(r,4).Value = c.LoaiXeId;
                ws.Cell(r,5).Value = c.LoaiVeId;
                ws.Cell(r,6).Value = c.NgayTao == DateTime.MinValue ? string.Empty : c.NgayTao.ToString("dd/MM/yyyy");
                ws.Cell(r,7).Value = c.NgayHetHan.HasValue ? c.NgayHetHan.Value.ToString("dd/MM/yyyy") : string.Empty;
                ws.Cell(r,8).Value = c.TrangThai;
                r++;
            }

            ws.RangeUsed().SetAutoFilter();
            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
        }
    }
}
