using Microsoft.EntityFrameworkCore;
using LicenseServer.Data;
using LicenseServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure SQLite DbContext
builder.Services.AddDbContext<LicenseDbContext>(options =>
    options.UseSqlite("Data Source=licenses.db"));

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
    }
}

app.UseAuthorization();

app.MapControllers();

app.Run();
