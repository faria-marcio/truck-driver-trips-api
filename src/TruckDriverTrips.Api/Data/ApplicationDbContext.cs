using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TruckDriverTrips.Api.Models;

namespace TruckDriverTrips.Api.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Trip> Trips => Set<Trip>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.Name)
                .HasMaxLength(120)
                .IsRequired();
        });

        builder.Entity<Trip>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DistanceKm).HasPrecision(10, 2);
            entity.Property(x => x.PickupLocation).HasMaxLength(250).IsRequired();
            entity.Property(x => x.DropoffLocation).HasMaxLength(250).IsRequired();
            entity.Property(x => x.DriverId).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasOne(x => x.Driver)
                .WithMany()
                .HasForeignKey(x => x.DriverId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.DriverId, x.Date });
        });
    }

    public override int SaveChanges()
    {
        ApplyTripTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTripTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTripTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Trip>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = utcNow;
                entry.Entity.UpdatedAtUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = utcNow;
            }
        }
    }
}
