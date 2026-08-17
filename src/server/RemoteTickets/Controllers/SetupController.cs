namespace RemoteTickets.Controllers;

/// <summary>Exposes anonymous endpoints used during first-time application setup.</summary>
/// <param name="mediator">The mediator used to dispatch setup requests.</param>
/// <param name="configurationStore">The store used to persist the master database connection.</param>
[ApiController]
[Route("api/setup")]
public sealed class SetupController(IMediator mediator, ISetupConfigurationStore configurationStore) : ControllerBase
{
    /// <summary>Gets the current first-time setup status.</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public Task<SetupStatusResponse> GetStatus(CancellationToken cancellationToken) => mediator.RequestAsync<GetSetupStatusQuery, SetupStatusResponse>(new GetSetupStatusQuery(), cancellationToken);

    /// <summary>Persists the master database connection string and initializes its schema.</summary>
    [HttpPost("master-database")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfigureMasterDatabase(MasterDatabaseSetupRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionString)) return BadRequest(new { error = "A master database connection string is required." });
        await configurationStore.SetMasterConnectionStringAsync(request.ConnectionString, cancellationToken);
        var options = new DbContextOptionsBuilder<RemoteTicketsDbContext>().UseSqlServer(request.ConnectionString).Options;
        await using var dbContext = new RemoteTicketsDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        return Ok();
    }

    /// <summary>Initializes the application with its first system administrator account.</summary>
    [HttpPost("initialize")]
    [AllowAnonymous]
    public async Task<IActionResult> Initialize(InitializeSetupRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<InitializeSetupCommand, IdentityResultResponse>(new InitializeSetupCommand(request.Email, request.Password), cancellationToken);
        return result.Succeeded ? Ok(result) : Conflict(result);
    }
}

/// <summary>Represents the master database setup payload.</summary>
/// <param name="ConnectionString">The SQL Server connection string used by the tenant catalog.</param>
public sealed record MasterDatabaseSetupRequest(string ConnectionString);

/// <summary>Represents the first-time setup administrator credentials.</summary>
/// <param name="Email">The administrator email address.</param>
/// <param name="Password">The administrator password.</param>
public sealed record InitializeSetupRequest(string Email, string Password);
