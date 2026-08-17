namespace RemoteTickets.Infrastructure.Identity.Services;

/// <summary>Contains tenant-aware identity operations implemented by <see cref="IdentityService"/>.</summary>
public sealed partial class IdentityService
{
    /// <inheritdoc />
    public async Task<TokenResponse?> LoginAsync(string tenantId, string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return null;
        var roles = await userManager.GetRolesAsync(user);
        var sysAdmin = roles.Contains(TenantRoles.SysAdmin, StringComparer.OrdinalIgnoreCase);
        if (!sysAdmin && !string.Equals(user.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)) return null;
        var passwordResult = await signInManager.CheckPasswordSignInAsync(user, password, true);
        if (passwordResult.IsLockedOut || passwordResult.IsNotAllowed || !passwordResult.Succeeded) return null;
        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            var valid = !string.IsNullOrWhiteSpace(twoFactorCode)
                ? await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, twoFactorCode)
                : !string.IsNullOrWhiteSpace(twoFactorRecoveryCode) && (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, twoFactorRecoveryCode)).Succeeded;
            if (!valid) return null;
        }
        var claims = await GetTokenClaimsAsync(roles);
        if (user.TenantId is not null) claims = claims.Append(new Claim(TenantClaimTypes.TenantId, user.TenantId)).ToList();
        return tokenService.CreateTokens(user.Id, user.Email ?? user.UserName ?? user.Id, roles, claims);
    }

    /// <inheritdoc />
    public async Task<IdentityResultResponse> CreateTenantAdminAsync(string tenantId, string email, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return IdentityResultResponse.Failure(["A tenant identifier is required."]);
        if (await userManager.FindByEmailAsync(email) is not null) return IdentityResultResponse.Failure(["The email address is already registered."]);
        var role = await roleManager.FindByNameAsync(TenantRoles.TenantAdmin);
        if (role is null)
        {
            var roleResult = await roleManager.CreateAsync(new Role(TenantRoles.TenantAdmin));
            if (!roleResult.Succeeded) return IdentityResultResponse.Failure(roleResult.Errors.Select(x => x.Description));
        }
        var user = new User(email) { Email = email, EmailConfirmed = true, TenantId = tenantId };
        var userResult = await userManager.CreateAsync(user, password);
        if (!userResult.Succeeded) return IdentityResultResponse.Failure(userResult.Errors.Select(x => x.Description));
        var membership = await userManager.AddToRoleAsync(user, TenantRoles.TenantAdmin);
        return membership.Succeeded ? IdentityResultResponse.Success() : IdentityResultResponse.Failure(membership.Errors.Select(x => x.Description));
    }

    /// <inheritdoc />
    public async Task<TokenResponse?> RefreshAsync(string tenantId, string refreshToken, CancellationToken cancellationToken)
    {
        var principal = tokenService.ValidateToken(refreshToken);
        if (principal is null || !string.Equals(principal.FindFirstValue("token_type"), JwtTokenTypes.Refresh, StringComparison.Ordinal)) return null;
        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        if (!roles.Contains(TenantRoles.SysAdmin, StringComparer.OrdinalIgnoreCase) && !string.Equals(principal.FindFirst(TenantClaimTypes.TenantId)?.Value, tenantId, StringComparison.OrdinalIgnoreCase)) return null;
        return await RefreshAsync(refreshToken, cancellationToken);
    }

    private async Task<List<Claim>> GetTokenClaimsAsync(IReadOnlyCollection<string> roles)
    {
        var claims = (await userManager.GetClaimsAsync(await userManager.FindByNameAsync(roles.FirstOrDefault() ?? string.Empty) ?? new User())).ToList();
        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is not null) claims.AddRange(await roleManager.GetClaimsAsync(role));
        }
        return claims;
    }
}
