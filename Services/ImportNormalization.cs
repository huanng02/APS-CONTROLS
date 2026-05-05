using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace QuanLyGiuXe.Services
{
    public static class ImportNormalization
    {
        // Remove diacritics (accents) and normalize spaces and uppercase
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Normalize text for human-readable names (keep single spaces)
        /// - Trim
        /// - Remove diacritics
        /// - Uppercase
        /// - Collapse multiple spaces to single space
        /// - Remove non-alphanumeric characters except space
        /// Example: "  xe   máy " => "XE MAY"
        /// </summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Trim
            var s = input.Trim();
            // remove diacritics
            s = RemoveDiacritics(s);
            // uppercase
            s = s.ToUpperInvariant();
            // collapse multiple whitespace to single space
            s = Regex.Replace(s, "\\s+", " ");
            // remove any character that is not A-Z, 0-9 or space
            s = Regex.Replace(s, "[^A-Z0-9 ]", "");
            return s;
        }
    }
}
