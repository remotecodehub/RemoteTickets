namespace RemoteTickets.Infrastructure.Persistence.Auditing;

/// <summary>Represents a persisted state transition for an auditable entity.</summary>
public sealed class EntityAuditRecord : IEntityUpdateHistory<IEntityAuditable>
{
    /// <summary>Gets or sets the audit record identifier.</summary>
    public string Id { get; set; } = Guid.CreateVersion7().ToString();

    /// <summary>Gets or sets the CLR entity type name represented by the record.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Gets or sets the identifier of the audited entity.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation that caused the audit entry.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <inheritdoc />
    public string CurrentEntityState { get; init; } = string.Empty;

    /// <inheritdoc />
    public string PreviousEntityState { get; init; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string UpdatedBy { get; set; } = string.Empty;
}
