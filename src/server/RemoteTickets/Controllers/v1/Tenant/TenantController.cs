namespace RemoteTickets.Controllers.v1.Tenant;

/// <summary>Exposes system-level tenant provisioning endpoints.</summary>
/// <param name="tenants">The tenant management service.</param>
[ApiController]
[Route("/api/v1/system/tenants")]
[Authorize(Policy = TenantPolicies.SysAdmin)]
public sealed class TenantController(IMediator mediator) : ControllerBase
{
    /// <summary>Provisions a tenant database and its first tenant administrator.</summary>
    /// <param name="request">The tenant provisioning request.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The newly provisioned tenant.</returns>
    [HttpPost]
    [ProducesResponseType<TenantResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status405MethodNotAllowed)]
    public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TenantResponse tenant = 
                await mediator.RequestAsync<CreateTenantCommand, TenantResponse>(
                    request.ToCommand(),
                    cancellationToken);
            return Created($"/api/v1/{tenant.Id}/setup", tenant);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails{ Title="Argument Exception", Detail = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Title = "Invalid Operation Exception", Detail = exception.Message });
        }
    }

    /// <summary>Gets a tenant from the central catalog.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The tenant metadata.</returns>
    [HttpGet("{tenantId}")]
    [ProducesResponseType<TenantResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status405MethodNotAllowed)]
    public async Task<IActionResult> Get(string tenantId, CancellationToken cancellationToken)
    {
        TenantResponse? tenant = 
            await mediator.RequestAsync<GetTenantQuery, TenantResponse>(
                new GetTenantQuery(tenantId),
                cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}
