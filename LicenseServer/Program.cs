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
