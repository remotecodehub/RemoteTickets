namespace RemoteTickets.Infrastructure.Identity.Models;

/// <summary>
/// Represents a user in the RemoteTickets application.
/// </summary>
public class User : IdentityUser<string>, ISoftDeletable
{
    /// <summary>User name displayed in the application.</summary>
    public string? DisplayName { get; set; } = string.Empty;
    /// <summary>First name of the user.</summary>
    public string? FirstName { get; set; } = string.Empty;
    /// <summary>Surname of the user.</summary>
    public string? SurName { get; set; } = string.Empty;
    /// <summary>Indicates whether the user is deleted.</summary>
    public bool IsDeleted { get; set; } 
    /// <summary>Gets or sets the date and time when the user was deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="User"/> class.
    /// </summary>
    public User() : base()
    {
        Id = Guid.CreateVersion7().ToString();
    }
    /// <inheritdoc />
    public User(string name) : base(name)
    {
        Id = Guid.CreateVersion7().ToString();
    }
}
