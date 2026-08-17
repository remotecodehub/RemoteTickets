using Microsoft.EntityFrameworkCore.Metadata;

namespace RemoteTickets.Infrastructure.Persistence;

/// <summary>Represents the central database context for RemoteTickets identity, setup, tenant catalog, and audit data.</summary>
public sealed class RemoteTicketsDbContext(DbContextOptions<RemoteTicketsDbContext> options, IHttpContextAccessor? httpContextAccessor = null) : IdentityDbContext<User, Role, string>(options)
{
    private readonly IHttpContextAccessor? _httpContextAccessor = httpContextAccessor;

    /// <summary>Gets the registered tenants.</summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>Gets the singleton system setup state.</summary>
    public DbSet<SystemSetupState> SystemSetup => Set<SystemSetupState>();

    /// <summary>Gets the persisted entity state transitions stored in the audit schema.</summary>
    public DbSet<EntityAuditRecord> EntityAuditHistory => Set<EntityAuditRecord>();

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
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreparePersistenceChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        PreparePersistenceChanges();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreparePersistenceChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PreparePersistenceChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void PreparePersistenceChanges()
    {
        ApplySoftDelete();
        ApplyCreationMetadata();
        AddAuditEntries();
    }

    private void ApplyCreationMetadata()
    {
        string actor = GetActor();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (EntityEntry<IEntityAuditable> entry in ChangeTracker.Entries<IEntityAuditable>().Where(x => x.State == EntityState.Added))
        {
            entry.Property(nameof(IEntityAuditable.CreatedAt)).CurrentValue = now;
            entry.Property(nameof(IEntityAuditable.CreatedBy)).CurrentValue = actor;
        }
    }

    private void AddAuditEntries()
    {
        string actor = GetActor();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<EntityAuditRecord> auditEntries = new();

        foreach (EntityEntry<IEntityAuditable> entry in ChangeTracker.Entries<IEntityAuditable>().ToArray())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            PropertyEntry? idProperty = entry.Properties.FirstOrDefault(x => string.Equals(x.Metadata.Name, "Id", StringComparison.OrdinalIgnoreCase));
            if (idProperty is null || idProperty.CurrentValue is null)
            {
                continue;
            }

            string operation = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => throw new InvalidOperationException("Unsupported audit state.")
            };

            object previousState = entry.State == EntityState.Added ? new { } : entry.OriginalValues.ToObject();
            object currentState = entry.State == EntityState.Deleted ? entry.OriginalValues.ToObject() : entry.CurrentValues.ToObject();
            auditEntries.Add(new EntityAuditRecord
            {
                EntityType = entry.Metadata.ClrType.FullName ?? entry.Metadata.ClrType.Name,
                EntityId = idProperty.CurrentValue.ToString() ?? string.Empty,
                Operation = operation,
                PreviousEntityState = JsonSerializer.Serialize(previousState),
                CurrentEntityState = JsonSerializer.Serialize(currentState),
                UpdatedAt = now,
                UpdatedBy = actor
            });
        }

        if (auditEntries.Count != 0)
        {
            EntityAuditHistory.AddRange(auditEntries);
        }
    }

    private string GetActor()
        => _httpContextAccessor?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? _httpContextAccessor?.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? _httpContextAccessor?.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
            ?? "system";

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
