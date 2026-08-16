namespace RemoteTickets.Infrastructure.Identity.Models;
/// <summary>
/// Represents a role in the RemoteTickets application.
/// </summary>
public class Role : IdentityRole<string>, ISoftDeletable
{
    /// <summary>Indicates whether the role is deleted.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>Gets or sets the date and time when the role was deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
    /// <summary>Role parameterless constructor.</summary>
    public Role() : base()
    {
        Id = Guid.CreateVersion7().ToString();
    }
    /// <summary>Role constructor.</summary>
    public Role(string name) : base(name)
    {
        Id = Guid.CreateVersion7().ToString();
    }
}
