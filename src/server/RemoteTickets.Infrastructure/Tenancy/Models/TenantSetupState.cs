namespace RemoteTickets.Infrastructure.Tenancy.Models;

/// <summary>Stores the initialization state of a tenant database.</summary>
public sealed class TenantSetupState : IEntityBase
{
    /// <summary>Gets or sets the singleton setup-state identifier.</summary>
    public string Id { get; set; } = RemoteTicketsConstants.TenantSetupId;
    /// <summary>Gets or sets whether the tenant database setup has completed.</summary>
    public bool IsComplete { get; set; }
    /// <summary>Gets or sets the last setup validation timestamp.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
