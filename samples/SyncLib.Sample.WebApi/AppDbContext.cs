using Microsoft.EntityFrameworkCore;
using SyncLib.EntityFrameworkCore;
using SyncLib.Sample.WebApi.Sync;

namespace SyncLib.Sample.WebApi;

public sealed class AppDbContext : DbContext, ISyncStateDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<WeatherEntity> Weather => Set<WeatherEntity>();

    public DbSet<SyncStateEntity> SyncStates => Set<SyncStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.City).HasMaxLength(100).IsRequired();
        });

        modelBuilder.ConfigureSyncState();
    }
}
