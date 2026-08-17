namespace RemoteTickets.Infrastructure.Persistence.Configurations.Auditing;

/// <summary>Configures the SQL representation of persisted entity audit history.</summary>
public sealed class EntityAuditRecordConfiguration : IEntityTypeConfiguration<EntityAuditRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EntityAuditRecord> builder)
    {
        builder.ToTable("EntityHistory", "audit");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(256).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Operation).HasMaxLength(32).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.PreviousEntityState).IsRequired();
        builder.Property(x => x.CurrentEntityState).IsRequired();
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.UpdatedAt });
    }
}
