namespace RemoteTickets.Application.Tenant.Requests;

/// <summary>Query to retrieve tenant setup status</summary>
/// <param name="TenantId">Id of tenant to retrieve setup status</param>
public sealed record GetTenantSetupStatusQuery(string TenantId) : IRequest;

/// <summary>Command to complete tenant setup </summary>
/// <param name="TenantId">Id of tenant to complete setup</param>
public sealed record CompleteTenantSetupCommand(string TenantId) : IRequest;

/// <summary>Represents a command to provision a tenant and its first administrator.</summary>
/// <param name="Name">The tenant display name.</param>
/// <param name="DatabaseName">The SQL Server database name.</param>
/// <param name="ConnectionString">The connection string for the tenant database.</param>
/// <param name="AdminEmail">The first tenant administrator email.</param>
/// <param name="AdminPassword">The first tenant administrator password.</param>
public sealed record CreateTenantCommand(string Name, string DatabaseName, string ConnectionString, string AdminEmail, string AdminPassword) : IRequest
{
     public CreateTenantRequest ToRequest() 
        => new (null!,(string.IsNullOrEmpty(Name) || string.IsNullOrWhiteSpace(Name) ? throw new ArgumentNullException(nameof(Name)) : Name),
            (string.IsNullOrEmpty(DatabaseName) || string.IsNullOrWhiteSpace(DatabaseName)? throw new ArgumentNullException(nameof(DatabaseName)) : DatabaseName),
            (string.IsNullOrEmpty(ConnectionString) || string.IsNullOrWhiteSpace(ConnectionString)? throw new ArgumentNullException(nameof(ConnectionString)) : ConnectionString),
            (string.IsNullOrEmpty(AdminEmail) || string.IsNullOrWhiteSpace(AdminEmail)? throw new ArgumentNullException(nameof(AdminEmail)) : AdminEmail),
            (string.IsNullOrEmpty(AdminPassword) || string.IsNullOrWhiteSpace(AdminPassword)? throw new ArgumentNullException(nameof(AdminPassword)) : AdminPassword));
}

/// <summary>Query to retrieve an tenant by id</summary>
/// <param name="TenantId">The id of tenant to retrieve.</param>
public sealed record GetTenantQuery(string TenantId) : IRequest;

/// <summary>Query to retrieve all tenant</summary>
public sealed record GetTenantsQuery() : IRequest;