namespace RemoteTickets.Domain.Common;

public interface IEntityAuditable
{
    DateTimeOffset CreatedAt { get; init; }
    string CreatedBy { get; init; }
}

public interface IEntityUpdateHistory<T> where T : IEntityAuditable
{
    string CurrentEntityState { get; init; }
    string PreviousEntityState { get; init; } 
    DateTimeOffset UpdatedAt { get; init; }
    string UpdatedBy { get; set; }
}