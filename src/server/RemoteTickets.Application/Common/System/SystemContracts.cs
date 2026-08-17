namespace RemoteTickets.Application.Common.System;

/// <summary>Represents the master database setup payload.</summary>
/// <param name="ConnectionString">The SQL Server connection string used by the tenant catalog.</param>
/// <param name="CommandTimeout">The value (in seconds) for use for operations timeout in SQL Server.</param>
public sealed record MasterDatabaseSetupRequest(
    string ConnectionString, 
    int CommandTimeout);
   
/// <summary>Represents the first-time setup administrator credentials.</summary>
/// <param name="Email">The administrator email address.</param>
/// <param name="Password">The administrator password.</param>
public sealed record InitializeSetupRequest(string Email, string Password);

/// <summary>
/// Contract for System Service
/// </summary>
public interface ISystemService
{
    /// <summary>
    /// Performs the setup of master database
    /// </summary>
    /// <param name="connectionString">Connection string of master database</param>
    /// <param name="commandTimeout">Timeout for operations on master database</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>An <see cref="MasterDatabaseSetupResponse" /> with the status of setup operation </returns>
    Task<MasterDatabaseSetupResponse> PerformMasterDatabaseSetup(string connectionString, int commandTimeout, CancellationToken cancellationToken);
}

