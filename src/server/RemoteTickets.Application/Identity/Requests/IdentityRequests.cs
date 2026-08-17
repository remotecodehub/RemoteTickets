namespace RemoteTickets.Application.Identity.Requests;

/// <summary>Requests registration of a new user.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password.</param>
public sealed record RegisterCommand(string Email, string Password) : IRequest;

/// <summary>Requests authentication using a password and optional second factor.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's password.</param>
/// <param name="TwoFactorCode">The authenticator code, when required.</param>
/// <param name="TwoFactorRecoveryCode">The recovery code, when used instead of an authenticator code.</param>
/// <param name="TenantId">The tenant route identifier. When supplied, the identity must belong to this tenant or be a system administrator.</param>
public sealed record LoginCommand(string Email, string Password, string? TwoFactorCode = null, string? TwoFactorRecoveryCode = null, string? TenantId = null) : IRequest;

/// <summary>Requests exchange of a refresh token.</summary>
/// <param name="RefreshToken">The refresh token to exchange.</param>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest;

/// <summary>Requests revocation of an access token.</summary>
/// <param name="AccessToken">The access token to revoke.</param>
public sealed record RevokeTokenCommand(string AccessToken) : IRequest;

/// <summary>Requests confirmation of a user's email address.</summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Code">The confirmation token.</param>
/// <param name="ChangedEmail">The replacement email address when confirming an email change.</param>
public sealed record ConfirmEmailCommand(string UserId, string Code, string? ChangedEmail = null) : IRequest;

/// <summary>Requests that an email confirmation link be sent again.</summary>
/// <param name="Email">The user's email address.</param>
public sealed record ResendConfirmationEmailCommand(string Email) : IRequest;

/// <summary>Starts password recovery for an email address.</summary>
/// <param name="Email">The email address to recover.</param>
public sealed record ForgotPasswordCommand(string Email) : IRequest;

/// <summary>Requests a password reset.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="ResetCode">The password reset token.</param>
/// <param name="NewPassword">The replacement password.</param>
public sealed record ResetPasswordCommand(string Email, string ResetCode, string NewPassword) : IRequest;

/// <summary>Requests basic identity information for a user.</summary>
/// <param name="UserId">The user identifier.</param>
public sealed record GetIdentityInfoQuery(string UserId) : IRequest;

/// <summary>Requests an update to identity information.</summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="NewEmail">The replacement email address, or <see langword="null"/> to keep the current address.</param>
/// <param name="NewPassword">The replacement password, or <see langword="null"/> to keep the current password.</param>
/// <param name="OldPassword">The current password used to authorize the change.</param>
public sealed record UpdateIdentityInfoCommand(string UserId, string? NewEmail, string? NewPassword, string OldPassword) : IRequest;

/// <summary>Requests a change to authenticator-based two-factor authentication.</summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Enable">Whether to enable or disable two-factor authentication.</param>
/// <param name="TwoFactorCode">The authenticator code used when enabling two-factor authentication.</param>
/// <param name="ResetRecoveryCodes">Whether recovery codes should be regenerated.</param>
/// <param name="ResetSharedKey">Whether the shared authenticator key should be regenerated.</param>
/// <param name="ForgetMachine">Whether the remembered machine state should be cleared.</param>
public sealed record ConfigureTwoFactorCommand(string UserId, bool? Enable, string? TwoFactorCode, bool ResetRecoveryCodes, bool ResetSharedKey, bool ForgetMachine) : IRequest;

/// <summary>Requests the current first-time setup status.</summary>
public sealed record GetSetupStatusQuery : IRequest;

/// <summary>Requests creation of the initial administrator account.</summary>
/// <param name="Email">The administrator email address.</param>
/// <param name="Password">The administrator password.</param>
public sealed record InitializeSetupCommand(string Email, string Password) : IRequest;
