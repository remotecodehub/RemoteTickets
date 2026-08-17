namespace RemoteTickets.Application.Common.Tenancy;

/// <summary>Contains the application roles used by the tenant-aware authorization model.</summary>
public static class TenantRoles
{
    /// <summary>Gets the role that can administer the complete installation and every tenant.</summary>
    public const string SysAdmin = "sysadmin";
    /// <summary>Gets the role that can administer a single tenant.</summary>
    public const string TenantAdmin = "tenadmin";
    /// <summary>Gets the role that can operate the cash register and other non-configuration business functions.</summary>
    public const string TenantOperator = "tenop";
    /// <summary>Gets the role that can perform attendance and non-critical business operations.</summary>
    public const string TenantAttendant = "tenat";
}

/// <summary>Contains claim names used by the tenant-aware security model.</summary>
public static class TenantClaimTypes
{
    /// <summary>Gets the claim containing the tenant identifier assigned to a user.</summary>
    public const string TenantId = "tenant_id";
}

/// <summary>Contains authorization policy names used by tenant-aware endpoints.</summary>
public static class TenantPolicies
{
    /// <summary>Gets the policy requiring system administration privileges.</summary>
    public const string SysAdmin = "tenant.sysadmin";
    /// <summary>Gets the policy requiring tenant administration privileges.</summary>
    public const string TenantAdmin = "tenant.admin";
    /// <summary>Gets the policy requiring operator privileges.</summary>
    public const string TenantOperator = "tenant.operator";
    /// <summary>Gets the policy requiring attendant privileges.</summary>
    public const string TenantAttendant = "tenant.attendant";
}

/// <summary>Represents the setup state of a tenant database.</summary>
/// <param name="IsSetupRequired">Indicates whether tenant setup is still required.</param>
/// <param name="IsSetupComplete">Indicates whether tenant setup has completed.</param>
public sealed record TenantSetupStatusResponse(bool IsSetupRequired, bool IsSetupComplete) : IResponse;

/// <summary>Represents a tenant exposed to system administrators.</summary>
/// <param name="Id">The stable tenant identifier used in routes.</param>
/// <param name="Name">The tenant display name.</param>
/// <param name="DatabaseName">The database name assigned to the tenant.</param>
/// <param name="IsActive">Indicates whether the tenant accepts normal operations.</param>
/// <param name="IsSetupComplete">Indicates whether tenant setup has completed.</param>
public sealed record TenantResponse(string Id, string Name, string DatabaseName, bool IsActive, bool IsSetupComplete) : IResponse;

/// <summary>Represents a request to provision a tenant and its first administrator.</summary>
/// <param name="Id">The tenant identifier.</param>
/// <param name="Name">The tenant display name.</param>
/// <param name="DatabaseName">The SQL Server database name.</param>
/// <param name="ConnectionString">The connection string for the tenant database.</param>
/// <param name="AdminEmail">The first tenant administrator email.</param>
/// <param name="AdminPassword">The first tenant administrator password.</param>
public sealed record CreateTenantRequest(string Id, string Name, string DatabaseName, string ConnectionString, string AdminEmail, string AdminPassword)
{
    public CreateTenantCommand ToCommand() 
        => new ((string.IsNullOrEmpty(Name) || string.IsNullOrWhiteSpace(Name) ? throw new ArgumentNullException(nameof(Name)) : Name),
            (string.IsNullOrEmpty(DatabaseName) || string.IsNullOrWhiteSpace(DatabaseName)? throw new ArgumentNullException(nameof(DatabaseName)) : DatabaseName),
            (string.IsNullOrEmpty(ConnectionString) || string.IsNullOrWhiteSpace(ConnectionString)? throw new ArgumentNullException(nameof(ConnectionString)) : ConnectionString),
            (string.IsNullOrEmpty(AdminEmail) || string.IsNullOrWhiteSpace(AdminEmail)? throw new ArgumentNullException(nameof(AdminEmail)) : AdminEmail),
            (string.IsNullOrEmpty(AdminPassword) || string.IsNullOrWhiteSpace(AdminPassword)? throw new ArgumentNullException(nameof(AdminPassword)) : AdminPassword));
}

/// <summary>Provides tenant registration and setup operations.</summary>
public interface ITenantManagementService
{
    /// <summary>Gets a tenant by its route identifier.</summary>
    /// <param name="tenantId">The tenant route identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The tenant, or <see langword="null"/> when it does not exist.</returns>
    Task<TenantResponse?> GetAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>Creates a tenant database and its first tenant administrator.</summary>
    /// <param name="request">The tenant provisioning request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The provisioned tenant.</returns>
    Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken);

    /// <summary>Gets the setup state stored in a tenant database.</summary>
    /// <param name="tenantId">The tenant route identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The tenant setup state.</returns>
    Task<TenantSetupStatusResponse> GetSetupStatusAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>Completes tenant setup after all tenant initialization checks have succeeded.</summary>
    /// <param name="tenantId">The tenant route identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The resulting setup state.</returns>
    Task<TenantSetupStatusResponse> CompleteSetupAsync(string tenantId, CancellationToken cancellationToken);
}

