namespace RemoteTickets.Application.Common.Identity;

/// <summary>Creates, validates, and inspects application JWT tokens.</summary>
public interface IJwtTokenService
{
    /// <summary>Creates an access token and refresh token for a user.</summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="roles">The roles to include in the access token.</param>
    /// <param name="claims">Additional claims to include in the access token.</param>
    /// <returns>The issued token pair.</returns>
    TokenResponse CreateTokens(string userId, string email, IEnumerable<string> roles, IEnumerable<Claim> claims);

    /// <summary>Validates a JWT and returns its claims principal when valid and not revoked.</summary>
    /// <param name="token">The JWT to validate.</param>
    /// <param name="validateLifetime">Indicates whether token lifetime should be validated.</param>
    /// <returns>The validated principal, or <see langword="null"/> when validation fails.</returns>
    ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);

    /// <summary>Gets the JWT identifier without requiring the token to be valid.</summary>
    /// <param name="token">The encoded JWT.</param>
    /// <returns>The token identifier, or <see langword="null"/> when the token cannot be read.</returns>
    string? GetTokenId(string token);

    /// <summary>Gets the expiration timestamp encoded in a JWT.</summary>
    /// <param name="token">The encoded JWT.</param>
    /// <returns>The expiration timestamp, or <see langword="null"/> when it cannot be read.</returns>
    DateTimeOffset? GetExpiration(string token);
}

/// <summary>Stores and checks revoked JWT identifiers until their expiration.</summary>
public interface IRevokedTokenStore
{
    /// <summary>Determines whether a token identifier is currently revoked.</summary>
    /// <param name="tokenId">The JWT identifier to check.</param>
    /// <returns><see langword="true"/> when the token is revoked; otherwise <see langword="false"/>.</returns>
    bool IsRevoked(string tokenId);

    /// <summary>Revokes a token identifier until the supplied expiration time.</summary>
    /// <param name="tokenId">The JWT identifier to revoke.</param>
    /// <param name="expiresAt">The time after which the revocation may be discarded.</param>
    void Revoke(string tokenId, DateTimeOffset expiresAt);
}

/// <summary>Sends email messages required by identity workflows.</summary>
public interface IIdentityEmailSender
{
    /// <summary>Sends an email confirmation message.</summary>
    /// <param name="email">The recipient email address.</param>
    /// <param name="confirmationLink">The confirmation link to include in the message.</param>
    /// <param name="cancellationToken">The token used to cancel the send operation.</param>
    /// <returns>A task representing the send operation.</returns>
    Task SendConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken);

    /// <summary>Sends a password reset message.</summary>
    /// <param name="email">The recipient email address.</param>
    /// <param name="resetLink">The password reset link to include in the message.</param>
    /// <param name="cancellationToken">The token used to cancel the send operation.</param>
    /// <returns>A task representing the send operation.</returns>
    Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken);
}
