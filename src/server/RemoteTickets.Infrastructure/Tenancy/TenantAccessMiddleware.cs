using Microsoft.AspNetCore.Mvc;

namespace RemoteTickets.Infrastructure.Tenancy;

/// <summary>Enforces tenant route isolation and mandatory setup state for HTTP requests.</summary>
public sealed class TenantAccessMiddleware(RequestDelegate next, ITenantManagementService tenants, RemoteTicketsDbContext masterDb)
{
    /// <summary>Processes the current request and enforces tenant isolation when a tenant route is present.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The asynchronous middleware operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsInfrastructurePath(context.Request.Path)) { await next(context); return; }
        Endpoint? endpoint = context.GetEndpoint();
        bool anonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        SystemSetupState? setupState = await masterDb.SystemSetup.AsNoTracking().SingleOrDefaultAsync(x => x.Id == RemoteTicketsConstants.SystemSetupId, context.RequestAborted);
        bool systemSetupRequired = setupState is null || !setupState.IsComplete;
        if (systemSetupRequired)
        {
            if (IsSystemSetupEndpoint(context)) { await next(context); return; }
            await RejectOrRedirectAsync(context, "/setup", StatusCodes.Status503ServiceUnavailable);
            return;
        }
        if (IsSystemSetupEndpoint(context) && HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { error = "System setup is already complete." }, context.RequestAborted);
            return;
        }
        if (!context.Request.RouteValues.TryGetValue("tenantId", out object? value) || string.IsNullOrWhiteSpace(value?.ToString()))
        {
            if (anonymous)
            {
                await next(context);
            }
            else
            {
                await RejectOrRedirectAsync(context, "/setup", StatusCodes.Status503ServiceUnavailable);
            }

            return;
        }
        string tenantId = value.ToString()!;
        if (string.Equals(tenantId, "system", StringComparison.OrdinalIgnoreCase))
        {
            if (context.User.Identity?.IsAuthenticated != true || !context.User.IsInRole(TenantRoles.SysAdmin)) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            await next(context);
            return;
        }
        TenantResponse? tenant = await tenants.GetAsync(tenantId, context.RequestAborted);
        if (tenant is null || !tenant.IsActive)
        {
            await RejectOrRedirectAsync(context, "/not-found", StatusCodes.Status404NotFound); return;
        }
        if (anonymous)
        {
            if (!tenant.IsSetupComplete && !IsTenantSetupEndpoint(context) && !IsTenantLoginEndpoint(context)) { await RejectOrRedirectAsync(context, $"/{tenantId}/setup", StatusCodes.Status409Conflict); return; }
            await next(context);
            return;
        }
        if (context.User.Identity?.IsAuthenticated != true) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        if (!context.User.IsInRole(TenantRoles.SysAdmin))
        {
            string? assignedTenant = context.User.FindFirst(TenantClaimTypes.TenantId)?.Value;
            if (!string.Equals(assignedTenant, tenantId, StringComparison.OrdinalIgnoreCase)) { await RejectOrRedirectAsync(context, $"/{assignedTenant ?? tenantId}", StatusCodes.Status403Forbidden); return; }
        }
        if (!tenant.IsSetupComplete && !IsTenantSetupEndpoint(context)) { await RejectOrRedirectAsync(context, $"/{tenantId}/setup", StatusCodes.Status409Conflict); return; }
        await next(context);
    }

    private static bool IsInfrastructurePath(PathString path) 
        => path.StartsWithSegments("/_blazor") || 
        path.StartsWithSegments("/_framework") || 
        path.StartsWithSegments("/_content") || 
        path.StartsWithSegments("/favicon") || 
        path.StartsWithSegments("/css") || 
        path.StartsWithSegments("/js") || 
        path.StartsWithSegments("/lib");

    private static bool IsSystemSetupEndpoint(HttpContext context)
        => context.Request.Path.StartsWithSegments("/api/v1/setup") || 
        string.Equals(context.Request.Path.Value, "/setup",
        StringComparison.OrdinalIgnoreCase);

    private static bool IsTenantLoginEndpoint(HttpContext context) 
        => string.Equals(context.Request.Path.Value, $"/api/v1/{
            context.Request.RouteValues["tenantId"]}/login",
             StringComparison.OrdinalIgnoreCase);

    private static bool IsTenantSetupEndpoint(HttpContext context)
    {
        string? tenantId = context
            .Request
            .RouteValues
            .TryGetValue("tenantId", out object? value) ?
            value?.ToString() 
            : null;
        if (string.IsNullOrWhiteSpace(tenantId ) || 
            string.IsNullOrEmpty(tenantId) || 
            Guid.TryParse(tenantId, out Guid _)) { return false; }
        string? path = context.Request.Path.Value;
        return string.Equals(path,
            $"/{tenantId}/setup", 
            StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, 
            $"/api/v1/{tenantId}/setup", 
            StringComparison.OrdinalIgnoreCase) || string.Equals(path, 
            $"/api/v1/{tenantId}/setup/complete",
            StringComparison.OrdinalIgnoreCase);
    }
    private static async Task RejectOrRedirectAsync(HttpContext context, string location, int statusCode)
    {
        if (HttpMethods.IsGet(context.Request.Method) &&
             context.Request.Headers.Accept.Any(
                x => x != null && x.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
        { context.Response.Redirect(location); return; }
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            { 
                Title = "Setup required",
                Detail = $"Complete the required setup at {location} before using this endpoint." 
            },
            context.RequestAborted);
    }
}
