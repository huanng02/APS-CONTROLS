using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class LaneDirectionAnalysisResult
    {
        public bool IsConsistent { get; set; }
        public bool IsMixed { get; set; }
        public LaneDirection? AutoDirection { get; set; }
        public List<LaneDirection> AllowedDirections { get; set; } = new();
        public LaneDirectionBadge Badge { get; set; }
        public string BadgeText { get; set; } = string.Empty;
        public string BadgeTooltip { get; set; } = string.Empty;
    }

    public class LaneDirectionConsistencyService
    {
        private static readonly Lazy<LaneDirectionConsistencyService> _lazy =
            new(() => new LaneDirectionConsistencyService());

        public static LaneDirectionConsistencyService Instance => _lazy.Value;

        private LaneDirectionConsistencyService() { }

        public LaneDirectionAnalysisResult AnalyzeLaneDirection(int laneId)
        {
            var result = new LaneDirectionAnalysisResult();

            // Retrieve only enabled readers mapped to this lane
            var readers = ReaderLaneMappingService.Instance.GetMappingsByLane(laneId)
                .Where(r => r.IsEnabled)
                .ToList();

            if (!readers.Any())
            {
                // Rule 4: No readers exist
                result.IsConsistent = false;
                result.IsMixed = false;
                result.AutoDirection = null;
                result.AllowedDirections = new List<LaneDirection>
                {
                    LaneDirection.In,
                    LaneDirection.Out,
                    LaneDirection.Maintenance
                };
                result.Badge = LaneDirectionBadge.None;
                result.BadgeText = string.Empty;
                result.BadgeTooltip = string.Empty;
                return result;
            }

            // Group by reader directions mapped to enums
            var distinctDirections = readers
                .Select(r => r.Direction.ToLaneDirection())
                .Distinct()
                .ToList();

            if (distinctDirections.Count == 1)
            {
                var commonDirection = distinctDirections.First();
                result.IsConsistent = true;
                result.IsMixed = false;
                result.AutoDirection = commonDirection;

                if (commonDirection == LaneDirection.In)
                {
                    // Rule 1: All readers are IN
                    result.AllowedDirections = new List<LaneDirection>
                    {
                        LaneDirection.In,
                        LaneDirection.Maintenance
                    };
                    result.Badge = LaneDirectionBadge.AutoIn;
                    result.BadgeText = "AUTO IN";
                    result.BadgeTooltip = string.Empty;
                }
                else
                {
                    // Rule 2: All readers are OUT
                    result.AllowedDirections = new List<LaneDirection>
                    {
                        LaneDirection.Out,
                        LaneDirection.Maintenance
                    };
                    result.Badge = LaneDirectionBadge.AutoOut;
                    result.BadgeText = "AUTO OUT";
                    result.BadgeTooltip = string.Empty;
                }
            }
            else
            {
                // Rule 3: Mixed directions
                result.IsConsistent = false;
                result.IsMixed = true;
                result.AutoDirection = null;
                result.AllowedDirections = new List<LaneDirection>
                {
                    LaneDirection.In,
                    LaneDirection.Out,
                    LaneDirection.Maintenance
                };
                result.Badge = LaneDirectionBadge.Mixed;
                result.BadgeText = "MIXED";
                result.BadgeTooltip = "Lane contains readers with different directions.";
            }

            return result;
        }
    }
}
