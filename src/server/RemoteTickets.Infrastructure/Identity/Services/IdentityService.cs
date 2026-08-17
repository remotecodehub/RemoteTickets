namespace RemoteTickets.Infrastructure.Identity.Services;

/// <summary>
/// Provides identity operations for users, authentication, account recovery, and two-factor authentication.
/// </summary>
/// <param name="userManager">The user manager used to manage application users.</param>
/// <param name="roleManager">The role manager used to manage application roles.</param>
/// <param name="signInManager">The sign-in manager used to validate user passwords and lockout state.</param>
/// <param name="tokenService">The service used to create and validate JWT tokens.</param>
/// <param name="revokedTokenStore">The store used to track revoked tokens.</param>
/// <param name="emailSender">The service used to send identity-related email messages.</param>
/// <param name="logger">The logger used to record operational identity events.</param>
public sealed partial class IdentityService(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    SignInManager<User> signInManager,
    IJwtTokenService tokenService,
    IRevokedTokenStore revokedTokenStore,
    IIdentityEmailSender emailSender,
    ISetupConfigurationStore configurationStore,
    ILogger<IdentityService> logger) : IIdentityService
{
    private const string AdministratorRole = "Administrator";
    private const string AdministratorPermission = "system.admin";

    /// <summary>Registers a new user and sends an email confirmation link.</summary>
    public async Task<IdentityResultResponse> RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = new User(email) { Email = email, DisplayName = "", FirstName = "", SurName = "", EmailConfirmed = false };
        IdentityResult result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        string code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendConfirmationAsync(email, $"/confirmEmail?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(code)}", cancellationToken);
        return IdentityResultResponse.Success();
    }

    /// <summary>Authenticates a user and issues JWT access and refresh tokens when all required authentication factors are valid.</summary>
    public async Task<TokenResponse?> LoginAsync(string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        SignInResult passwordResult = await signInManager.CheckPasswordSignInAsync(user, password, true);
        if (passwordResult.IsLockedOut || passwordResult.IsNotAllowed || !passwordResult.Succeeded)
        {
            return null;
        }

        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            bool valid = !string.IsNullOrWhiteSpace(twoFactorCode)
                ? await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, twoFactorCode)
                : !string.IsNullOrWhiteSpace(twoFactorRecoveryCode) && (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, twoFactorRecoveryCode)).Succeeded;
            if (!valid)
            {
                return null;
            }
        }
        return await CreateUserTokensAsync(user, cancellationToken);
    }

    /// <summary>Exchanges a valid refresh token for a new access and refresh token pair and revokes the previous refresh token.</summary>
    public async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        ClaimsPrincipal? principal = tokenService.ValidateToken(refreshToken);
        if (principal is null || !string.Equals(principal.FindFirstValue("token_type"), JwtTokenTypes.Refresh, StringComparison.Ordinal))
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

        string? tokenId = tokenService.GetTokenId(refreshToken);
        DateTimeOffset? expiration = tokenService.GetExpiration(refreshToken);
        TokenResponse tokens = await CreateUserTokensAsync(user, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tokenId) && expiration.HasValue)
        {
            revokedTokenStore.Revoke(tokenId, expiration.Value);
        }

        return tokens;
    }

    /// <summary>Revokes a valid access token until its natural expiration.</summary>
    public Task<bool> RevokeAsync(string accessToken, CancellationToken cancellationToken)
    {
        ClaimsPrincipal? principal = tokenService.ValidateToken(accessToken);
        string? tokenId = tokenService.GetTokenId(accessToken);
        DateTimeOffset? expiration = tokenService.GetExpiration(accessToken);
        if (principal is null || string.IsNullOrWhiteSpace(tokenId) || !expiration.HasValue)
        {
            return Task.FromResult(false);
        }

        revokedTokenStore.Revoke(tokenId, expiration.Value);
        return Task.FromResult(true);
    }

    /// <summary>Confirms a user's email address or confirms a changed email address using a supplied Identity token.</summary>
    public async Task<bool> ConfirmEmailAsync(string userId, string code, string? changedEmail, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        IdentityResult result = !string.IsNullOrWhiteSpace(changedEmail) ? await userManager.ChangeEmailAsync(user, changedEmail, code) : await userManager.ConfirmEmailAsync(user, code);
        return result.Succeeded;
    }

    /// <summary>Resends an email confirmation link when the specified user exists and remains unconfirmed.</summary>
    public async Task<IdentityResultResponse> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByEmailAsync(email);
        if (user is null || await userManager.IsEmailConfirmedAsync(user))
        {
            return IdentityResultResponse.Success();
        }

        string code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendConfirmationAsync(email, $"/confirmEmail?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(code)}", cancellationToken);
        return IdentityResultResponse.Success();
    }

    /// <summary>Starts a password recovery operation for a user with a password.</summary>
    public async Task<IdentityResultResponse> ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.HasPasswordAsync(user))
        {
            return IdentityResultResponse.Success();
        }

        string code = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailSender.SendPasswordResetAsync(email, $"/resetPassword?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}", cancellationToken);
        return IdentityResultResponse.Success();
    }

    /// <summary>Resets a user's password using a valid password-reset token.</summary>
    public async Task<IdentityResultResponse> ResetPasswordAsync(string email, string resetCode, string newPassword, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return IdentityResultResponse.Failure(["Invalid password reset request."]);
        }

        IdentityResult result = await userManager.ResetPasswordAsync(user, resetCode, newPassword);
        return result.Succeeded ? IdentityResultResponse.Success() : Failure(result);
    }

    /// <summary>Gets basic identity information for a user.</summary>
    public async Task<IdentityInfoResponse?> GetInfoAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByIdAsync(userId);
        return user is null ? null : new IdentityInfoResponse(user.Email ?? string.Empty, await userManager.IsEmailConfirmedAsync(user));
    }

    /// <summary>Updates a user's email address and password after validating the current password.</summary>
    public async Task<IdentityResultResponse> UpdateInfoAsync(string userId, string? newEmail, string? newPassword, string oldPassword, CancellationToken cancellationToken)
    {
        User? user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.CheckPasswordAsync(user, oldPassword))
        {
            return IdentityResultResponse.Failure(["The current credentials are invalid."]);
        }

        if (!string.IsNullOrWhiteSpace(newEmail) && !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            IdentityResult emailResult = await userManager.ChangeEmailAsync(user, newEmail, await userManager.GenerateChangeEmailTokenAsync(user, newEmail));
            if (!emailResult.Succeeded)
            {
                return Failure(emailResult);
            }
        }
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            IdentityResult passwordResult = await userManager.ChangePasswordAsync(user, oldPassword, newPassword);
            if (!passwordResult.Succeeded)
            {
                return Failure(passwordResult);
            }
        }
        return IdentityResultResponse.Success();
    }

    /// <summary>Configures authenticator-based two-factor authentication and optionally rotates its recovery material.</summary>
    public async Task<TwoFactorResponse?> ConfigureTwoFactorAsync(string userId, bool? enable, string? twoFactorCode, bool resetRecoveryCodes, bool resetSharedKey, bool forgetMachine, CancellationToken cancellationToken)
    {
        User user = await userManager.FindByIdAsync(userId) ??  null!;

        if (user == null) { return null; }

        if (enable == true)
        {
            if (string.IsNullOrWhiteSpace(twoFactorCode) || !await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, twoFactorCode))
            {
                return null;
            }
            await userManager.SetTwoFactorEnabledAsync(user, true);
        }
        else if (enable == false || resetSharedKey)
        {
            await userManager.SetTwoFactorEnabledAsync(user, false);
        }

        if (resetSharedKey)
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
        }

        string[]? recoveryCodes = null;
        if (resetRecoveryCodes || (enable == true && await userManager.CountRecoveryCodesAsync(user) == 0))
        {
            recoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
        }

        string? key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key)) { await userManager.ResetAuthenticatorKeyAsync(user); key = await userManager.GetAuthenticatorKeyAsync(user); }
        return new TwoFactorResponse(key, await userManager.CountRecoveryCodesAsync(user), recoveryCodes, await userManager.GetTwoFactorEnabledAsync(user), false);
    }

    /// <summary>Gets whether initial system setup is required or has already been completed.</summary>
    public async Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken)
    {
        bool hasUsers = await userManager.Users.AsNoTracking().AnyAsync(cancellationToken);
        return new SetupStatusResponse(!hasUsers, hasUsers);
    }

    /// <summary>Creates the initial system administrator account when setup has not yet been completed.</summary>
    public async Task<IdentityResultResponse> InitializeSetupAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (await userManager.Users.AsNoTracking().AnyAsync(cancellationToken))
        {
            return IdentityResultResponse.Failure(["The system setup has already been completed."]);
        }

        await EnsureRoleAsync(TenantRoles.SysAdmin, cancellationToken);
        await EnsureRoleAsync(TenantRoles.TenantAdmin, cancellationToken);
        await EnsureRoleAsync(TenantRoles.TenantOperator, cancellationToken);
        await EnsureRoleAsync(TenantRoles.TenantAttendant, cancellationToken);
        Role? role = await roleManager.FindByNameAsync(AdministratorRole);
        if (role is null)
        {
            role = new Role(AdministratorRole);
            IdentityResult roleResult = await roleManager.CreateAsync(role);
            if (!roleResult.Succeeded)
            {
                return Failure(roleResult);
            }

            IdentityResult claimResult = await roleManager.AddClaimAsync(role, new Claim(IdentityClaimTypes.Permission, AdministratorPermission));
            if (!claimResult.Succeeded)
            {
                return Failure(claimResult);
            }
        }
        var user = new User(email) { Email = email, EmailConfirmed = true };
        IdentityResult userResult = await userManager.CreateAsync(user, password);
        if (!userResult.Succeeded)
        {
            return Failure(userResult);
        }

        IdentityResult membershipResult = await userManager.AddToRoleAsync(user, TenantRoles.SysAdmin);
        if (!membershipResult.Succeeded)
        {
            return Failure(membershipResult);
        }

        membershipResult = await userManager.AddToRoleAsync(user, AdministratorRole);
        if (!membershipResult.Succeeded)
        {
            return Failure(membershipResult);
        }

        string? connectionString = configurationStore.GetMasterConnectionString();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            DbContextOptions<RemoteTicketsDbContext> options = new DbContextOptionsBuilder<RemoteTicketsDbContext>().UseSqlServer(connectionString).Options;
            await using var dbContext = new RemoteTicketsDbContext(options);
            SystemSetupState? state = await dbContext.SystemSetup.SingleOrDefaultAsync(x => x.Id == RemoteTicketsConstants.SystemSetupId, cancellationToken);
            if (state is null)
            {
                state = new SystemSetupState { Id = RemoteTicketsConstants.SystemSetupId };
                dbContext.SystemSetup.Add(state);
            }
            state.IsComplete = true;
            state.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("Initial system setup completed for user {UserId}.", user.Id);
        return IdentityResultResponse.Success();
    }

    private async Task<TokenResponse> CreateUserTokensAsync(User user, CancellationToken cancellationToken)
    {
        IList<string> roles = await userManager.GetRolesAsync(user);
        IList<Claim> claims = await userManager.GetClaimsAsync(user);
        foreach (string roleName in roles)
        {
            Role? role = await roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                claims = claims.Concat(await roleManager.GetClaimsAsync(role)).ToList();
            }
        }
        return tokenService.CreateTokens(user.Id, user.Email ?? user.UserName ?? user.Id, roles, claims);
    }

    private async Task EnsureRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        IdentityResult result = await roleManager.CreateAsync(new Role(roleName));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
    }

    private static IdentityResultResponse Failure(IdentityResult result) => IdentityResultResponse.Failure(result.Errors.Select(error => error.Description));
}

/// <summary>Logs identity email messages instead of delivering them through an external email provider.</summary>
public sealed class LoggingIdentityEmailSender(ILogger<LoggingIdentityEmailSender> logger) : IIdentityEmailSender
{
    /// <summary>Logs an email confirmation message.</summary>
    public Task SendConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken)
    {
        logger.LogInformation("Identity confirmation link for {Email}: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    /// <summary>Logs a password reset message.</summary>
    public Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken)
    {
        logger.LogInformation("Identity password reset link for {Email}: {Link}", email, resetLink);
        return Task.CompletedTask;
    }
}
