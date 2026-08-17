namespace RemoteTickets.Infrastructure.Identity.Services;

/// <summary>Provides tenant-bound refresh-token handling for <see cref="IdentityService"/>.</summary>
public sealed partial class IdentityService
{
    /// <inheritdoc />
    public async Task<TokenResponse?> RefreshAsync(string tenantId, string refreshToken, CancellationToken cancellationToken)
    {
        ClaimsPrincipal? principal = tokenService.ValidateToken(refreshToken);
        if (principal is null || !string.Equals(principal.FindFirstValue("token_type"), JwtTokenTypes.Refresh, StringComparison.Ordinal))
        {
            return null;
        }

        string[] roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        if (!roles.Contains(TenantRoles.SysAdmin, StringComparer.OrdinalIgnoreCase) && !string.Equals(principal.FindFirst(TenantClaimTypes.TenantId)?.Value, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        User? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        IList<Claim> claims = await userManager.GetClaimsAsync(user);
        foreach (string? roleName in roles)
        {
            Role? role = await roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                claims = claims.Concat(await roleManager.GetClaimsAsync(role)).ToList();
            }
        }
        if (user.TenantId is not null)
        {
            claims = claims.Append(new Claim(TenantClaimTypes.TenantId, user.TenantId)).ToList();
        }

        string? tokenId = tokenService.GetTokenId(refreshToken);
        DateTimeOffset? expiration = tokenService.GetExpiration(refreshToken);
        TokenResponse tokens = tokenService.CreateTokens(user.Id, user.Email ?? user.UserName ?? user.Id, roles, claims);
        if (!string.IsNullOrWhiteSpace(tokenId) && expiration.HasValue)
        {
            revokedTokenStore.Revoke(tokenId, expiration.Value);
        }

        return tokens;
    }
}
