namespace RemoteTickets.Controllers.v1.Tenant;

/// <summary>Exposes setup and validation operations for an individual tenant database.</summary>
/// <param name="tenants">The tenant management service.</param>
[ApiController]
[Route("/api/v1/{tenantId}/setup")]
[Tags("Tenant", "Setup")]
public sealed class TenantSetupController(IMediator mediator) : ControllerBase
{
    /// <summary>Gets the current tenant setup state.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The tenant setup state.</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<TenantSetupStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(string tenantId, CancellationToken cancellationToken) 
        => Ok(await mediator.RequestAsync<GetTenantSetupStatusQuery, TenantSetupStatusResponse >(
            new GetTenantSetupStatusQuery(tenantId),
             cancellationToken)); 
    

    /// <summary>Completes tenant setup after the required tenant validations have passed.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The resulting tenant setup state.</returns>
    [HttpPost("complete")]
    [Authorize(Policy = TenantPolicies.TenantAdmin)]
    [ProducesResponseType<TenantSetupStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(string tenantId, CancellationToken cancellationToken)
    {
        TenantSetupStatusResponse result = await mediator.RequestAsync<CompleteTenantSetupCommand,TenantSetupStatusResponse>(
            new CompleteTenantSetupCommand(tenantId), cancellationToken);
        return Ok(result);
    }
}
