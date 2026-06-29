using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using LicenseServer.Data;
using LicenseServer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// Add services to the container.
builder.Services.AddControllers();

// Configure SQLite DbContext in a writable CommonApplicationData location
var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "APS", "LicenseServer");
if (!Directory.Exists(appDataFolder))
{
    Directory.CreateDirectory(appDataFolder);
}

var dbPath = Path.Combine(appDataFolder, "licenses.db");
var fallbackDbPath = Path.Combine(AppContext.BaseDirectory, "licenses.db");

if (!File.Exists(dbPath) && File.Exists(fallbackDbPath))
{
    try
    {
        File.Copy(fallbackDbPath, dbPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to copy database template to ProgramData: {ex.Message}");
    }
}

builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register RSA Signing Service as a singleton
builder.Services.AddSingleton<IRsaSigningService, RsaSigningService>();

var app = builder.Build();

// Automatically initialize the database (ensure tables are created)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    try
    {
        context.Database.EnsureCreated();

        // ── Runtime migration: thêm cột hardware info nếu chưa có ──────────
        // SQLite không hỗ trợ ALTER TABLE ADD COLUMN IF NOT EXISTS
        // nên phải kiểm tra qua PRAGMA trước
        var conn = context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            conn.Open();

        // 1. Migrate Machines table
        var existingColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Machines)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                existingColumns.Add(reader.GetString(1)); // cột "name"
        }

        var newColumns = new (string Name, string Type)[]
        {
            ("LastActivatedAt", "TEXT NULL"),
            ("MachineName",     "TEXT NULL"),
            ("CpuId",           "TEXT NULL"),
            ("DiskSerial",      "TEXT NULL"),
            ("MacAddress",      "TEXT NULL"),
            ("OsVersion",       "TEXT NULL"),
            ("ActivatedFromIp", "TEXT NULL"),
            ("MotherboardSerial", "TEXT NULL"),
            ("BiosSerial",      "TEXT NULL"),
        };

        foreach (var (colName, colType) in newColumns)
        {
            if (!existingColumns.Contains(colName))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE Machines ADD COLUMN {colName} {colType}";
                cmd.ExecuteNonQuery();
                Console.WriteLine($"[DB Migration] Added column Machines.{colName}");
            }
        }

        // 2. Migrate Licenses table
        var existingLicenseColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Licenses)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                existingLicenseColumns.Add(reader.GetString(1));
        }

        var newLicenseColumns = new (string Name, string Type)[]
        {
            ("Product",      "TEXT NULL"),
            ("CustomerName", "TEXT NULL"),
            ("LicenseType",  "TEXT NULL"),
            ("Features",     "TEXT NULL"),
            ("Version",      "INTEGER DEFAULT 1"),
        };

        foreach (var (colName, colType) in newLicenseColumns)
        {
            if (!existingLicenseColumns.Contains(colName))
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE Licenses ADD COLUMN {colName} {colType}";
                cmd.ExecuteNonQuery();
                Console.WriteLine($"[DB Migration] Added column Licenses.{colName}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error initializing SQLite database: {ex.Message}");
        var currentEx = ex;
        int depth = 1;
        while (currentEx.InnerException != null)
        {
            currentEx = currentEx.InnerException;
            Console.WriteLine($"[Inner Exception Level {depth}]: {currentEx.Message}");
            Console.WriteLine($"Type: {currentEx.GetType().FullName}");
            Console.WriteLine($"StackTrace: {currentEx.StackTrace}");
            depth++;
        }
    }
}

app.UseAuthorization();

app.MapControllers();

app.Run();
