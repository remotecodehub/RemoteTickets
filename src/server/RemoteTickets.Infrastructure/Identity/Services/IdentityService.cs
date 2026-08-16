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
public sealed class IdentityService(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    SignInManager<User> signInManager,
    IJwtTokenService tokenService,
    IRevokedTokenStore revokedTokenStore,
    IIdentityEmailSender emailSender,
    ILogger<IdentityService> logger) : IIdentityService
{
    private const string AdministratorRole = "Administrator";
    private const string AdministratorPermission = "system.admin";

    /// <summary>
    /// Registers a new user and sends an email confirmation link.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="password">The password to assign to the user.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The result of the registration operation.</returns>
    public async Task<IdentityResultResponse> RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = new User(email)
        {
            Email = email,
            DisplayName = "",
            FirstName = "",
            SurName = "",
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return Failure(result);
        }

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendConfirmationAsync(
            email,
            $"/confirmEmail?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(code)}",
            cancellationToken);

        return IdentityResultResponse.Success();
    }

    /// <summary>
    /// Authenticates a user and issues JWT access and refresh tokens when all required authentication factors are valid.
    /// </summary>
    /// <param name="email">The user's email address.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="twoFactorCode">The authenticator application code, when two-factor authentication is enabled.</param>
    /// <param name="twoFactorRecoveryCode">The recovery code, when two-factor authentication is enabled and a recovery code is used.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The issued tokens, or <see langword="null"/> when authentication fails.</returns>
    public async Task<TokenResponse?> LoginAsync(
        string email,
        string password,
        string? twoFactorCode,
        string? twoFactorRecoveryCode,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        var passwordResult = await signInManager.CheckPasswordSignInAsync(user, password, true);
        if (passwordResult.IsLockedOut || passwordResult.IsNotAllowed || !passwordResult.Succeeded)
        {
            return null;
        }

        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            var valid = !string.IsNullOrWhiteSpace(twoFactorCode)
                ? await userManager.VerifyTwoFactorTokenAsync(
                    user,
                    userManager.Options.Tokens.AuthenticatorTokenProvider,
                    twoFactorCode)
                : !string.IsNullOrWhiteSpace(twoFactorRecoveryCode) &&
                  (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, twoFactorRecoveryCode)).Succeeded;

            if (!valid)
            {
                return null;
            }
        }

        return await CreateUserTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access and refresh token pair and revokes the previous refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to exchange.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The new token pair, or <see langword="null"/> when the refresh token is invalid.</returns>
    public async Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var principal = tokenService.ValidateToken(refreshToken);
        if (principal is null || !string.Equals(principal.FindFirstValue("token_type"), JwtTokenTypes.Refresh, StringComparison.Ordinal))
        {
            return null;
        }

        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var tokenId = tokenService.GetTokenId(refreshToken);
        var expiration = tokenService.GetExpiration(refreshToken);
        var tokens = await CreateUserTokensAsync(user, cancellationToken);

        if (!string.IsNullOrWhiteSpace(tokenId) && expiration.HasValue)
        {
            revokedTokenStore.Revoke(tokenId, expiration.Value);
        }

        return tokens;
    }

    /// <summary>
    /// Revokes a valid access token until its natural expiration.
    /// </summary>
    /// <param name="accessToken">The access token to revoke.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the token was revoked; otherwise, <see langword="false"/>.</returns>
    public Task<bool> RevokeAsync(string accessToken, CancellationToken cancellationToken)
    {
        var principal = tokenService.ValidateToken(accessToken);
        var tokenId = tokenService.GetTokenId(accessToken);
        var expiration = tokenService.GetExpiration(accessToken);

        if (principal is null || string.IsNullOrWhiteSpace(tokenId) || !expiration.HasValue)
        {
            return Task.FromResult(false);
        }

        revokedTokenStore.Revoke(tokenId, expiration.Value);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Confirms a user's email address or confirms a changed email address using a supplied Identity token.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="code">The confirmation token.</param>
    /// <param name="changedEmail">The new email address when confirming an email change; otherwise <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when confirmation succeeds; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> ConfirmEmailAsync(string userId, string code, string? changedEmail, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        var result = !string.IsNullOrWhiteSpace(changedEmail)
            ? await userManager.ChangeEmailAsync(user, changedEmail, code)
            : await userManager.ConfirmEmailAsync(user, code);

        return result.Succeeded;
    }

    /// <summary>
    /// Resends an email confirmation link when the specified user exists and remains unconfirmed.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The result of the resend operation.</returns>
    public async Task<IdentityResultResponse> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || await userManager.IsEmailConfirmedAsync(user))
        {
            return IdentityResultResponse.Success();
        }

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendConfirmationAsync(
            email,
            $"/confirmEmail?userId={Uri.EscapeDataString(user.Id)}&code={Uri.EscapeDataString(code)}",
            cancellationToken);

        return IdentityResultResponse.Success();
    }

    /// <summary>
    /// Starts a password recovery operation for a user with a password.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A successful result whether or not a matching password-bearing user exists.</returns>
    public async Task<IdentityResultResponse> ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.HasPasswordAsync(user))
        {
            return IdentityResultResponse.Success();
        }

        var code = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailSender.SendPasswordResetAsync(
            email,
            $"/resetPassword?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(code)}",
            cancellationToken);

        return IdentityResultResponse.Success();
    }

    /// <summary>
    /// Resets a user's password using a valid password-reset token.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="resetCode">The password-reset token.</param>
    /// <param name="newPassword">The new password.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The result of the password reset operation.</returns>
    public async Task<IdentityResultResponse> ResetPasswordAsync(string email, string resetCode, string newPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return IdentityResultResponse.Failure(["Invalid password reset request."]);
        }

        var result = await userManager.ResetPasswordAsync(user, resetCode, newPassword);
        return result.Succeeded ? IdentityResultResponse.Success() : Failure(result);
    }

    /// <summary>
    /// Gets basic identity information for a user.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The user's identity information, or <see langword="null"/> when the user does not exist.</returns>
    public async Task<IdentityInfoResponse?> GetInfoAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null
            ? null
            : new IdentityInfoResponse(user.Email ?? string.Empty, await userManager.IsEmailConfirmedAsync(user));
    }

    /// <summary>
    /// Updates a user's email address and password after validating the current password.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="newEmail">The new email address, or <see langword="null"/> to leave it unchanged.</param>
    /// <param name="newPassword">The new password, or <see langword="null"/> to leave it unchanged.</param>
    /// <param name="oldPassword">The current password.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The result of the update operation.</returns>
    public async Task<IdentityResultResponse> UpdateInfoAsync(string userId, string? newEmail, string? newPassword, string oldPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.CheckPasswordAsync(user, oldPassword))
        {
            return IdentityResultResponse.Failure(["The current credentials are invalid."]);
        }

        if (!string.IsNullOrWhiteSpace(newEmail) && !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailCode = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
            var emailResult = await userManager.ChangeEmailAsync(user, newEmail, emailCode);
            if (!emailResult.Succeeded)
            {
                return Failure(emailResult);
            }
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var passwordResult = await userManager.ChangePasswordAsync(user, oldPassword, newPassword);
            if (!passwordResult.Succeeded)
            {
                return Failure(passwordResult);
            }
        }

        return IdentityResultResponse.Success();
    }

    /// <summary>
    /// Configures authenticator-based two-factor authentication and optionally rotates its recovery material.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="enable">Whether to enable or disable two-factor authentication.</param>
    /// <param name="twoFactorCode">The authenticator code used to validate enabling two-factor authentication.</param>
    /// <param name="resetRecoveryCodes">Whether new recovery codes should be generated.</param>
    /// <param name="resetSharedKey">Whether the authenticator shared key should be regenerated.</param>
    /// <param name="forgetMachine">Whether the current machine should be forgotten; reserved for parity with the Identity API contract.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current two-factor configuration, or <see langword="null"/> when the user does not exist or an enable operation fails.</returns>
    public async Task<TwoFactorResponse?> ConfigureTwoFactorAsync(string userId, bool? enable, string? twoFactorCode, bool resetRecoveryCodes, bool resetSharedKey, bool forgetMachine, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

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

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var recoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user);
        return new TwoFactorResponse(key, recoveryCodesLeft, recoveryCodes, await userManager.GetTwoFactorEnabledAsync(user), false);
    }

    /// <summary>
    /// Gets whether initial system setup is required or has already been completed.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current setup status.</returns>
    public async Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken)
    {
        var hasUsers = await userManager.Users.AsNoTracking().AnyAsync(cancellationToken);
        return new SetupStatusResponse(!hasUsers, hasUsers);
    }

    /// <summary>
    /// Creates the initial administrator account when system setup has not yet been completed.
    /// </summary>
    /// <param name="email">The administrator email address.</param>
    /// <param name="password">The administrator password.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The result of the setup operation.</returns>
    public async Task<IdentityResultResponse> InitializeSetupAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (await userManager.Users.AsNoTracking().AnyAsync(cancellationToken))
        {
            return IdentityResultResponse.Failure(["The system setup has already been completed."]);
        }

        var role = await roleManager.FindByNameAsync(AdministratorRole);
        if (role is null)
        {
            role = new Role(AdministratorRole);
            var roleResult = await roleManager.CreateAsync(role);
            if (!roleResult.Succeeded)
            {
                return Failure(roleResult);
            }

            var claimResult = await roleManager.AddClaimAsync(role, new Claim(IdentityClaimTypes.Permission, AdministratorPermission));
            if (!claimResult.Succeeded)
            {
                return Failure(claimResult);
            }
        }

        var user = new User(email)
        {
            Email = email,
            EmailConfirmed = true
        };

        var userResult = await userManager.CreateAsync(user, password);
        if (!userResult.Succeeded)
        {
            return Failure(userResult);
        }

        var membershipResult = await userManager.AddToRoleAsync(user, AdministratorRole);
        if (!membershipResult.Succeeded)
        {
            return Failure(membershipResult);
        }

        logger.LogInformation("Initial system setup completed for user {UserId}.", user.Id);
        return IdentityResultResponse.Success();
    }

    private async Task<TokenResponse> CreateUserTokensAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var claims = await userManager.GetClaimsAsync(user);

        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            claims = claims.Concat(await roleManager.GetClaimsAsync(role)).ToList();
        }

        return tokenService.CreateTokens(user.Id, user.Email ?? user.UserName ?? user.Id, roles, claims);
    }

    private static IdentityResultResponse Failure(IdentityResult result) => IdentityResultResponse.Failure(result.Errors.Select(error => error.Description));
}

/// <summary>
/// Logs identity email messages instead of delivering them through an external email provider.
/// </summary>
/// <param name="logger">The logger used to record email messages.</param>
public sealed class LoggingIdentityEmailSender(ILogger<LoggingIdentityEmailSender> logger) : IIdentityEmailSender
{
    /// <summary>
    /// Logs an email confirmation message.
    /// </summary>
    /// <param name="email">The recipient email address.</param>
    /// <param name="confirmationLink">The confirmation link.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task SendConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken)
    {
        logger.LogInformation("Identity confirmation link for {Email}: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs a password reset message.
    /// </summary>
    /// <param name="email">The recipient email address.</param>
    /// <param name="resetLink">The password reset link.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken)
    {
        logger.LogInformation("Identity password reset link for {Email}: {Link}", email, resetLink);
        return Task.CompletedTask;
    }
}
