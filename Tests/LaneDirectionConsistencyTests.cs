using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Tests
{
    public static class LaneDirectionConsistencyTests
    {
        public static void Run()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("RUNNING LANE DIRECTION CONSISTENCY TESTS");
            Console.WriteLine("=================================================");

            try
            {
                var dbConfig = DbConnectionConfig.LoadFromFile();
                using (var conn = new System.Data.SqlClient.SqlConnection(dbConfig.BuildConnectionString()))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Id, CameraName, CameraKey, IpAddress, RtspUrl, LaneId, Direction, IsActive FROM dbo.Cameras";
                        using (var r = cmd.ExecuteReader())
                        {
                            Console.WriteLine("DIAGNOSTIC: Cameras in Database:");
                            while (r.Read())
                            {
                                Console.WriteLine($" - ID: {r[0]}, Name: {r[1]}, Key: {r[2]}, IP: {r[3]}, RTSP: {r[4]}, LaneId: {r[5]}, Dir: {r[6]}, Active: {r[7]}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DIAGNOSTIC CAMERA ERROR: " + ex.Message);
            }

            // 1. Backup original mappings
            var originalMappings = ReaderLaneMappingService.Instance.GetAll()
                .Select(m => new ReaderLaneMapping
                {
                    ReaderNo = m.ReaderNo,
                    LaneId = m.LaneId,
                    Direction = m.Direction,
                    IsEnabled = m.IsEnabled
                })
                .ToList();

            try
            {
                int testLaneId = 999;

                // Scenario 1: Reader1 = IN, Reader2 = IN
                Console.WriteLine("Running Scenario 1...");
                var mappingsS1 = new List<ReaderLaneMapping>
                {
                    new ReaderLaneMapping { ReaderNo = 1, LaneId = testLaneId, Direction = "IN", IsEnabled = true },
                    new ReaderLaneMapping { ReaderNo = 2, LaneId = testLaneId, Direction = "IN", IsEnabled = true }
                };
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS1);
                var resultS1 = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS1.IsConsistent, "S1: Should be consistent");
                Assert(resultS1.AutoDirection == LaneDirection.In, "S1: Auto direction should be IN");
                Assert(resultS1.AllowedDirections.Contains(LaneDirection.In), "S1: AllowedDirections should contain IN");
                Assert(!resultS1.AllowedDirections.Contains(LaneDirection.Out), "S1: AllowedDirections should NOT contain OUT");
                Assert(resultS1.AllowedDirections.Contains(LaneDirection.Maintenance), "S1: AllowedDirections should contain MAINTENANCE");
                Assert(resultS1.Badge == LaneDirectionBadge.AutoIn, "S1: Badge should be AutoIn");

                // Scenario 2: Reader1 = OUT, Reader2 = OUT
                Console.WriteLine("Running Scenario 2...");
                var mappingsS2 = new List<ReaderLaneMapping>
                {
                    new ReaderLaneMapping { ReaderNo = 1, LaneId = testLaneId, Direction = "OUT", IsEnabled = true },
                    new ReaderLaneMapping { ReaderNo = 2, LaneId = testLaneId, Direction = "OUT", IsEnabled = true }
                };
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS2);
                var resultS2 = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS2.IsConsistent, "S2: Should be consistent");
                Assert(resultS2.AutoDirection == LaneDirection.Out, "S2: Auto direction should be OUT");
                Assert(!resultS2.AllowedDirections.Contains(LaneDirection.In), "S2: AllowedDirections should NOT contain IN");
                Assert(resultS2.AllowedDirections.Contains(LaneDirection.Out), "S2: AllowedDirections should contain OUT");
                Assert(resultS2.AllowedDirections.Contains(LaneDirection.Maintenance), "S2: AllowedDirections should contain MAINTENANCE");
                Assert(resultS2.Badge == LaneDirectionBadge.AutoOut, "S2: Badge should be AutoOut");

                // Scenario 3: Reader1 = IN, Reader2 = OUT
                Console.WriteLine("Running Scenario 3...");
                var mappingsS3 = new List<ReaderLaneMapping>
                {
                    new ReaderLaneMapping { ReaderNo = 1, LaneId = testLaneId, Direction = "IN", IsEnabled = true },
                    new ReaderLaneMapping { ReaderNo = 2, LaneId = testLaneId, Direction = "OUT", IsEnabled = true }
                };
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS3);
                var resultS3 = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(!resultS3.IsConsistent, "S3: Should NOT be consistent");
                Assert(resultS3.IsMixed, "S3: Should be mixed");
                Assert(resultS3.AutoDirection == null, "S3: Auto direction should be null");
                Assert(resultS3.AllowedDirections.Contains(LaneDirection.In), "S3: AllowedDirections should contain IN");
                Assert(resultS3.AllowedDirections.Contains(LaneDirection.Out), "S3: AllowedDirections should contain OUT");
                Assert(resultS3.AllowedDirections.Contains(LaneDirection.Maintenance), "S3: AllowedDirections should contain MAINTENANCE");
                Assert(resultS3.Badge == LaneDirectionBadge.Mixed, "S3: Badge should be Mixed");

                // Scenario 6: Reader1 = IN, Reader2 = IN -> Remove Reader2 -> Still AutoIn
                Console.WriteLine("Running Scenario 6...");
                var mappingsS6 = new List<ReaderLaneMapping>
                {
                    new ReaderLaneMapping { ReaderNo = 1, LaneId = testLaneId, Direction = "IN", IsEnabled = true },
                    new ReaderLaneMapping { ReaderNo = 2, LaneId = testLaneId, Direction = "IN", IsEnabled = true }
                };
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS6);
                var resultS6_Before = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS6_Before.Badge == LaneDirectionBadge.AutoIn, "S6_Before: Badge should be AutoIn");

                // Remove Reader2
                mappingsS6.RemoveAt(1);
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS6);
                var resultS6_After = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS6_After.Badge == LaneDirectionBadge.AutoIn, "S6_After: Badge should still be AutoIn");

                // Scenario 7: Reader1 = IN -> Remove Reader1 -> Badge = None, All allowed
                Console.WriteLine("Running Scenario 7...");
                var mappingsS7 = new List<ReaderLaneMapping>
                {
                    new ReaderLaneMapping { ReaderNo = 1, LaneId = testLaneId, Direction = "IN", IsEnabled = true }
                };
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS7);
                var resultS7_Before = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS7_Before.Badge == LaneDirectionBadge.AutoIn, "S7_Before: Badge should be AutoIn");

                // Remove Reader1
                mappingsS7.Clear();
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS7);
                var resultS7_After = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS7_After.Badge == LaneDirectionBadge.None, "S7_After: Badge should be None");
                Assert(resultS7_After.AllowedDirections.Contains(LaneDirection.In), "S7_After: AllowedDirections should contain IN");
                Assert(resultS7_After.AllowedDirections.Contains(LaneDirection.Out), "S7_After: AllowedDirections should contain OUT");
                Assert(resultS7_After.AllowedDirections.Contains(LaneDirection.Maintenance), "S7_After: AllowedDirections should contain MAINTENANCE");

                // Scenario 8: Reader1 = IN, Reader2 = IN -> Change Reader2 -> OUT -> Badge = Mixed, all allowed
                Console.WriteLine("Running Scenario 8...");
                var mappingsS8 = new List<ReaderLaneMapping>
                {
                    new ReaderLaneMapping { ReaderNo = 1, LaneId = testLaneId, Direction = "IN", IsEnabled = true },
                    new ReaderLaneMapping { ReaderNo = 2, LaneId = testLaneId, Direction = "IN", IsEnabled = true }
                };
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS8);
                var resultS8_Before = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS8_Before.Badge == LaneDirectionBadge.AutoIn, "S8_Before: Badge should be AutoIn");

                // Change Reader 2 to OUT
                mappingsS8[1].Direction = "OUT";
                ReaderLaneMappingService.Instance.UpdateMappings(mappingsS8);
                var resultS8_After = LaneDirectionConsistencyService.Instance.AnalyzeLaneDirection(testLaneId);
                Assert(resultS8_After.Badge == LaneDirectionBadge.Mixed, "S8_After: Badge should be Mixed");
                Assert(resultS8_After.AllowedDirections.Contains(LaneDirection.In), "S8_After: AllowedDirections should contain IN");
                Assert(resultS8_After.AllowedDirections.Contains(LaneDirection.Out), "S8_After: AllowedDirections should contain OUT");
                Assert(resultS8_After.AllowedDirections.Contains(LaneDirection.Maintenance), "S8_After: AllowedDirections should contain MAINTENANCE");

                // Scenario 9: Auto-sync simulation with Maintenance preservation
                Console.WriteLine("Running Scenario 9...");
                // 9.1: Current lane is OUT, readers are IN (Allowed: IN, MAINTENANCE). Expected: Needs sync to IN.
                var allowedDirs_9_1 = new List<LaneDirection> { LaneDirection.In, LaneDirection.Maintenance };
                var currentDir_9_1 = LaneDirection.Out;
                bool needsSync_9_1 = !allowedDirs_9_1.Contains(currentDir_9_1);
                Assert(needsSync_9_1, "S9.1: OUT lane should need sync when readers are IN");

                // 9.2: Current lane is MAINTENANCE, readers are IN. Expected: Does NOT need sync.
                var currentDir_9_2 = LaneDirection.Maintenance;
                bool needsSync_9_2 = !allowedDirs_9_1.Contains(currentDir_9_2);
                Assert(!needsSync_9_2, "S9.2: MAINTENANCE lane should NOT need sync when readers are IN");

                // 9.3: Current lane is IN, readers are IN. Expected: Does NOT need sync.
                var currentDir_9_3 = LaneDirection.In;
                bool needsSync_9_3 = !allowedDirs_9_1.Contains(currentDir_9_3);
                Assert(!needsSync_9_3, "S9.3: IN lane should NOT need sync when readers are IN");

                Console.WriteLine("ALL CONSISTENCY TESTS PASSED SUCCESSFULLY!");
                Console.WriteLine("=================================================");

                // Diagnostic database print
                try
                {
                    var db = new DatabaseService();
                    string connStr = db.GetConnectionString();
                    Console.WriteLine("DIAGNOSTIC: Connection String: " + connStr);
                    using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                    {
                        conn.Open();
                        Console.WriteLine("DIAGNOSTIC: Connected to DB!");
                        
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM dbo.DeploymentHistory";
                            Console.WriteLine("DIAGNOSTIC: DeploymentHistory count: " + cmd.ExecuteScalar());
                            
                            cmd.CommandText = "SELECT COUNT(*) FROM dbo.ConfigurationAudit";
                            Console.WriteLine("DIAGNOSTIC: ConfigurationAudit count: " + cmd.ExecuteScalar());

                            cmd.CommandText = "SELECT TOP 10 Id, Version, DeployTime, Status, Notes FROM dbo.DeploymentHistory ORDER BY DeployTime DESC";
                            using (var r = cmd.ExecuteReader())
                            {
                                Console.WriteLine("DIAGNOSTIC: Deployment History list:");
                                while (r.Read())
                                {
                                    Console.WriteLine(" - ID " + r[0] + ", V" + r[1] + ", Time " + r.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss.fff") + ", Status " + r[3] + ", Notes " + r[4]);
                                }
                            }

                            cmd.CommandText = "SELECT TOP 10 Id, Timestamp, UserName, EntityType, EntityName, PropertyName, OldValue, NewValue FROM dbo.ConfigurationAudit ORDER BY Timestamp DESC";
                            using (var r = cmd.ExecuteReader())
                            {
                                Console.WriteLine("DIAGNOSTIC: Configuration Audit list:");
                                while (r.Read())
                                {
                                    Console.WriteLine(" - ID " + r[0] + ", Time " + r.GetDateTime(1).ToString("yyyy-MM-dd HH:mm:ss.fff") + ", User " + r[2] + ", " + r[3] + " / " + r[4] + " / " + r[5] + ": '" + r[6] + "' -> '" + r[7] + "'");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("DIAGNOSTIC ERROR: " + ex.Message);
                }
            }
            finally
            {
                // Restore original mappings
                ReaderLaneMappingService.Instance.UpdateMappings(originalMappings);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }
    }
}
