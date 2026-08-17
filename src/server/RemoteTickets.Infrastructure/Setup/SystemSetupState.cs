namespace RemoteTickets.Infrastructure.Setup;

/// <summary>Stores the singleton installation setup state in the master database.</summary>
public sealed class SystemSetupState
{
    /// <summary>Gets or sets the singleton setup-state identifier.</summary>
    public string Id { get; set; } = RemoteTicketsConstants.SystemSetupId;
    /// <summary>Gets or sets whether the installation is ready for normal use.</summary>
    public bool IsComplete { get; set; }
    /// <summary>Gets or sets the completion timestamp.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
