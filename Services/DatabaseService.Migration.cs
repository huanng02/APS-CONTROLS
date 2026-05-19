using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Services
{
    public partial class DatabaseService
    {
        private static bool _migrationsApplied = false;

        public static async Task EnsureMigrationsAppliedAsync()
        {
            if (_migrationsApplied) return;

            try
            {
                // Direct connectivity check — don't rely on ConnectivityStateService 
                // because it may not be started yet at App startup time.
                string connStr = ConnectionManager.Instance.CurrentConnectionString;
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "No connection string configured, skipping migrations.");
                    return;
                }

                // Quick probe: can we actually reach SQL Server?
                try
                {
                    using var probe = new SqlConnection(connStr + (connStr.Contains("Timeout") ? "" : ";Connect Timeout=5;"));
                    await probe.OpenAsync();
                    // Connection OK, proceed with migrations
                }
                catch (Exception probeEx)
                {
                    LoggingService.Instance.LogInfo("MIGRATION", "Ensure", 
                        $"SQL Server unreachable at startup, skipping migrations. ({probeEx.Message})");
                    return;
                }
                using (var conn = new SqlConnection(connStr))
                {
                    // Use a short timeout to prevent blocking startup
                    if (!connStr.Contains("Connect Timeout") && !connStr.Contains("Connection Timeout"))
                    {
                        conn.ConnectionString += ";Connect Timeout=5;";
                    }

                    await conn.OpenAsync();

                    LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "Starting SQL Server topology migrations...");

                    // Read migration SQL script
                    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260519_multi_zone_topology.sql");
                    string sql = string.Empty;

                    if (File.Exists(scriptPath))
                    {
                        sql = await File.ReadAllTextAsync(scriptPath);
                    }
                    else
                    {
                        // Fallback embedded script
                        sql = GetEmbeddedMigrationSql();
                    }

                    // SQL Server SqlCommand cannot run 'GO' or transaction scripts with GO, but our script
                    // uses standard SQL with BEGIN TRANSACTION/COMMIT TRANSACTION without GO.
                    // Execute the migration SQL script
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = 60; // Allow enough time for migration
                        await cmd.ExecuteNonQueryAsync();
                    }

                    _migrationsApplied = true;
                    LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "SQL Server topology migrations applied successfully.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("MIGRATION_ERROR", "Ensure", "Failed to apply SQL Server migrations", ex);
            }
        }

        private static string GetEmbeddedMigrationSql()
        {
            return @"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            -- 1) ParkingSites
            IF OBJECT_ID(N'dbo.ParkingSites', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ParkingSites (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SiteCode NVARCHAR(50) NOT NULL UNIQUE,
                    SiteName NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL DEFAULT(1),
                    CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
                );
            END

            -- 2) ParkingZones
            IF OBJECT_ID(N'dbo.ParkingZones', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ParkingZones (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SiteId INT NOT NULL,
                    ZoneCode NVARCHAR(50) NOT NULL UNIQUE,
                    ZoneName NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    MaxCapacity INT NOT NULL DEFAULT(100),
                    IsActive BIT NOT NULL DEFAULT(1),
                    CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
                    CONSTRAINT FK_ParkingZones_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id)
                );
            END

            -- 3) C3Controllers
            IF OBJECT_ID(N'dbo.C3Controllers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.C3Controllers (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ControllerName NVARCHAR(100) NOT NULL,
                    IpAddress NVARCHAR(50) NOT NULL UNIQUE,
                    ZoneId INT NOT NULL,
                    IsActive BIT NOT NULL DEFAULT(1),
                    CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
                    CONSTRAINT FK_C3Controllers_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id)
                );
            END

            -- 4) Lanes
            IF OBJECT_ID(N'dbo.Lanes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Lanes (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    LaneCode NVARCHAR(50) NOT NULL UNIQUE,
                    LaneName NVARCHAR(100) NOT NULL,
                    Direction NVARCHAR(20) NOT NULL,
                    IsActive BIT NOT NULL DEFAULT(1),
                    CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
                );
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Lanes' AND COLUMN_NAME = 'ZoneId')
            BEGIN
                ALTER TABLE dbo.Lanes ADD ZoneId INT NULL;
                ALTER TABLE dbo.Lanes ADD CONSTRAINT FK_Lanes_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id);
            END

            -- 5) VehicleSessions
            IF OBJECT_ID(N'dbo.VehicleSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.VehicleSessions (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CardId INT NULL,
                    BienSo NVARCHAR(50) NULL,
                    ThoiGianVao DATETIME NOT NULL,
                    ThoiGianRa DATETIME NULL,
                    Tien DECIMAL(18,2) NULL,
                    TrangThai NVARCHAR(50) NULL,
                    AnhVao NVARCHAR(500) NULL,
                    AnhRa NVARCHAR(500) NULL,
                    CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
                );
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'SiteId')
            BEGIN
                ALTER TABLE dbo.VehicleSessions ADD SiteId INT NULL;
                ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'ZoneId')
            BEGIN
                ALTER TABLE dbo.VehicleSessions ADD ZoneId INT NULL;
                ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'EntryLaneId')
            BEGIN
                ALTER TABLE dbo.VehicleSessions ADD EntryLaneId INT NULL;
                ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_EntryLane FOREIGN KEY (EntryLaneId) REFERENCES dbo.Lanes(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'ExitLaneId')
            BEGIN
                ALTER TABLE dbo.VehicleSessions ADD ExitLaneId INT NULL;
                ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_ExitLane FOREIGN KEY (ExitLaneId) REFERENCES dbo.Lanes(Id);
            END

            -- 6) Indexes
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleSessions_SiteId' AND object_id = OBJECT_ID('dbo.VehicleSessions'))
            BEGIN
                CREATE INDEX IX_VehicleSessions_SiteId ON dbo.VehicleSessions(SiteId);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleSessions_ZoneId' AND object_id = OBJECT_ID('dbo.VehicleSessions'))
            BEGIN
                CREATE INDEX IX_VehicleSessions_ZoneId ON dbo.VehicleSessions(ZoneId);
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Lanes_ZoneId' AND object_id = OBJECT_ID('dbo.Lanes'))
            BEGIN
                CREATE INDEX IX_Lanes_ZoneId ON dbo.Lanes(ZoneId);
            END

            -- 7) Seeding
            IF NOT EXISTS (SELECT 1 FROM dbo.ParkingSites WHERE SiteCode = 'DEFAULT-SITE')
            BEGIN
                INSERT INTO dbo.ParkingSites (SiteCode, SiteName, Description, IsActive, CreatedUtc)
                VALUES ('DEFAULT-SITE', N'Default Parking Site', N'Auto-seeded default site', 1, GETUTCDATE());
            END

            DECLARE @DefaultSiteId INT = (SELECT Id FROM dbo.ParkingSites WHERE SiteCode = 'DEFAULT-SITE');

            IF NOT EXISTS (SELECT 1 FROM dbo.ParkingZones WHERE ZoneCode = 'DEFAULT-ZONE')
            BEGIN
                INSERT INTO dbo.ParkingZones (SiteId, ZoneCode, ZoneName, Description, MaxCapacity, IsActive, CreatedUtc)
                VALUES (@DefaultSiteId, 'DEFAULT-ZONE', N'Default Parking Zone', N'Auto-seeded default zone', 500, 1, GETUTCDATE());
            END

            -- 8) Map existing lanes
            DECLARE @DefaultZoneId INT = (SELECT Id FROM dbo.ParkingZones WHERE ZoneCode = 'DEFAULT-ZONE');

            IF NOT EXISTS (SELECT 1 FROM dbo.Lanes)
            BEGIN
                INSERT INTO dbo.Lanes (LaneCode, LaneName, Direction, ZoneId, IsActive, CreatedUtc)
                VALUES 
                ('LANE-1', N'Cổng Vào 1', 'IN', @DefaultZoneId, 1, GETUTCDATE()),
                ('LANE-2', N'Cổng Ra 1', 'OUT', @DefaultZoneId, 1, GETUTCDATE());
            END
            ELSE
            BEGIN
                UPDATE dbo.Lanes SET ZoneId = @DefaultZoneId WHERE ZoneId IS NULL;
            END

            COMMIT TRANSACTION;
            ";
        }
    }
}
