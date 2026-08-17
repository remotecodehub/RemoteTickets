using Microsoft.EntityFrameworkCore.Metadata;

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
        foreach (IMutableEntityType entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "entity");
            MemberExpression property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            LambdaExpression filter = Expression.Lambda(Expression.Not(property), parameter);
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
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTimeOffset.UtcNow;
        }
    }
}
