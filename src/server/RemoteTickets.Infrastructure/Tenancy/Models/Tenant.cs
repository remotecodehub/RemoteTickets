namespace RemoteTickets.Infrastructure.Tenancy.Models;

/// <summary>Represents a tenant registered in the installation catalog.</summary>
public sealed class Tenant
{
    /// <summary>Gets or sets the stable tenant identifier used by routes and tokens.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Gets or sets the display name of the tenant.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the SQL Server database name assigned to the tenant.</summary>
    public string DatabaseName { get; set; } = string.Empty;
    /// <summary>Gets or sets the connection string used to access the tenant database.</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Gets or sets whether the tenant accepts normal requests.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Gets or sets whether tenant setup has completed.</summary>
    public bool IsSetupComplete { get; set; }
    /// <summary>Gets or sets the tenant creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
