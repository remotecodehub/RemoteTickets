namespace RemoteTickets.Infrastructure.Setup;

/// <summary> System service implementation.</summary>
public sealed class SystemService(ISetupConfigurationStore configurationStore) : ISystemService
{
    /// <inheritdoc/>
    public async Task<MasterDatabaseSetupResponse> PerformMasterDatabaseSetup(string connectionString, int commandTimeout, CancellationToken cancellationToken)
    {
        await configurationStore.SetMasterConnectionStringAsync(connectionString, cancellationToken);
        DbContextOptions<RemoteTicketsDbContext> options = new DbContextOptionsBuilder<RemoteTicketsDbContext>()
            .UseSqlServer(connectionString, options =>
            {
                options.CommandTimeout(commandTimeout);
            })
            .Options;
        await using RemoteTicketsDbContext dbContext = new(options);
        bool status = await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return new(status, status? "Master Database Successfully set up!" : "Master Database not set up!");
    }
}
