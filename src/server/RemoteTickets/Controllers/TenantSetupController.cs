namespace RemoteTickets.Controllers;

/// <summary>Exposes setup and validation operations for an individual tenant database.</summary>
/// <param name="tenants">The tenant management service.</param>
[ApiController]
[Route("/api/v1/{tenantId}/setup")]
public sealed class TenantSetupController(ITenantManagementService tenants) : ControllerBase
{
    /// <summary>Gets the current tenant setup state.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The tenant setup state.</returns>
    [HttpGet]
    [AllowAnonymous]
    public Task<TenantSetupStatusResponse> GetStatus(string tenantId, CancellationToken cancellationToken) => tenants.GetSetupStatusAsync(tenantId, cancellationToken);

    /// <summary>Completes tenant setup after the required tenant validations have passed.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The resulting tenant setup state.</returns>
    [HttpPost("complete")]
    [Authorize(Policy = TenantPolicies.TenantAdmin)]
    public async Task<IActionResult> Complete(string tenantId, CancellationToken cancellationToken)
    {
        var result = await tenants.CompleteSetupAsync(tenantId, cancellationToken);
        return Ok(result);
    }
}
