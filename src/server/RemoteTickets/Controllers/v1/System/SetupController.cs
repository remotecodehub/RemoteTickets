namespace RemoteTickets.Controllers.v1.System;

/// <summary>Exposes anonymous endpoints used during first-time application setup.</summary>
/// <param name="mediator">The mediator used to dispatch setup requests.</param>
[ApiController]
[Route("api/v1/setup")]
[Tags("System", "Setup")]
public sealed class SetupController(IMediator mediator) : ControllerBase
{
    /// <summary>Gets the current first-time setup status.</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType<SetupStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken) 
        => Ok(await mediator.RequestAsync<GetSetupStatusQuery, SetupStatusResponse>(
            new GetSetupStatusQuery(), cancellationToken));

    /// <summary>Persists the master database connection string and initializes its schema.</summary>
    [HttpPost("database")]
    [AllowAnonymous]
    [ProducesResponseType<MasterDatabaseSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType<MasterDatabaseSetupResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ConfigureMasterDatabase(MasterDatabaseSetupRequest request, CancellationToken cancellationToken)
    {
        MasterDatabaseSetupResponse response = await mediator.RequestAsync<MasterDatabaseSetupCommand, MasterDatabaseSetupResponse>(
            new MasterDatabaseSetupCommand(request), cancellationToken);
        return response.Status ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    /// <summary>Initializes the application with its first system administrator account.</summary>
    [HttpPost("initialize")]
    [AllowAnonymous]
    [ProducesResponseType<IdentityResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType<IdentityResultResponse>(StatusCodes.Status409Conflict)] 
    public async Task<IActionResult> Initialize(InitializeSetupRequest request, CancellationToken cancellationToken)
    {
        IdentityResultResponse result = await mediator.RequestAsync<InitializeSetupCommand, IdentityResultResponse>(
            new InitializeSetupCommand(request.Email, request.Password),
            cancellationToken);
        return result.Succeeded ? Ok(result) : Conflict(result);
    }
}
