using Microsoft.EntityFrameworkCore;
using LicenseServer.Models;

namespace LicenseServer.Data
{
    public class LicenseDbContext : DbContext
    {
        public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options)
        {
        }

        public DbSet<License> Licenses => Set<License>();
        public DbSet<Machine> Machines => Set<Machine>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<License>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.LicenseKey).IsUnique();
            });

            modelBuilder.Entity<Machine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.LicenseId, e.MachineFingerprint }).IsUnique();
            });
        }
    }
}
