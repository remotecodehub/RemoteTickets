namespace RemoteTickets.Controllers;

/// <summary>Exposes system-level tenant provisioning endpoints.</summary>
/// <param name="tenants">The tenant management service.</param>
[ApiController]
[Route("/api/v1/system/tenants")]
[Authorize(Policy = TenantPolicies.SysAdmin)]
public sealed class TenantController(ITenantManagementService tenants) : ControllerBase
{
    /// <summary>Provisions a tenant database and its first tenant administrator.</summary>
    /// <param name="request">The tenant provisioning request.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The newly provisioned tenant.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TenantResponse tenant = await tenants.CreateAsync(request, cancellationToken);
            return Created($"/api/v1/{tenant.Id}/setup", tenant);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    /// <summary>Gets a tenant from the central catalog.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The tenant metadata.</returns>
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> Get(string tenantId, CancellationToken cancellationToken)
    {
        TenantResponse? tenant = await tenants.GetAsync(tenantId, cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}
