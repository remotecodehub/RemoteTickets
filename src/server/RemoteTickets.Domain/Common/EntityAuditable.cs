namespace RemoteTickets.Domain.Common;

/// <summary>Defines creation metadata that is persisted with an auditable entity.</summary>
public interface IEntityAuditable
{
    /// <summary>Gets the UTC timestamp at which the entity was created.</summary>
    DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the identifier of the actor that created the entity.</summary>
    string CreatedBy { get; init; }
}

/// <summary>Defines the state transition recorded for an auditable entity.</summary>
/// <typeparam name="T">The auditable entity contract represented by the history record.</typeparam>
public interface IEntityUpdateHistory<T> where T : IEntityAuditable
{
    /// <summary>Gets the serialized state after the operation.</summary>
    string CurrentEntityState { get; init; }

    /// <summary>Gets the serialized state before the operation.</summary>
    string PreviousEntityState { get; init; }

    /// <summary>Gets the UTC timestamp at which the state transition was recorded.</summary>
    DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets or sets the identifier of the actor that caused the state transition.</summary>
    string UpdatedBy { get; set; }
}
