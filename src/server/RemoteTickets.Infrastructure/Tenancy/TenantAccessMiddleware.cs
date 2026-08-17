using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using RemoteTickets.Application.Common.Tenancy;

namespace RemoteTickets.Infrastructure.Tenancy;

/// <summary>Enforces tenant route isolation and mandatory setup state for HTTP requests.</summary>
public sealed class TenantAccessMiddleware(RequestDelegate next, ITenantManagementService tenants, IIdentityService identityService)
{
    /// <summary>Processes the current request and enforces tenant isolation when a tenant route is present.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The asynchronous middleware operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var anonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var systemStatus = await identityService.GetSetupStatusAsync(context.RequestAborted);
        if (systemStatus.IsSetupRequired)
        {
            if (IsSystemSetupEndpoint(context)) { await next(context); return; }
            await RejectOrRedirectAsync(context, "/setup", StatusCodes.Status503ServiceUnavailable);
            return;
        }

        if (!context.Request.RouteValues.TryGetValue("tenantId", out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
        {
            if (anonymous) await next(context);
            else await RejectOrRedirectAsync(context, "/setup", StatusCodes.Status503ServiceUnavailable);
            return;
        }

        var tenantId = value.ToString()!;
        if (string.Equals(tenantId, "system", StringComparison.OrdinalIgnoreCase))
        {
            if (context.User.Identity?.IsAuthenticated != true || !context.User.IsInRole(TenantRoles.SysAdmin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
            return;
        }

        var tenant = await tenants.GetAsync(tenantId, context.RequestAborted);
        if (tenant is null || !tenant.IsActive)
        {
            await RejectOrRedirectAsync(context, "/not-found", StatusCodes.Status404NotFound);
            return;
        }

        if (anonymous)
        {
            if (!tenant.IsSetupComplete && !IsTenantSetupEndpoint(context) && !IsTenantLoginEndpoint(context))
            {
                await RejectOrRedirectAsync(context, $"/{tenantId}/setup", StatusCodes.Status409Conflict);
                return;
            }
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.User.IsInRole(TenantRoles.SysAdmin))
        {
            var assignedTenant = context.User.FindFirst(TenantClaimTypes.TenantId)?.Value;
            if (!string.Equals(assignedTenant, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                await RejectOrRedirectAsync(context, $"/{assignedTenant ?? tenantId}", StatusCodes.Status403Forbidden);
                return;
            }
        }

        if (!tenant.IsSetupComplete && !IsTenantSetupEndpoint(context))
        {
            await RejectOrRedirectAsync(context, $"/{tenantId}/setup", StatusCodes.Status409Conflict);
            return;
        }

        await next(context);
    }

    private static bool IsSystemSetupEndpoint(HttpContext context) => context.Request.Path.StartsWithSegments("/api/setup") || string.Equals(context.Request.Path.Value, "/setup", StringComparison.OrdinalIgnoreCase);

    private static bool IsTenantLoginEndpoint(HttpContext context) => string.Equals(context.Request.Path.Value, $"/api/v1/{context.Request.RouteValues["tenantId"]}/login", StringComparison.OrdinalIgnoreCase);

    private static bool IsTenantSetupEndpoint(HttpContext context)
    {
        var tenantId = context.Request.RouteValues.TryGetValue("tenantId", out var value) ? value?.ToString() : null;
        if (string.IsNullOrWhiteSpace(tenantId)) return false;
        var path = context.Request.Path.Value;
        return string.Equals(path, $"/{tenantId}/setup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, $"/api/v1/{tenantId}/setup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, $"/api/v1/{tenantId}/setup/complete", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RejectOrRedirectAsync(HttpContext context, string location, int statusCode)
    {
        if (HttpMethods.IsGet(context.Request.Method) && context.Request.Headers.Accept.Any(x => x.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Redirect(location);
            return;
        }
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new { title = "Setup required", detail = "Complete the required setup before using this endpoint.", setup = location }, context.RequestAborted);
    }
}
