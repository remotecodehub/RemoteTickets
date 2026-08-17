namespace RemoteTickets.Controllers;

/// <summary>Exposes HTTP endpoints for tenant-scoped registration, authentication, account management, and two-factor authentication.</summary>
/// <param name="mediator">The mediator used to dispatch application requests.</param>
[ApiController]
[Route("/api/v1/{tenantId}")]
public sealed class IdentityController(IMediator mediator) : ControllerBase
{
    /// <summary>Registers a new user in the requested tenant.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromRoute] string tenantId, RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<RegisterCommand, IdentityResultResponse>(new RegisterCommand(request.Email, request.Password));
        return result.Succeeded ? Ok() : BadRequest(result);
    }

    /// <summary>Authenticates a user against the requested tenant.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromRoute] string tenantId, LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<LoginCommand, Response<TokenResponse>>(new LoginCommand(request.Email, request.Password, request.TwoFactorCode, request.TwoFactorRecoveryCode, tenantId), cancellationToken);
        return result.Succeeded ? Ok(result.Data) : Unauthorized(result);
    }

    /// <summary>Exchanges a refresh token for a new token pair in the requested tenant context.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<RefreshTokenCommand, Response<TokenResponse>>(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        return result.Succeeded ? Ok(result.Data) : Unauthorized(result);
    }

    /// <summary>Revokes the access token supplied by the authenticated caller.</summary>
    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        var accessToken = Request.Headers.Authorization.ToString().Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);
        var result = await mediator.RequestAsync<RevokeTokenCommand, Response<bool>>(new RevokeTokenCommand(accessToken), cancellationToken);
        return result.Data == true ? Ok() : Unauthorized(result);
    }

    /// <summary>Confirms a user's email address.</summary>
    [HttpGet("confirmEmail")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<ConfirmEmailCommand, Response<bool>>(new ConfirmEmailCommand(userId, code, changedEmail), cancellationToken);
        return result.Data == true ? Ok("Thank you for confirming your email.") : BadRequest(result);
    }

    /// <summary>Resends a user's email confirmation link.</summary>
    [HttpPost("resendConfirmationEmail")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendConfirmationEmail(EmailRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<ResendConfirmationEmailCommand, IdentityResultResponse>(new ResendConfirmationEmailCommand(request.Email), cancellationToken);
        return result.Succeeded ? Ok() : BadRequest(result);
    }

    /// <summary>Starts password recovery for an email address.</summary>
    [HttpPost("forgotPassword")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<ForgotPasswordCommand, IdentityResultResponse>(new ForgotPasswordCommand(request.Email), cancellationToken);
        return result.Succeeded ? Ok() : BadRequest(result);
    }

    /// <summary>Resets a user's password using a reset token.</summary>
    [HttpPost("resetPassword")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.RequestAsync<ResetPasswordCommand, IdentityResultResponse>(new ResetPasswordCommand(request.Email, request.ResetCode, request.NewPassword), cancellationToken);
        return result.Succeeded ? Ok() : BadRequest(result);
    }

    /// <summary>Gets identity information for the authenticated user.</summary>
    [HttpGet("manage/info")]
    [Authorize]
    public async Task<IActionResult> GetInfo(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await mediator.RequestAsync<GetIdentityInfoQuery, Response<IdentityInfoResponse>>(new GetIdentityInfoQuery(userId), cancellationToken);
        return result.Succeeded ? Ok(result.Data) : NotFound(result);
    }

    /// <summary>Updates identity information for the authenticated user.</summary>
    [HttpPost("manage/info")]
    [Authorize]
    public async Task<IActionResult> UpdateInfo(InfoRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await mediator.RequestAsync<UpdateIdentityInfoCommand, IdentityResultResponse>(new UpdateIdentityInfoCommand(userId, request.NewEmail, request.NewPassword, request.OldPassword), cancellationToken);
        return result.Succeeded ? Ok() : BadRequest(result);
    }

    /// <summary>Configures authenticator-based two-factor authentication for the authenticated user.</summary>
    [HttpPost("manage/2fa")]
    [Authorize]
    public async Task<IActionResult> ConfigureTwoFactor(TwoFactorRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await mediator.RequestAsync<ConfigureTwoFactorCommand, Response<TwoFactorResponse>>(new ConfigureTwoFactorCommand(userId, request.Enable, request.TwoFactorCode, request.ResetRecoveryCodes, request.ResetSharedKey, request.ForgetMachine), cancellationToken);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result);
    }

    private string? GetUserId() => User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}

/// <summary>Represents the registration request payload.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password.</param>
public sealed record RegisterRequest(string Email, string Password);
/// <summary>Represents the login request payload.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password.</param>
/// <param name="TwoFactorCode">The authenticator code, when required.</param>
/// <param name="TwoFactorRecoveryCode">The recovery code, when used instead of an authenticator code.</param>
public sealed record LoginRequest(string Email, string Password, string? TwoFactorCode = null, string? TwoFactorRecoveryCode = null);
/// <summary>Represents a refresh-token request payload.</summary>
/// <param name="RefreshToken">The refresh token to exchange.</param>
public sealed record RefreshRequest(string RefreshToken);
/// <summary>Represents an email-only request payload.</summary>
/// <param name="Email">The email address.</param>
public sealed record EmailRequest(string Email);
/// <summary>Represents a password reset request payload.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="ResetCode">The password reset token.</param>
/// <param name="NewPassword">The replacement password.</param>
public sealed record ResetPasswordRequest(string Email, string ResetCode, string NewPassword);
/// <summary>Represents an authenticated identity information update payload.</summary>
/// <param name="NewEmail">The replacement email address.</param>
/// <param name="NewPassword">The replacement password.</param>
/// <param name="OldPassword">The replacement password.</param>
public sealed record InfoRequest(string? NewEmail, string? NewPassword, string OldPassword);
/// <summary>Represents an authenticator-based two-factor configuration request.</summary>
/// <param name="Enable">Whether to enable or disable two-factor authentication.</param>
/// <param name="TwoFactorCode">The authenticator code used when enabling two-factor authentication.</param>
/// <param name="ResetRecoveryCodes">Whether recovery codes should be regenerated.</param>
/// <param name="ResetSharedKey">Whether the shared authenticator key should be regenerated.</param>
/// <param name="ForgetMachine">Whether remembered-machine state should be cleared.</param>
public sealed record TwoFactorRequest(bool? Enable = null, string? TwoFactorCode = null, bool ResetRecoveryCodes = false, bool ResetSharedKey = false, bool ForgetMachine = false);
