namespace RemoteTickets.Infrastructure.Persistence;

/// <summary>Represents the central database context for RemoteTickets identity, setup, and tenant catalog data.</summary>
public sealed class RemoteTicketsDbContext(DbContextOptions<RemoteTicketsDbContext> options) : IdentityDbContext<User, Role, string>(options)
{
    /// <summary>Gets the registered tenants.</summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();
    /// <summary>Gets the singleton system setup state.</summary>
    public DbSet<SystemSetupState> SystemSetup => Set<SystemSetupState>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DatabaseName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ConnectionString).IsRequired();
            entity.HasIndex(x => x.DatabaseName).IsUnique();
        });
        builder.Entity<SystemSetupState>(entity =>
        {
            entity.ToTable("SystemSetupState");
            entity.HasKey(x => x.Id);
        });
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(property), parameter);
            builder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
        builder.ApplyConfigurationsFromAssembly(typeof(RemoteTicketsDbContext).Assembly);
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { ApplySoftDelete(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    /// <inheritdoc />
    public override int SaveChanges() { ApplySoftDelete(); return base.SaveChanges(); }
    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { ApplySoftDelete(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { ApplySoftDelete(); return base.SaveChangesAsync(cancellationToken); }

    private void ApplySoftDelete()
    {
        foreach (EntityEntry<ISoftDeletable> entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted) continue;
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTimeOffset.UtcNow;
        }
    }
}
