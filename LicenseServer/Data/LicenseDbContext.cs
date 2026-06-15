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
                entity.HasIndex(e => new { e.LicenseId, e.Fingerprint }).IsUnique();
            });

            modelBuilder.Entity<License>().HasData(
                new License { Id = 1, LicenseKey = "D8X1-75V5-R8BH-671R", MaxMachines = 4, Status = "NOT_ACTIVATED", ExpireDate = new DateTime(2027, 6, 12, 6, 9, 40, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 6, 12, 6, 9, 40, DateTimeKind.Utc) },
                new License { Id = 2, LicenseKey = "G5J2-2T42-NRXU-GNQC", MaxMachines = 4, Status = "REVOKED", ExpireDate = new DateTime(2027, 6, 12, 6, 11, 9, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 6, 12, 6, 11, 9, DateTimeKind.Utc) },
                new License { Id = 3, LicenseKey = "QUP9-AANY-KND8-YSQ3", MaxMachines = 10, Status = "NOT_ACTIVATED", ExpireDate = new DateTime(3000, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 6, 12, 6, 11, 9, DateTimeKind.Utc) },
                new License { Id = 4, LicenseKey = "QD91-996N-ZD19-6CMC", MaxMachines = 1, Status = "REVOKED", ExpireDate = new DateTime(3000, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 6, 12, 6, 11, 9, DateTimeKind.Utc) },
                new License { Id = 5, LicenseKey = "54YN-86R1-T9RW-LCDX", MaxMachines = 1, Status = "NOT_ACTIVATED", ExpireDate = new DateTime(3000, 12, 31, 0, 0, 0, DateTimeKind.Utc), CreatedAt = new DateTime(2026, 6, 12, 6, 11, 9, DateTimeKind.Utc) }
            );
        }
    }
}
