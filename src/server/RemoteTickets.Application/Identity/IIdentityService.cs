namespace RemoteTickets.Application.Identity;

/// <summary>Provides application-level identity and authentication operations.</summary>
public interface IIdentityService
{
    /// <summary>Registers a user and starts email confirmation.</summary>
    Task<IdentityResultResponse> RegisterAsync(string email, string password, CancellationToken cancellationToken);
    /// <summary>Authenticates a user and issues JWT tokens when all required factors are valid.</summary>
    Task<TokenResponse?> LoginAsync(string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken);
    /// <summary>Authenticates a user against a tenant route and rejects users assigned to another tenant.</summary>
    Task<TokenResponse?> LoginAsync(string tenantId, string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken);
    /// <summary>Creates the first administrator account for a tenant in the central identity store.</summary>
    Task<IdentityResultResponse> CreateTenantAdminAsync(string tenantId, string email, string password, CancellationToken cancellationToken);
    /// <summary>Exchanges a valid refresh token for a new token pair.</summary>
    Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    /// <summary>Exchanges a refresh token while enforcing its tenant route binding.</summary>
    Task<TokenResponse?> RefreshAsync(string tenantId, string refreshToken, CancellationToken cancellationToken);
    /// <summary>Revokes an access token until its natural expiration.</summary>
    Task<bool> RevokeAsync(string accessToken, CancellationToken cancellationToken);
    /// <summary>Confirms a user's email address or a changed email address.</summary>
    Task<bool> ConfirmEmailAsync(string userId, string code, string? changedEmail, CancellationToken cancellationToken);
    /// <summary>Resends email confirmation when required.</summary>
    Task<IdentityResultResponse> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken);
    /// <summary>Starts password recovery for a password-bearing user.</summary>
    Task<IdentityResultResponse> ForgotPasswordAsync(string email, CancellationToken cancellationToken);
    /// <summary>Resets a user's password with a valid reset token.</summary>
    Task<IdentityResultResponse> ResetPasswordAsync(string email, string resetCode, string newPassword, CancellationToken cancellationToken);
    /// <summary>Gets basic identity information for a user.</summary>
    Task<IdentityInfoResponse?> GetInfoAsync(string userId, CancellationToken cancellationToken);
    /// <summary>Updates identity information after validating the current password.</summary>
    Task<IdentityResultResponse> UpdateInfoAsync(string userId, string? newEmail, string? newPassword, string oldPassword, CancellationToken cancellationToken);
    /// <summary>Configures authenticator-based two-factor authentication and recovery material.</summary>
    Task<TwoFactorResponse?> ConfigureTwoFactorAsync(string userId, bool? enable, string? twoFactorCode, bool resetRecoveryCodes, bool resetSharedKey, bool forgetMachine, CancellationToken cancellationToken);
    /// <summary>Gets the current first-time setup status.</summary>
    Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken);
    /// <summary>Creates the initial administrator account when setup has not yet completed.</summary>
    Task<IdentityResultResponse> InitializeSetupAsync(string email, string password, CancellationToken cancellationToken);
}
