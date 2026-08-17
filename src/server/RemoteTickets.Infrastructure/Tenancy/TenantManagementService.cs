namespace RemoteTickets.Infrastructure.Tenancy;

/// <summary>Implements tenant catalog, database provisioning, and tenant setup operations.</summary>
public sealed class TenantManagementService(
    RemoteTicketsDbContext catalog,
    IIdentityService identityService,
    ILogger<TenantManagementService> logger) : ITenantManagementService
{
    /// <inheritdoc />
    public async Task<TenantResponse?> GetAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tenant = await catalog.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return tenant is null ? null : Map(tenant);
    }

    /// <inheritdoc />
    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        if (await catalog.Tenants.AnyAsync(x => x.Id == request.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Tenant '{request.Id}' already exists.");
        }

        await CreateDatabaseAsync(request.ConnectionString, request.DatabaseName, cancellationToken);
        await InitializeTenantDatabaseAsync(request.ConnectionString, cancellationToken);

        var tenant = new Tenant
        {
            Id = request.Id,
            Name = request.Name,
            DatabaseName = request.DatabaseName,
            ConnectionString = request.ConnectionString,
            IsActive = true,
            IsSetupComplete = false
        };

        catalog.Tenants.Add(tenant);
        await catalog.SaveChangesAsync(cancellationToken);

        var adminResult = await identityService.CreateTenantAdminAsync(request.Id, request.AdminEmail, request.AdminPassword, cancellationToken);
        if (!adminResult.Succeeded)
        {
            catalog.Tenants.Remove(tenant);
            await catalog.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(string.Join(" ", adminResult.Errors));
        }

        logger.LogInformation("Tenant {TenantId} was provisioned with database {DatabaseName}.", request.Id, request.DatabaseName);
        return Map(tenant);
    }

    /// <inheritdoc />
    public async Task<TenantSetupStatusResponse> GetSetupStatusAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tenant = await GetTenantEntityAsync(tenantId, cancellationToken);
        await using var context = CreateSetupContext(tenant.ConnectionString);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        var state = await context.SetupStates.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        return new TenantSetupStatusResponse(state is null || !state.IsComplete, state?.IsComplete == true);
    }

    /// <inheritdoc />
    public async Task<TenantSetupStatusResponse> CompleteSetupAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tenant = await GetTenantEntityAsync(tenantId, cancellationToken);
        await using var context = CreateSetupContext(tenant.ConnectionString);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        var state = await context.SetupStates.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (state is null)
        {
            state = new TenantSetupState { Id = 1 };
            context.SetupStates.Add(state);
        }

        state.IsComplete = true;
        state.CompletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        tenant.IsSetupComplete = true;
        await catalog.SaveChangesAsync(cancellationToken);
        return new TenantSetupStatusResponse(false, true);
    }

    private async Task<Tenant> GetTenantEntityAsync(string tenantId, CancellationToken cancellationToken)
    {
        return await catalog.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant '{tenantId}' was not found.");
    }

    private static TenantSetupDbContext CreateSetupContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TenantSetupDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new TenantSetupDbContext(options);
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        string escaped = $"[{databaseName.Replace("]", "]]", StringComparison.Ordinal)}]";
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(@databaseName) IS NULL CREATE DATABASE {escaped};";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InitializeTenantDatabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = CreateSetupContext(connectionString);
        // The repository currently has no EF migration history. EnsureCreated creates the foundation schema;
        // the same tenant connection is used by the future migration runner when migrations are introduced.
        await context.Database.MigrateAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    private static void Validate(CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            throw new ArgumentException("Tenant id and database name are required.");
        }

        if (string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            throw new ArgumentException("A tenant database connection string is required.");
        }
    }

    private static TenantResponse Map(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.DatabaseName, tenant.IsActive, tenant.IsSetupComplete);
}
