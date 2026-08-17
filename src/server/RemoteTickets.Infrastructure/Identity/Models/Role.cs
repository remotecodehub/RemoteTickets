namespace RemoteTickets.Infrastructure.Identity.Models;

/// <summary>Represents a role in the RemoteTickets application.</summary>
public class Role : IdentityRole<string>, ISoftDeletable, IEntityAuditable
{
    /// <summary>Indicates whether the role is deleted.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>Gets or sets the date and time when the role was deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; init; }
    /// <inheritdoc />
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Initializes a role with a new identifier.</summary>
    public Role() : base() => Id = Guid.CreateVersion7().ToString();

    /// <summary>Initializes a role with the supplied name.</summary>
    /// <param name="name">The role name.</param>
    public Role(string name) : base(name) => Id = Guid.CreateVersion7().ToString();
}
