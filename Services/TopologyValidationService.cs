using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Centralized topology validation engine.
    /// Read-only: never modifies database, configuration files, or auto-corrects data.
    /// </summary>
    public class TopologyValidationService
    {
        private static readonly Lazy<TopologyValidationService> _lazy =
            new(() => new TopologyValidationService());

        public static TopologyValidationService Instance => _lazy.Value;

        private TopologyValidationService() { }

        /// <summary>
        /// Analyze the current topology configuration and produce validation results.
        /// This method is read-only and has no side effects.
        /// </summary>
        public TopologyValidationResult ValidateTopology()
        {
            var result = new TopologyValidationResult();

            try
            {
                // Load all topology data once (read-only)
                var zones = ParkingTopologyService.Instance.GetZones();
                var gates = ParkingTopologyService.Instance.GetGates();
                var lanes = ParkingTopologyService.Instance.GetLanes();
                var controllers = ParkingTopologyService.Instance.GetControllers();
                var allMappings = ReaderLaneMappingService.Instance.GetAll();

                // Build lookup sets
                var laneIds = new HashSet<int>(lanes.Select(l => l.Id));

                // Track lanes that triggered Rule 7 to avoid duplication with Rule 1
                var lanesWithNoRelay = new HashSet<int>();

                // ─── RULE 7: Lane Without Relay Route (Error) ───────────────
                // Active lane with zero enabled reader mappings → no relay path
                foreach (var lane in lanes.Where(l => l.IsActive))
                {
                    var enabledMappings = allMappings
                        .Where(m => m.LaneId == lane.Id && m.IsEnabled)
                        .ToList();

                    if (enabledMappings.Count == 0)
                    {
                        lanesWithNoRelay.Add(lane.Id);
                        result.Errors.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "LANE_NO_RELAY",
                            Message = $"Lane '{lane.LaneName}' has no relay route.",
                            EntityType = "Lane",
                            EntityName = lane.LaneName
                        });
                    }
                }

                // ─── RULE 1: Lane Without Reader (Warning) ──────────────────
                // Lane with no active reader mapping (skip lanes already flagged by Rule 7)
                foreach (var lane in lanes)
                {
                    if (lanesWithNoRelay.Contains(lane.Id))
                        continue;

                    var activeMappings = allMappings
                        .Where(m => m.LaneId == lane.Id && m.IsEnabled)
                        .ToList();

                    if (activeMappings.Count == 0)
                    {
                        result.Warnings.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Code = "LANE_NO_READER",
                            Message = $"Lane '{lane.LaneName}' has no assigned reader.",
                            EntityType = "Lane",
                            EntityName = lane.LaneName
                        });
                    }
                }

                // ─── RULE 2: Gate Without Lane (Warning) ────────────────────
                foreach (var gate in gates)
                {
                    var gateLanes = lanes.Where(l => l.GateId == gate.Id).ToList();
                    if (gateLanes.Count == 0)
                    {
                        result.Warnings.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Code = "GATE_NO_LANE",
                            Message = $"Gate '{gate.GateName}' contains no lanes.",
                            EntityType = "Gate",
                            EntityName = gate.GateName
                        });
                    }
                }

                // ─── RULE 3: Zone Without Gate (Warning) ────────────────────
                // Zones connect to gates through lanes (LaneConfig.ZoneId).
                // A zone with zero lanes assigned effectively has no gates.
                foreach (var zone in zones)
                {
                    var zoneLanes = lanes.Where(l => l.ZoneId == zone.Id).ToList();
                    if (zoneLanes.Count == 0)
                    {
                        result.Warnings.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Code = "ZONE_NO_GATE",
                            Message = $"Zone '{zone.ZoneName}' contains no gates.",
                            EntityType = "Zone",
                            EntityName = zone.ZoneName
                        });
                    }
                }

                // ─── RULE 4: Reader Assigned To Missing Lane (Error) ────────
                foreach (var mapping in allMappings.Where(m => m.IsEnabled))
                {
                    if (!laneIds.Contains(mapping.LaneId))
                    {
                        string readerName = $"Reader {mapping.ReaderNo}";
                        result.Errors.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "READER_MISSING_LANE",
                            Message = $"Reader '{readerName}' references missing lane.",
                            EntityType = "Reader",
                            EntityName = readerName
                        });
                    }
                }

                // ─── RULE 5: Controller Capacity Violation (Error) ──────────
                // Reuse ControllerLaneValidationService
                try
                {
                    var cfg = AppConfig.Load();
                    var controllerType = cfg.ZKTeco.ControllerType;
                    var enabledMappings = allMappings.Where(m => m.IsEnabled);

                    var capacityResult = ControllerLaneValidationService.Instance
                        .ValidateLaneCapacity(controllerType, enabledMappings);

                    if (!capacityResult.IsValid)
                    {
                        result.Errors.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "CONTROLLER_CAPACITY",
                            Message = "Controller exceeds supported lane capacity.",
                            EntityType = "Controller",
                            EntityName = controllerType.ToString()
                        });
                    }
                }
                catch
                {
                    // If config cannot be loaded, skip this rule
                }

                // ─── RULE 6: Direction Consistency Conflict (Warning) ───────
                // Reuse LaneDirectionConsistencyService
                foreach (var lane in lanes)
                {
                    try
                    {
                        var analysis = LaneDirectionConsistencyService.Instance
                            .AnalyzeLaneDirection(lane.Id);

                        if (analysis.IsMixed)
                        {
                            result.Warnings.Add(new ValidationIssue
                            {
                                Severity = ValidationSeverity.Warning,
                                Code = "DIRECTION_MIXED",
                                Message = $"Lane '{lane.LaneName}' contains readers with mixed directions.",
                                EntityType = "Lane",
                                EntityName = lane.LaneName
                            });
                        }
                    }
                    catch
                    {
                        // Skip lane if analysis fails
                    }
                }
            }
            catch (Exception ex)
            {
                // If topology data cannot be loaded, report a single error
                result.Errors.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "TOPOLOGY_LOAD_FAILED",
                    Message = $"Failed to load topology data: {ex.Message}",
                    EntityType = "System",
                    EntityName = "TopologyValidation"
                });
            }

            // Apply localization post-processing
            foreach (var issue in result.Errors) LocalizeIssue(issue);
            foreach (var issue in result.Warnings) LocalizeIssue(issue);
            foreach (var issue in result.Infos) LocalizeIssue(issue);

            return result;
        }

        private void LocalizeIssue(ValidationIssue issue)
        {
            // Map EntityType to Vietnamese
            issue.TranslatedEntityType = issue.EntityType switch
            {
                "Lane" => "Làn xe",
                "Gate" => "Cổng kiểm soát",
                "Zone" => "Phân khu",
                "Reader" => "Đầu đọc",
                "Controller" => "Bộ điều khiển",
                "System" => "Hệ thống",
                _ => issue.EntityType
            };

            // Map Severity & Impact to Vietnamese
            switch (issue.Severity)
            {
                case ValidationSeverity.Error:
                    issue.Impact = "Không thể vận hành";
                    break;
                case ValidationSeverity.Warning:
                    issue.Impact = "Có thể vận hành nhưng cần kiểm tra";
                    break;
                case ValidationSeverity.Info:
                    issue.Impact = "Chỉ mang tính tham khảo";
                    break;
            }

            // Map Code to Title and Description
            switch (issue.Code)
            {
                case "LANE_NO_READER":
                    issue.Title = "Làn chưa được gán đầu đọc";
                    issue.Description = $"Làn '{issue.EntityName}' hiện chưa có đầu đọc RFID nào được cấu hình.";
                    break;

                case "GATE_NO_LANE":
                    issue.Title = "Cổng chưa có làn hoạt động";
                    issue.Description = $"Cổng '{issue.EntityName}' hiện chưa chứa làn xe nào.";
                    break;

                case "ZONE_NO_GATE":
                case "ZONE_NO_LANE":
                    issue.Title = "Khu vực chưa được sử dụng";
                    issue.Description = $"Khu vực '{issue.EntityName}' chưa có làn xe nào được gán.";
                    break;

                case "READER_MISSING_LANE":
                    issue.Title = "Đầu đọc tham chiếu tới làn không tồn tại";
                    issue.Description = $"Đầu đọc '{issue.EntityName}' đang được cấu hình tới một làn xe không tồn tại trong hệ thống.";
                    break;

                case "CONTROLLER_CAPACITY":
                    issue.Title = "Vượt quá khả năng phần cứng";
                    issue.Description = "Bộ điều khiển hiện tại không hỗ trợ số lượng làn đang được cấu hình.";
                    break;

                case "DIRECTION_MIXED":
                    issue.Title = "Mâu thuẫn chiều hoạt động";
                    issue.Description = "Các đầu đọc trong cùng một làn đang được cấu hình với nhiều chiều khác nhau (Vào/Ra).";
                    break;

                case "LANE_NO_RELAY":
                    issue.Title = "Làn chưa cấu hình cổng kết nối barrier (Relay)";
                    issue.Description = $"Làn '{issue.EntityName}' hoạt động nhưng không có cấu hình đầu đọc kích hoạt relay mở barrier.";
                    break;

                case "TOPOLOGY_LOAD_FAILED":
                    issue.Title = "Lỗi tải dữ liệu sơ đồ";
                    issue.Description = $"Không thể tải dữ liệu sơ đồ hệ thống: {issue.Message}";
                    break;

                default:
                    issue.Title = issue.Code;
                    issue.Description = issue.Message;
                    break;
            }
        }
    }
}
