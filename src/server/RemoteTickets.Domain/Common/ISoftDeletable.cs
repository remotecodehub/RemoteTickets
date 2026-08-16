namespace RemoteTickets.Domain.Common;

/// <summary>Defines an entity that can be hidden through soft deletion instead of physical deletion.</summary>
public interface ISoftDeletable
{
    /// <summary>Gets or sets whether the entity is logically deleted.</summary>
    bool IsDeleted { get; set; }

    /// <summary>Gets or sets the time at which the entity was logically deleted.</summary>
    DateTimeOffset? DeletedAt { get; set; }
}
