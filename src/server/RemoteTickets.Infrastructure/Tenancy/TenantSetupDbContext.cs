namespace RemoteTickets.Infrastructure.Tenancy;

/// <summary>Provides persistence for tenant-database initialization state.</summary>
public sealed class TenantSetupDbContext(DbContextOptions<TenantSetupDbContext> options) : DbContext(options)
{
    /// <summary>Gets the singleton tenant setup state.</summary>
    public DbSet<TenantSetupState> SetupStates => Set<TenantSetupState>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantSetupStateConfiguration).Assembly);
    }
}
