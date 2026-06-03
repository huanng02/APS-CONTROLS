using System.Collections.Generic;
using System.Linq;

namespace QuanLyGiuXe.Models
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;

        // Localized fields for Vietnamese UI
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string TranslatedEntityType { get; set; } = string.Empty;
    }

    public class TopologyValidationResult
    {
        public List<ValidationIssue> Infos { get; set; } = new();
        public List<ValidationIssue> Warnings { get; set; } = new();
        public List<ValidationIssue> Errors { get; set; } = new();

        public bool IsValid => Errors.Count == 0;
    }
}
