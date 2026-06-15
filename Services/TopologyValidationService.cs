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
                var allMappings = ReaderLaneMappingService.Instance.GetAllDraft();

                // Build lookup sets
                var laneIds = new HashSet<int>(lanes.Select(l => l.Id));

                // ─── RULE 1: Lane Without Reader (Warning) ──────────────────
                // Lane with no active reader mapping
                foreach (var lane in lanes)
                {
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
                    var cfg = AppConfig.LoadDraft();
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

                // ─── RULE 8: Mismatched Controller and Lane Site/Gate (Error) ─
                try
                {
                    var draftCfg = AppConfig.LoadDraft();
                    var configuredIp = draftCfg.ZKTeco.IpAddress;

                    if (string.IsNullOrEmpty(configuredIp))
                    {
                        result.Errors.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "CONTROLLER_IP_EMPTY",
                            Message = "Controller IP Address is empty.",
                            EntityType = "System",
                            EntityName = "ZKTeco Settings"
                        });
                    }
                    else
                    {
                        var matchingController = controllers.FirstOrDefault(c => string.Equals(c.IpAddress, configuredIp, StringComparison.OrdinalIgnoreCase));
                        if (matchingController == null)
                        {
                            result.Errors.Add(new ValidationIssue
                            {
                                Severity = ValidationSeverity.Error,
                                Code = "CONTROLLER_IP_NOT_FOUND",
                                Message = $"Configured IP Address '{configuredIp}' does not match any registered controller.",
                                EntityType = "Controller",
                                EntityName = configuredIp
                            });
                        }
                        else
                        {
                            if (matchingController.GateId == null)
                            {
                                result.Errors.Add(new ValidationIssue
                                {
                                    Severity = ValidationSeverity.Error,
                                    Code = "CONTROLLER_NO_GATE",
                                    Message = $"Controller '{matchingController.ControllerName}' is not assigned to any Gate.",
                                    EntityType = "Controller",
                                    EntityName = matchingController.ControllerName
                                });
                            }
                            else
                            {
                                int controllerGateId = matchingController.GateId.Value;
                                var controllerGate = gates.FirstOrDefault(g => g.Id == controllerGateId);
                                string controllerGateName = controllerGate?.GateName ?? $"(Gate ID {controllerGateId})";

                                // Validate that all enabled reader mappings point to lanes belonging to the same Gate as the active controller
                                var enabledMappings = allMappings.Where(m => m.IsEnabled).ToList();
                                foreach (var mapping in enabledMappings)
                                {
                                    var lane = lanes.FirstOrDefault(l => l.Id == mapping.LaneId);
                                    if (lane != null)
                                    {
                                        int laneGateId = lane.GateId ?? 0;
                                        if (laneGateId != controllerGateId)
                                        {
                                            var laneGate = gates.FirstOrDefault(g => g.Id == laneGateId);
                                            string laneGateName = laneGate?.GateName ?? $"(Gate ID {laneGateId})";
                                            
                                            result.Errors.Add(new ValidationIssue
                                            {
                                                Severity = ValidationSeverity.Error,
                                                Code = "LANE_GATE_MISMATCH",
                                                Message = $"Lane '{lane.LaneName}' (Gate: '{laneGateName}') does not belong to Gate '{controllerGateName}' of the active controller.",
                                                EntityType = "Lane",
                                                EntityName = lane.LaneName
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("TopologyValidationService", "ValidateTopology", "Error in Rule 8 validation", ex);
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

                case "CONTROLLER_IP_EMPTY":
                    issue.Title = "Địa chỉ IP bộ điều khiển trống";
                    issue.Description = "Địa chỉ IP của bộ điều khiển ZKTeco chưa được cấu hình.";
                    break;

                case "CONTROLLER_IP_NOT_FOUND":
                    issue.Title = "IP bộ điều khiển không khớp";
                    issue.Description = $"Địa chỉ IP '{issue.EntityName}' cấu hình trong cài đặt không khớp với bất kỳ bộ điều khiển nào đã đăng ký trong cơ sở dữ liệu.";
                    break;

                case "CONTROLLER_NO_GATE":
                    issue.Title = "Bộ điều khiển chưa gán cổng";
                    issue.Description = $"Bộ điều khiển '{issue.EntityName}' chưa được gán vào cổng kiểm soát nào trong sơ đồ thiết bị.";
                    break;

                case "LANE_GATE_MISMATCH":
                    issue.Title = "Mâu thuẫn cổng kiểm soát của làn xe";
                    issue.Description = $"Làn xe '{issue.EntityName}' không thuộc cùng một cổng kiểm soát với bộ điều khiển đang hoạt động.";
                    break;

                default:
                    issue.Title = issue.Code;
                    issue.Description = issue.Message;
                    break;
            }
        }
    }
}
