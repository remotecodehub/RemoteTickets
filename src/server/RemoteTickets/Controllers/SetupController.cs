namespace RemoteTickets.Controllers;

/// <summary>Exposes anonymous endpoints used during first-time application setup.</summary>
/// <param name="mediator">The mediator used to dispatch setup requests.</param>
[ApiController]
[Route("api/setup")]
public sealed class SetupController(IMediator mediator) : ControllerBase
{
    /// <summary>Gets the current first-time setup status.</summary>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The current setup status.</returns>
    [HttpGet("status")]
    [AllowAnonymous]
    public Task<SetupStatusResponse> GetStatus(CancellationToken cancellationToken) =>
        mediator.RequestAsync<GetSetupStatusQuery, SetupStatusResponse>(new GetSetupStatusQuery(), cancellationToken);

    /// <summary>Initializes the application with its first administrator account.</summary>
    /// <param name="request">The initial administrator credentials.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>An HTTP response indicating whether initialization succeeded.</returns>
    [HttpPost("initialize")]
    [AllowAnonymous]
    public async Task<IActionResult> Initialize(InitializeSetupRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<InitializeSetupCommand, IdentityResultResponse>(
            new InitializeSetupCommand(request.Email, request.Password), cancellationToken);

        return result.Succeeded ? Ok(result) : Conflict(result);
    }
}

/// <summary>Represents the first-time setup administrator credentials.</summary>
/// <param name="Email">The administrator email address.</param>
/// <param name="Password">The administrator password.</param>
public sealed record InitializeSetupRequest(string Email, string Password);
