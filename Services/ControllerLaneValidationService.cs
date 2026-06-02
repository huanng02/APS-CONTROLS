using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class ControllerLaneValidationService
    {
        public static ControllerLaneValidationService Instance { get; } = new();

        public ValidationResult ValidateLaneCapacity(
            ControllerType type,
            IEnumerable<ReaderLaneMapping> mappings)
        {
            var capability = ControllerCapabilityRegistry.GetCapability(type);
            
            var distinctLaneIds = mappings
                .Where(m => m.IsEnabled)
                .Select(m => m.LaneId)
                .Distinct()
                .ToList();

            int distinctLaneCount = distinctLaneIds.Count;

            if (distinctLaneCount > capability.MaxSupportedLanes)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Selected controller supports maximum {capability.MaxSupportedLanes} lanes. Current configuration uses {distinctLaneCount} lanes. Please reduce lane assignments before saving."
                };
            }

            return new ValidationResult
            {
                IsValid = true,
                ErrorMessage = string.Empty
            };
        }
    }
}
