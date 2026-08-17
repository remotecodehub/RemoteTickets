namespace RemoteTickets.Domain.Common;

/// <summary> Interface for abstrace base entity </summary>
public interface IEntityBase
{
    string Id { get; set; }
}

/// <summary> Abstract base entity for all entities tracked by DbContext. </summary>
public abstract class EntityBase(string id = "") : IEntityBase
{
    public string Id { get; set; } = id == "" ?  Guid.CreateVersion7().ToString() : id;
}

 