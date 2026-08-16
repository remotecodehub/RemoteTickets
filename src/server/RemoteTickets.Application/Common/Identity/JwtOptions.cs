namespace RemoteTickets.Application.Common.Identity;

/// <summary>Defines configuration options used by the application JWT implementation.</summary>
public sealed class JwtOptions
{
    /// <summary>Gets the configuration section name containing these options.</summary>
    public const string SectionName = "Authentication:Jwt";

    /// <summary>Gets or sets the secret key used to sign JWTs.</summary>
    public required string SecretKey { get; set; }

    /// <summary>Gets or sets the JWT issuer.</summary>
    public string Issuer { get; set; } = "RemoteTickets";

    /// <summary>Gets or sets the JWT audience.</summary>
    public string Audience { get; set; } = "RemoteTickets";

    /// <summary>Gets or sets the lifetime of an access token.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets or sets the lifetime of a refresh token.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
}
