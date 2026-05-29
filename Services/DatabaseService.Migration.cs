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

                    // Execute Gates migration (20260528_add_parking_gates_table.sql)
                    string scriptPathGates = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260528_add_parking_gates_table.sql");
                    if (File.Exists(scriptPathGates))
                    {
                        LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "Executing Cổng kiểm soát (ParkingGates) migration...");
                        string sqlGates = await File.ReadAllTextAsync(scriptPathGates);
                        using (var cmd = new SqlCommand(sqlGates, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                        LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "Cổng kiểm soát (ParkingGates) migration applied successfully.");
                    }

                    // Execute RBAC migration (20260529_rbac_enterprise.sql)
                    string scriptPathRbac = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260529_rbac_enterprise.sql");
                    if (File.Exists(scriptPathRbac))
                    {
                        LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "Executing Enterprise RBAC migration...");
                        string sqlRbac = await File.ReadAllTextAsync(scriptPathRbac);
                        using (var cmd = new SqlCommand(sqlRbac, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                        LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "Enterprise RBAC migration applied successfully.");
                    }

                    // Execute RBAC Function Separation migration (20260529_rbac_function_separation.sql)
                    string scriptPathFuncSep = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260529_rbac_function_separation.sql");
                    if (File.Exists(scriptPathFuncSep))
                    {
                        LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "Executing Enterprise Function Separation migration...");
                        string sqlFuncSep = await File.ReadAllTextAsync(scriptPathFuncSep);
                        using (var cmd = new SqlCommand(sqlFuncSep, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                        LoggingService.Instance.LogInfo("MIGRATION", "Ensure", "Enterprise Function Separation migration applied successfully.");
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
                    ServerIp NVARCHAR(50) NOT NULL DEFAULT '127.0.0.1',
                    PcIp NVARCHAR(50) NOT NULL DEFAULT '127.0.0.1',
                    ZoneId INT NOT NULL,
                    IsActive BIT NOT NULL DEFAULT(1),
                    CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
                    CONSTRAINT FK_C3Controllers_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id)
                );
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'C3Controllers' AND COLUMN_NAME = 'ServerIp')
            BEGIN
                ALTER TABLE dbo.C3Controllers ADD ServerIp NVARCHAR(50) NOT NULL DEFAULT '127.0.0.1';
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'C3Controllers' AND COLUMN_NAME = 'PcIp')
            BEGIN
                ALTER TABLE dbo.C3Controllers ADD PcIp NVARCHAR(50) NOT NULL DEFAULT '127.0.0.1';
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

            -- 7) Seeding (Only runs if the database is completely empty)
            IF NOT EXISTS (SELECT 1 FROM dbo.ParkingSites)
            BEGIN
                INSERT INTO dbo.ParkingSites (SiteCode, SiteName, Description, IsActive, CreatedUtc)
                VALUES ('DEFAULT-SITE', N'Default Parking Site', N'Auto-seeded default site', 1, GETUTCDATE());

                DECLARE @DefaultSiteId INT = (SELECT Id FROM dbo.ParkingSites WHERE SiteCode = 'DEFAULT-SITE');

                INSERT INTO dbo.ParkingZones (SiteId, ZoneCode, ZoneName, Description, MaxCapacity, IsActive, CreatedUtc)
                VALUES (@DefaultSiteId, 'DEFAULT-ZONE', N'Default Parking Zone', N'Auto-seeded default zone', 500, 1, GETUTCDATE());

                DECLARE @DefaultZoneId INT = (SELECT Id FROM dbo.ParkingZones WHERE ZoneCode = 'DEFAULT-ZONE');

                INSERT INTO dbo.Lanes (LaneCode, LaneName, Direction, ZoneId, IsActive, CreatedUtc)
                VALUES 
                ('LANE-1', N'Cổng Vào 1', 'IN', @DefaultZoneId, 1, GETUTCDATE()),
                ('LANE-2', N'Cổng Ra 1', 'OUT', @DefaultZoneId, 1, GETUTCDATE());
            END

            -- 8) Add multi-zone columns to XeTrongBai if they do not exist
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'SiteId')
            BEGIN
                ALTER TABLE dbo.XeTrongBai ADD SiteId INT NULL;
                ALTER TABLE dbo.XeTrongBai ADD CONSTRAINT FK_XeTrongBai_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'ZoneId')
            BEGIN
                ALTER TABLE dbo.XeTrongBai ADD ZoneId INT NULL;
                ALTER TABLE dbo.XeTrongBai ADD CONSTRAINT FK_XeTrongBai_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'EntryLaneId')
            BEGIN
                ALTER TABLE dbo.XeTrongBai ADD EntryLaneId INT NULL;
                ALTER TABLE dbo.XeTrongBai ADD CONSTRAINT FK_XeTrongBai_EntryLane FOREIGN KEY (EntryLaneId) REFERENCES dbo.Lanes(Id);
            END

            -- 9) Add multi-zone columns to LichSuXe if they do not exist
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'SiteId')
            BEGIN
                ALTER TABLE dbo.LichSuXe ADD SiteId INT NULL;
                ALTER TABLE dbo.LichSuXe ADD CONSTRAINT FK_LichSuXe_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'ZoneId')
            BEGIN
                ALTER TABLE dbo.LichSuXe ADD ZoneId INT NULL;
                ALTER TABLE dbo.LichSuXe ADD CONSTRAINT FK_LichSuXe_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'EntryLaneId')
            BEGIN
                ALTER TABLE dbo.LichSuXe ADD EntryLaneId INT NULL;
                ALTER TABLE dbo.LichSuXe ADD CONSTRAINT FK_LichSuXe_EntryLane FOREIGN KEY (EntryLaneId) REFERENCES dbo.Lanes(Id);
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'ExitLaneId')
            BEGIN
                ALTER TABLE dbo.LichSuXe ADD ExitLaneId INT NULL;
                ALTER TABLE dbo.LichSuXe ADD CONSTRAINT FK_LichSuXe_ExitLane FOREIGN KEY (ExitLaneId) REFERENCES dbo.Lanes(Id);
            END

            COMMIT TRANSACTION;
            ";
        }

        private static bool _baseSchemaChecked = false;

        public static async Task EnsureBaseSchemaAndAdminSeededAsync(string connStr)
        {
            if (_baseSchemaChecked) return;

            try
            {
                if (string.IsNullOrWhiteSpace(connStr)) return;

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // 1. Create base tables and seed Admin
                    string baseSql = @"
                    SET XACT_ABORT ON;
                    BEGIN TRANSACTION;

                    -- 1) Create Roles table
                    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.Roles (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            Name NVARCHAR(50) NOT NULL UNIQUE,
                            TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active'
                        );
                    END

                    -- 2) Create NhanVien table
                    IF OBJECT_ID(N'dbo.NhanVien', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.NhanVien (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            Ten NVARCHAR(100) NOT NULL,
                            Username NVARCHAR(50) NOT NULL UNIQUE,
                            [Password] NVARCHAR(255) NOT NULL,
                            TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active',
                            RoleId INT NOT NULL,
                            CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            LastLogin DATETIME NULL,
                            CONSTRAINT FK_NhanVien_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id)
                        );
                    END

                    -- 3) Create LoaiXe table
                    IF OBJECT_ID(N'dbo.LoaiXe', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.LoaiXe (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            TenLoai NVARCHAR(100) NOT NULL,
                            Ten NVARCHAR(100) NULL,
                            TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active'
                        );
                    END

                    -- 4) Create LoaiVe table
                    IF OBJECT_ID(N'dbo.LoaiVe', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.LoaiVe (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            TenLoai NVARCHAR(100) NOT NULL,
                            Ten NVARCHAR(100) NULL,
                            TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active',
                            Detail NVARCHAR(500) NULL,
                            CoTheGiaHan BIT NOT NULL DEFAULT 0
                        );
                    END

                    -- 5) Create BangGia table
                    IF OBJECT_ID(N'dbo.BangGia', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.BangGia (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            LoaiXeId INT NOT NULL,
                            LoaiVeId INT NOT NULL,
                            GiaBanNgay DECIMAL(18,2) NULL,
                            GiaQuaDem DECIMAL(18,2) NULL,
                            GiaThang DECIMAL(18,2) NULL,
                            TrangThai NVARCHAR(20) NOT NULL DEFAULT '1',
                            CONSTRAINT FK_BangGia_LoaiXe FOREIGN KEY (LoaiXeId) REFERENCES dbo.LoaiXe(Id),
                            CONSTRAINT FK_BangGia_LoaiVe FOREIGN KEY (LoaiVeId) REFERENCES dbo.LoaiVe(Id)
                        );
                    END

                    -- 6) Create RFIDCards table
                    IF OBJECT_ID(N'dbo.RFIDCards', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.RFIDCards (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            CardUID NVARCHAR(50) NOT NULL UNIQUE,
                            BienSo NVARCHAR(50) NULL,
                            LoaiVeId INT NOT NULL,
                            LoaiXeId INT NOT NULL,
                            TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active',
                            NgayDangKy DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            NgayHetHan DATETIME NULL,
                            CONSTRAINT FK_RFIDCards_LoaiVe FOREIGN KEY (LoaiVeId) REFERENCES dbo.LoaiVe(Id),
                            CONSTRAINT FK_RFIDCards_LoaiXe FOREIGN KEY (LoaiXeId) REFERENCES dbo.LoaiXe(Id)
                        );
                    END

                    -- 7) Create XeTrongBai table
                    IF OBJECT_ID(N'dbo.XeTrongBai', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.XeTrongBai (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            CardId INT NOT NULL,
                            BienSo NVARCHAR(50) NULL,
                            ThoiGianVao DATETIME NOT NULL DEFAULT GETUTCDATE(),
                            ThoiGianRa DATETIME NULL,
                            AnhXe NVARCHAR(500) NULL,
                            CONSTRAINT FK_XeTrongBai_RFIDCards FOREIGN KEY (CardId) REFERENCES dbo.RFIDCards(Id)
                        );
                    END

                    -- 8) Create LichSuXe table
                    IF OBJECT_ID(N'dbo.LichSuXe', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.LichSuXe (
                            Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            CardId INT NULL,
                            BienSo NVARCHAR(50) NULL,
                            ThoiGianVao DATETIME NOT NULL,
                            ThoiGianRa DATETIME NULL,
                            Tien DECIMAL(18,2) NULL,
                            AnhRa NVARCHAR(500) NULL,
                            CONSTRAINT FK_LichSuXe_RFIDCards FOREIGN KEY (CardId) REFERENCES dbo.RFIDCards(Id)
                        );
                    END

                    -- 9) Seed default Roles
                    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Admin')
                    BEGIN
                        INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Admin', 'Active');
                    END
                    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Operator')
                    BEGIN
                        INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Operator', 'Active');
                    END

                    -- 10) Seed default Admin user
                    IF NOT EXISTS (SELECT 1 FROM dbo.NhanVien)
                    BEGIN
                        DECLARE @AdminRoleId INT = (SELECT TOP 1 Id FROM dbo.Roles WHERE Name = 'Admin');
                        IF @AdminRoleId IS NOT NULL
                        BEGIN
                            INSERT INTO dbo.NhanVien (Ten, Username, [Password], TrangThai, RoleId, CreatedAt)
                            VALUES (N'Administrator', 'admin', 'admin', 'Active', @AdminRoleId, GETUTCDATE());
                        END
                    END

                    COMMIT TRANSACTION;
                    ";

                    using (var cmd = new SqlCommand(baseSql, conn))
                    {
                        cmd.CommandTimeout = 60;
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Now run the multi-zone topology migrations safely
                    string multiZoneSql = GetEmbeddedMigrationSql();
                    using (var cmd = new SqlCommand(multiZoneSql, conn))
                    {
                        cmd.CommandTimeout = 60;
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 3. Now run the pricing migration script (20260424_add_khunggio_pricing.sql)
                    string pricingSql = GetEmbeddedPricingMigrationSql();
                    if (!string.IsNullOrWhiteSpace(pricingSql))
                    {
                        using (var cmd = new SqlCommand(pricingSql, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 4. Now run the gates migration script (20260528_add_parking_gates_table.sql)
                    string scriptPathGates = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260528_add_parking_gates_table.sql");
                    string sqlGates = string.Empty;
                    if (File.Exists(scriptPathGates))
                    {
                        sqlGates = await File.ReadAllTextAsync(scriptPathGates);
                    }
                    else
                    {
                        sqlGates = GetEmbeddedGatesMigrationSql();
                    }

                    if (!string.IsNullOrWhiteSpace(sqlGates))
                    {
                        using (var cmd = new SqlCommand(sqlGates, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 5. Now run the lane vehicle type migration script (20260529_add_lane_vehicle_type.sql)
                    string scriptPathLaneVehicle = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260529_add_lane_vehicle_type.sql");
                    string sqlLaneVehicle = string.Empty;
                    if (File.Exists(scriptPathLaneVehicle))
                    {
                        sqlLaneVehicle = await File.ReadAllTextAsync(scriptPathLaneVehicle);
                    }
                    else
                    {
                        sqlLaneVehicle = GetEmbeddedLaneVehicleTypeMigrationSql();
                    }

                    if (!string.IsNullOrWhiteSpace(sqlLaneVehicle))
                    {
                        using (var cmd = new SqlCommand(sqlLaneVehicle, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // 6. Now run the Enterprise RBAC migration script (20260529_rbac_enterprise.sql)
                    string scriptPathRbac = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260529_rbac_enterprise.sql");
                    if (File.Exists(scriptPathRbac))
                    {
                        LoggingService.Instance.LogInfo("DB_INIT", "EnsureBaseSchemaAndAdminSeededAsync", "Executing Enterprise RBAC migration...");
                        string sqlRbac = await File.ReadAllTextAsync(scriptPathRbac);
                        using (var cmd = new SqlCommand(sqlRbac, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                        LoggingService.Instance.LogInfo("DB_INIT", "EnsureBaseSchemaAndAdminSeededAsync", "Enterprise RBAC migration applied successfully.");
                    }

                    // 7. Now run the Enterprise RBAC Function Separation migration script (20260529_rbac_function_separation.sql)
                    string scriptPathFuncSep = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations", "20260529_rbac_function_separation.sql");
                    if (File.Exists(scriptPathFuncSep))
                    {
                        LoggingService.Instance.LogInfo("DB_INIT", "EnsureBaseSchemaAndAdminSeededAsync", "Executing Enterprise Function Separation migration...");
                        string sqlFuncSep = await File.ReadAllTextAsync(scriptPathFuncSep);
                        using (var cmd = new SqlCommand(sqlFuncSep, conn))
                        {
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                        LoggingService.Instance.LogInfo("DB_INIT", "EnsureBaseSchemaAndAdminSeededAsync", "Enterprise Function Separation migration applied successfully.");
                    }

                    _baseSchemaChecked = true;
                    _migrationsApplied = true; // so that standard migrations are skipped post-login
                    LoggingService.Instance.LogInfo("DB_INIT", "EnsureBaseSchemaAndAdminSeededAsync", "Database successfully initialized and seeded.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DB_INIT_ERROR", "EnsureBaseSchemaAndAdminSeededAsync", "Failed to initialize and seed database", ex);
                throw;
            }
        }

        private static string GetEmbeddedPricingMigrationSql()
        {
            return @"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            -- Ensure GiaBanNgay and GiaQuaDem columns exist on BangGia if table already existed without them
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BangGia' AND COLUMN_NAME = 'GiaBanNgay')
            BEGIN
                ALTER TABLE dbo.BangGia ADD GiaBanNgay DECIMAL(18,2) NULL;
            END

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BangGia' AND COLUMN_NAME = 'GiaQuaDem')
            BEGIN
                ALTER TABLE dbo.BangGia ADD GiaQuaDem DECIMAL(18,2) NULL;
            END

            -- 1) Create KhungGio table
            IF OBJECT_ID(N'dbo.KhungGio', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.KhungGio (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenKhungGio NVARCHAR(100) NOT NULL,
                    GioBatDau TIME NOT NULL,
                    GioKetThuc TIME NOT NULL,
                    QuaDem BIT NOT NULL CONSTRAINT DF_KhungGio_QuaDem DEFAULT(0),
                    TrangThai BIT NOT NULL CONSTRAINT DF_KhungGio_TrangThai DEFAULT(1)
                );
            END

            -- 2) Create BangGiaKhungGio table
            IF OBJECT_ID(N'dbo.BangGiaKhungGio', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.BangGiaKhungGio (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    BangGiaId INT NOT NULL,
                    KhungGioId INT NOT NULL,
                    GiaTien DECIMAL(18,2) NOT NULL,
                    CONSTRAINT FK_BangGiaKhungGio_BangGia FOREIGN KEY (BangGiaId) REFERENCES dbo.BangGia(Id),
                    CONSTRAINT FK_BangGiaKhungGio_KhungGio FOREIGN KEY (KhungGioId) REFERENCES dbo.KhungGio(Id)
                );
            END

            -- 3) Seed KhungGio (day / night). Use localized names.
            IF NOT EXISTS (SELECT 1 FROM dbo.KhungGio WHERE TenKhungGio = N'Ban ngày')
            BEGIN
                INSERT INTO dbo.KhungGio (TenKhungGio, GioBatDau, GioKetThuc, QuaDem, TrangThai)
                VALUES (N'Ban ngày', '06:00:00', '18:00:00', 0, 1);
            END

            IF NOT EXISTS (SELECT 1 FROM dbo.KhungGio WHERE TenKhungGio = N'Ban đêm')
            BEGIN
                INSERT INTO dbo.KhungGio (TenKhungGio, GioBatDau, GioKetThuc, QuaDem, TrangThai)
                VALUES (N'Ban đêm', '18:00:00', '06:00:00', 1, 1);
            END

            -- 4) Ensure LoaiXe and LoaiVe seed values exist
            IF OBJECT_ID(N'dbo.LoaiXe', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.LoaiXe WHERE TenLoai = N'Xe máy')
                    INSERT INTO dbo.LoaiXe (TenLoai, TrangThai) VALUES (N'Xe máy', 'Active');
                IF NOT EXISTS (SELECT 1 FROM dbo.LoaiXe WHERE TenLoai = N'Ô tô')
                    INSERT INTO dbo.LoaiXe (TenLoai, TrangThai) VALUES (N'Ô tô', 'Active');
            END

            IF OBJECT_ID(N'dbo.LoaiVe', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.LoaiVe WHERE TenLoai = N'Vãng lai')
                    INSERT INTO dbo.LoaiVe (TenLoai, TrangThai, Detail) VALUES (N'Vãng lai', 'Active', N'Vé vãng lai');
                IF NOT EXISTS (SELECT 1 FROM dbo.LoaiVe WHERE TenLoai = N'Tháng')
                    INSERT INTO dbo.LoaiVe (TenLoai, TrangThai, Detail) VALUES (N'Tháng', 'Active', N'Vé tháng');
            END

            -- 5) Migrate existing BangGia values into BangGiaKhungGio (using dynamic SQL for new columns)
            DECLARE @DayKhungId INT = (SELECT TOP(1) Id FROM dbo.KhungGio WHERE TenKhungGio = N'Ban ngày');
            DECLARE @NightKhungId INT = (SELECT TOP(1) Id FROM dbo.KhungGio WHERE TenKhungGio = N'Ban đêm');

            IF @DayKhungId IS NOT NULL AND @NightKhungId IS NOT NULL
            BEGIN
                -- Insert day prices
                EXEC sp_executesql N'
                INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
                SELECT b.Id, @DayKhungId, b.GiaBanNgay
                FROM dbo.BangGia b
                WHERE b.GiaBanNgay IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @DayKhungId);', N'@DayKhungId INT', @DayKhungId = @DayKhungId;

                -- Insert night prices
                EXEC sp_executesql N'
                INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
                SELECT b.Id, @NightKhungId, b.GiaQuaDem
                FROM dbo.BangGia b
                WHERE b.GiaQuaDem IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @NightKhungId);', N'@NightKhungId INT', @NightKhungId = @NightKhungId;
            END

            -- 6) Insert sample BangGia entries (if not present) and assign KhungGio prices (using dynamic SQL)
            DECLARE @XeMayId INT = NULL, @OToId INT = NULL, @VangLaiId INT = NULL, @ThangId INT = NULL;
            IF OBJECT_ID(N'dbo.LoaiXe', N'U') IS NOT NULL
            BEGIN
                SELECT @XeMayId = Id FROM dbo.LoaiXe WHERE TenLoai = N'Xe máy';
                SELECT @OToId = Id FROM dbo.LoaiXe WHERE TenLoai = N'Ô tô';
            END
            IF OBJECT_ID(N'dbo.LoaiVe', N'U') IS NOT NULL
            BEGIN
                SELECT @VangLaiId = Id FROM dbo.LoaiVe WHERE TenLoai = N'Vãng lai';
                SELECT @ThangId = Id FROM dbo.LoaiVe WHERE TenLoai = N'Tháng';
            END

            -- Create BangGia
            IF @XeMayId IS NOT NULL AND @VangLaiId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @XeMayId AND LoaiVeId = @VangLaiId)
                BEGIN
                    INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai, GiaBanNgay, GiaQuaDem)
                    VALUES (@XeMayId, @VangLaiId, NULL, N''1'', 5000.00, 3000.00);
                END', N'@XeMayId INT, @VangLaiId INT', @XeMayId = @XeMayId, @VangLaiId = @VangLaiId;
            END

            IF @XeMayId IS NOT NULL AND @ThangId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @XeMayId AND LoaiVeId = @ThangId)
                BEGIN
                    INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai)
                    VALUES (@XeMayId, @ThangId, 200000.00, N''1'');
                END', N'@XeMayId INT, @ThangId INT', @XeMayId = @XeMayId, @ThangId = @ThangId;
            END

            IF @OToId IS NOT NULL AND @VangLaiId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @OToId AND LoaiVeId = @VangLaiId)
                BEGIN
                    INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai, GiaBanNgay, GiaQuaDem)
                    VALUES (@OToId, @VangLaiId, NULL, N''1'', 15000.00, 10000.00);
                END', N'@OToId INT, @VangLaiId INT', @OToId = @OToId, @VangLaiId = @VangLaiId;
            END

            IF @OToId IS NOT NULL AND @ThangId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @OToId AND LoaiVeId = @ThangId)
                BEGIN
                    INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai)
                    VALUES (@OToId, @ThangId, 1000000.00, N''1'');
                END', N'@OToId INT, @ThangId INT', @OToId = @OToId, @ThangId = @ThangId;
            END

            -- 7) For any newly created BangGia rows above, ensure BangGiaKhungGio pricing exist
            IF @DayKhungId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
                SELECT b.Id, @DayKhungId,
                       CASE WHEN b.LoaiVeId = @ThangId THEN 0.00 ELSE
                            CASE WHEN b.LoaiXeId = @XeMayId THEN 5000.00 WHEN b.LoaiXeId = @OToId THEN 15000.00 ELSE 0.00 END END
                FROM dbo.BangGia b
                WHERE NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @DayKhungId)
                  AND (b.GiaBanNgay IS NULL OR b.GiaBanNgay = 0)
                  AND (b.LoaiVeId = @VangLaiId OR b.LoaiVeId = @ThangId);', 
                  N'@DayKhungId INT, @ThangId INT, @XeMayId INT, @OToId INT, @VangLaiId INT', 
                  @DayKhungId = @DayKhungId, @ThangId = @ThangId, @XeMayId = @XeMayId, @OToId = @OToId, @VangLaiId = @VangLaiId;
            END

            IF @NightKhungId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
                SELECT b.Id, @NightKhungId,
                       CASE WHEN b.LoaiVeId = @ThangId THEN 0.00 ELSE
                            CASE WHEN b.LoaiXeId = @XeMayId THEN 3000.00 WHEN b.LoaiXeId = @OToId THEN 10000.00 ELSE 0.00 END END
                FROM dbo.BangGia b
                WHERE NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @NightKhungId)
                  AND (b.GiaQuaDem IS NULL OR b.GiaQuaDem = 0)
                  AND (b.LoaiVeId = @VangLaiId OR b.LoaiVeId = @ThangId);', 
                  N'@NightKhungId INT, @ThangId INT, @XeMayId INT, @OToId INT, @VangLaiId INT', 
                  @NightKhungId = @NightKhungId, @ThangId = @ThangId, @XeMayId = @XeMayId, @OToId = @OToId, @VangLaiId = @VangLaiId;
            END

            COMMIT TRANSACTION;
            ";
        }

        private static string GetEmbeddedGatesMigrationSql()
        {
            return @"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            -- 1) Create ParkingGates table if it does not exist
            IF OBJECT_ID(N'dbo.ParkingGates', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ParkingGates (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    SiteId INT NOT NULL,
                    GateCode NVARCHAR(50) NOT NULL UNIQUE,
                    GateName NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(250) NULL,
                    IsActive BIT NOT NULL DEFAULT(1),
                    CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
                    CONSTRAINT FK_ParkingGates_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id)
                );
            END

            -- 2) Add LoaiXeId to ParkingZones if it does not exist
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ParkingZones' AND COLUMN_NAME = 'LoaiXeId')
            BEGIN
                EXEC sp_executesql N'ALTER TABLE dbo.ParkingZones ADD LoaiXeId INT NULL;';
                EXEC sp_executesql N'ALTER TABLE dbo.ParkingZones ADD CONSTRAINT FK_ParkingZones_LoaiXe FOREIGN KEY (LoaiXeId) REFERENCES dbo.LoaiXe(Id);';
            END

            -- 3) Add GateId to C3Controllers if it does not exist
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'C3Controllers' AND COLUMN_NAME = 'GateId')
            BEGIN
                -- Alter existing columns to make ZoneId nullable since controllers will now belong to Gates
                EXEC sp_executesql N'ALTER TABLE dbo.C3Controllers ALTER COLUMN ZoneId INT NULL;';
                
                EXEC sp_executesql N'ALTER TABLE dbo.C3Controllers ADD GateId INT NULL;';
                EXEC sp_executesql N'ALTER TABLE dbo.C3Controllers ADD CONSTRAINT FK_C3Controllers_ParkingGates FOREIGN KEY (GateId) REFERENCES dbo.ParkingGates(Id);';
            END

            -- 4) Add GateId to Lanes if it does not exist
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Lanes' AND COLUMN_NAME = 'GateId')
            BEGIN
                EXEC sp_executesql N'ALTER TABLE dbo.Lanes ADD GateId INT NULL;';
                EXEC sp_executesql N'ALTER TABLE dbo.Lanes ADD CONSTRAINT FK_Lanes_ParkingGates FOREIGN KEY (GateId) REFERENCES dbo.ParkingGates(Id);';
            END

            -- 5) DATA MIGRATION: Migrate existing Zone-based topology to Gate-based topology
            -- Create a default Gate for each existing Zone to prevent configuration loss
            IF EXISTS (SELECT 1 FROM dbo.ParkingZones)
            BEGIN
                -- Auto-insert Gates from existing Zones
                EXEC sp_executesql N'
                INSERT INTO dbo.ParkingGates (SiteId, GateCode, GateName, Description, IsActive)
                SELECT z.SiteId, z.ZoneCode + ''-GATE'', z.ZoneName + N'' Gate'', z.Description, z.IsActive
                FROM dbo.ParkingZones z
                WHERE NOT EXISTS (SELECT 1 FROM dbo.ParkingGates g WHERE g.GateCode = z.ZoneCode + ''-GATE'');';

                -- Connect existing Controllers to the new Gates
                EXEC sp_executesql N'
                UPDATE c
                SET c.GateId = g.Id
                FROM dbo.C3Controllers c
                JOIN dbo.ParkingZones z ON c.ZoneId = z.Id
                JOIN dbo.ParkingGates g ON z.ZoneCode + ''-GATE'' = g.GateCode
                WHERE c.GateId IS NULL;';

                -- Connect existing Lanes to the new Gates
                EXEC sp_executesql N'
                UPDATE l
                SET l.GateId = g.Id
                FROM dbo.Lanes l
                JOIN dbo.ParkingZones z ON l.ZoneId = z.Id
                JOIN dbo.ParkingGates g ON z.ZoneCode + ''-GATE'' = g.GateCode
                WHERE l.GateId IS NULL;';
            END

            -- 6) Map Vehicle Types to default zones if they exist (Helper for Dynamic Resolution)
            DECLARE @XeMayId INT = (SELECT TOP 1 Id FROM dbo.LoaiXe WHERE TenLoai = N'Xe máy');
            DECLARE @OToId INT = (SELECT TOP 1 Id FROM dbo.LoaiXe WHERE TenLoai = N'Ô tô');

            IF @XeMayId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                UPDATE dbo.ParkingZones
                SET LoaiXeId = @val
                WHERE (ZoneCode LIKE ''%XE_MAY%'' OR ZoneCode LIKE ''%MOTOR%'' OR ZoneName LIKE N''%Xe máy%'' OR ZoneName LIKE N''%Xe may%'')
                  AND LoaiXeId IS NULL;', N'@val INT', @val = @XeMayId;
            END

            IF @OToId IS NOT NULL
            BEGIN
                EXEC sp_executesql N'
                UPDATE dbo.ParkingZones
                SET LoaiXeId = @val
                WHERE (ZoneCode LIKE ''%OTO%'' OR ZoneCode LIKE ''%CAR%'' OR ZoneName LIKE N''%Ô tô%'' OR ZoneName LIKE N''%O to%'')
                  AND LoaiXeId IS NULL;', N'@val INT', @val = @OToId;
            END

            COMMIT TRANSACTION;
            ";
        }

        private static string GetEmbeddedLaneVehicleTypeMigrationSql()
        {
            return @"
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            -- Add LoaiXeId to Lanes table (nullable – NULL means mixed/hybrid lane)
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Lanes' AND COLUMN_NAME = 'LoaiXeId')
            BEGIN
                EXEC sp_executesql N'ALTER TABLE dbo.Lanes ADD LoaiXeId INT NULL;';
                EXEC sp_executesql N'ALTER TABLE dbo.Lanes ADD CONSTRAINT FK_Lanes_LoaiXe FOREIGN KEY (LoaiXeId) REFERENCES dbo.LoaiXe(Id);';
            END

            COMMIT TRANSACTION;
            ";
        }
    }
}
