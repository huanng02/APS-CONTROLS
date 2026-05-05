using System;
using System.IO;
using System.Threading;
using ClosedXML.Excel;

namespace QuanLyGiuXe.Services
{
    public static class FileSaveHelpers
    {
        /// <summary>
        /// Save an XLWorkbook safely: write to temp file first (exclusive), then move to destination.
        /// If destination is locked, retries are attempted. If still locked, returns the temp file path.
        /// </summary>
        public static string SaveWorkbookSafe(XLWorkbook workbook, string destinationPath, int maxRetries = 5, int delayMs = 300)
        {
            if (workbook == null) throw new ArgumentNullException(nameof(workbook));
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentNullException(nameof(destinationPath));

            var destDir = Path.GetDirectoryName(destinationPath) ?? Path.GetTempPath();
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            var tempFile = Path.Combine(destDir,
                Path.GetFileNameWithoutExtension(destinationPath) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".tmp" + Path.GetExtension(destinationPath));

            // Write to temp file with exclusive access
            using (var fs = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                workbook.SaveAs(fs);
                fs.Flush(true);
            }

            int attempt = 0;
            while (true)
            {
                try
                {
                    // If destination exists, try to delete it first
                    if (File.Exists(destinationPath))
                    {
                        try { File.Delete(destinationPath); } catch { }
                    }

                    File.Move(tempFile, destinationPath);
                    return destinationPath;
                }
                catch (IOException)
                {
                    attempt++;
                    if (attempt >= maxRetries)
                    {
                        // Give up and return temp file path so caller can notify user
                        return tempFile;
                    }
                    Thread.Sleep(delayMs * attempt);
                }
                catch (UnauthorizedAccessException)
                {
                    // Permission issue -- return temp path for inspection
                    return tempFile;
                }
            }
        }
    }
}
