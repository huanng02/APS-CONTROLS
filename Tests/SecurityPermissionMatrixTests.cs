using System;
using System.Collections.Generic;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Tests
{
    public static class SecurityPermissionMatrixTests
    {
        public static void Run()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("RUNNING SECURITY PERMISSION MATRIX TESTS");
            Console.WriteLine("=================================================");

            // 1. Test Role Hierarchy levels helper
            Console.WriteLine("Running Scenario 1: Verify Role Hierarchy levels...");
            Assert(PermissionMatrixService.GetRoleLevel("SuperAdmin") == 100, "SuperAdmin level should be 100");
            Assert(PermissionMatrixService.GetRoleLevel("Admin") == 80, "Admin level should be 80");
            Assert(PermissionMatrixService.GetRoleLevel("Manager") == 60, "Manager level should be 60");
            Assert(PermissionMatrixService.GetRoleLevel("Operator") == 40, "Operator level should be 40");
            Assert(PermissionMatrixService.GetRoleLevel("Technician") == 30, "Technician level should be 30");
            Assert(PermissionMatrixService.GetRoleLevel("Cashier") == 20, "Cashier level should be 20");
            Assert(PermissionMatrixService.GetRoleLevel("Guard") == 10, "Guard level should be 10");
            Assert(PermissionMatrixService.GetRoleLevel("Auditor") == 5, "Auditor level should be 5");
            Assert(PermissionMatrixService.GetRoleLevel("Viewer") == 1, "Viewer level should be 1");
            Assert(PermissionMatrixService.GetRoleLevel("UnknownRole") == 0, "Unknown role level should be 0");

            // 2. Test Self-Role Protection (Manager modifying Manager)
            Console.WriteLine("Running Scenario 2: Self-role editing protection...");
            var selfChange = new List<PermissionChange>
            {
                new PermissionChange
                {
                    RoleId = 3,
                    RoleName = "Manager",
                    PermissionId = 5,
                    PermissionCode = "CONFIG_EDIT",
                    OriginalValue = false,
                    NewValue = true
                }
            };
            AssertThrows<UnauthorizedAccessException>(() =>
            {
                PermissionMatrixService.ValidateChanges(selfChange, "Manager");
            }, "Should throw UnauthorizedAccessException when user tries to edit their own role.");

            // 3. Test Hierarchy Protection (Manager modifying Admin)
            Console.WriteLine("Running Scenario 3: Privilege escalation protection (lower modifying higher)...");
            var managerModifyingAdmin = new List<PermissionChange>
            {
                new PermissionChange
                {
                    RoleId = 2,
                    RoleName = "Admin",
                    PermissionId = 5,
                    PermissionCode = "CONFIG_EDIT",
                    OriginalValue = false,
                    NewValue = true
                }
            };
            AssertThrows<UnauthorizedAccessException>(() =>
            {
                PermissionMatrixService.ValidateChanges(managerModifyingAdmin, "Manager");
            }, "Should throw UnauthorizedAccessException when Manager tries to edit Admin.");

            // 4. Test Hierarchy Protection (Manager modifying Manager)
            Console.WriteLine("Running Scenario 4: Privilege escalation protection (equal role level)...");
            var managerModifyingManager2 = new List<PermissionChange>
            {
                new PermissionChange
                {
                    RoleId = 4, // different role ID, same role level name
                    RoleName = "Manager",
                    PermissionId = 5,
                    PermissionCode = "CONFIG_EDIT",
                    OriginalValue = false,
                    NewValue = true
                }
            };
            AssertThrows<UnauthorizedAccessException>(() =>
            {
                PermissionMatrixService.ValidateChanges(managerModifyingManager2, "Manager");
            }, "Should throw UnauthorizedAccessException when Manager tries to edit another Manager role.");

            // 5. Test SuperAdmin Protection (Admin modifying SuperAdmin)
            Console.WriteLine("Running Scenario 5: SuperAdmin protection (non-SuperAdmin modifying SuperAdmin)...");
            var adminModifyingSuperAdmin = new List<PermissionChange>
            {
                new PermissionChange
                {
                    RoleId = 1,
                    RoleName = "SuperAdmin",
                    PermissionId = 6,
                    PermissionCode = "ROLE_MANAGEMENT",
                    OriginalValue = true,
                    NewValue = false
                }
            };
            AssertThrows<UnauthorizedAccessException>(() =>
            {
                PermissionMatrixService.ValidateChanges(adminModifyingSuperAdmin, "Admin");
            }, "Should throw UnauthorizedAccessException when Admin tries to modify SuperAdmin.");

            // 6. Test Valid Changes (Admin modifying Manager)
            Console.WriteLine("Running Scenario 6: Valid hierarchy changes...");
            var adminModifyingManager = new List<PermissionChange>
            {
                new PermissionChange
                {
                    RoleId = 3,
                    RoleName = "Manager",
                    PermissionId = 5,
                    PermissionCode = "CONFIG_EDIT",
                    OriginalValue = false,
                    NewValue = true
                }
            };
            // This should not throw any exception
            PermissionMatrixService.ValidateChanges(adminModifyingManager, "Admin");

            // 7. Verify SavePermissionChangesAsync invokes ValidateChanges
            Console.WriteLine("Running Scenario 7: Service integration check (SavePermissionChangesAsync throws on invalid changes)...");
            
            // Backup current user context
            int originalId = CurrentUserContext.Instance.Id;
            string originalUsername = CurrentUserContext.Instance.Username;
            string originalRole = CurrentUserContext.Instance.Role;
            string originalTen = CurrentUserContext.Instance.Ten;
            var originalPermissions = CurrentUserContext.Instance.Permissions;
            var originalLanes = CurrentUserContext.Instance.AssignedLaneIds;
            var originalSites = CurrentUserContext.Instance.AssignedSiteIds;

            try
            {
                // Set current user role to Manager
                CurrentUserContext.Instance.SetCurrentUser(10, "test_manager", "Manager", "Test Manager", originalPermissions, originalLanes, originalSites);

                AssertThrows<UnauthorizedAccessException>(() =>
                {
                    // Manager trying to save changes to Admin role
                    PermissionMatrixService.Instance.SavePermissionChangesAsync(managerModifyingAdmin).GetAwaiter().GetResult();
                }, "SavePermissionChangesAsync should throw UnauthorizedAccessException when called by a Manager to edit Admin role.");
            }
            finally
            {
                // Restore original user context
                CurrentUserContext.Instance.SetCurrentUser(originalId, originalUsername, originalRole, originalTen, originalPermissions, originalLanes, originalSites);
            }

            // 8. Verify role hierarchy level comparison for User List and Roles assignment filtering
            Console.WriteLine("Running Scenario 8: Verify User List and Role Assignment filtering comparison logic...");
            Assert(IsRoleVisibleAndEditable("Admin", "SuperAdmin"), "SuperAdmin should see and be able to assign/manage Admin");
            Assert(IsRoleVisibleAndEditable("Manager", "SuperAdmin"), "SuperAdmin should see and be able to assign/manage Manager");
            Assert(IsRoleVisibleAndEditable("Manager", "Admin"), "Admin should see and be able to assign/manage Manager");
            Assert(IsRoleVisibleAndEditable("Operator", "Manager"), "Manager should see and be able to assign/manage Operator");
            Assert(!IsRoleVisibleAndEditable("SuperAdmin", "SuperAdmin"), "SuperAdmin should NOT see/assign SuperAdmin (self-editing blocked)");
            Assert(!IsRoleVisibleAndEditable("SuperAdmin", "Admin"), "Admin should NOT see/assign SuperAdmin");
            Assert(!IsRoleVisibleAndEditable("Admin", "Admin"), "Admin should NOT see/assign Admin");
            Assert(!IsRoleVisibleAndEditable("Admin", "Manager"), "Manager should NOT see/assign Admin");
            Assert(!IsRoleVisibleAndEditable("Manager", "Manager"), "Manager should NOT see/assign Manager");

            Console.WriteLine("ALL SECURITY PERMISSION MATRIX TESTS PASSED SUCCESSFULLY!");
            Console.WriteLine("=================================================");
        }

        private static bool IsRoleVisibleAndEditable(string targetRole, string currentRole)
        {
            int currentLevel = PermissionMatrixService.GetRoleLevel(currentRole);
            int targetLevel = PermissionMatrixService.GetRoleLevel(targetRole);
            return targetLevel < currentLevel;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }

        private static void AssertThrows<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return; // Expected exception thrown
            }
            catch (Exception ex)
            {
                throw new Exception($"Assertion Failed: Expected exception of type {typeof(TException).Name}, but caught {ex.GetType().Name}. Message: {message}");
            }

            throw new Exception($"Assertion Failed: Expected exception of type {typeof(TException).Name} was not thrown. Message: {message}");
        }
    }
}
