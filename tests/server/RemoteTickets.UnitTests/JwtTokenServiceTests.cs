namespace RemoteTickets.UnitTests;

/// <summary>Verifies JWT creation, validation, metadata, and revocation behavior.</summary>
public sealed class JwtTokenServiceTests
{
    /// <summary>Verifies that an undersized signing secret is rejected.</summary>
    [Fact]
    public void Token_service_should_reject_invalid_secret_length()
    {
        var store = new RevokedTokenStore();
        var service = new JwtTokenService(Options.Create(new JwtOptions { SecretKey = "short" }), store);

        var action = () => service.CreateTokens("user", "user@example.com", [], []);
        action.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Verifies that revoked token entries remain active until expiration and are then removed.</summary>
    [Fact]
    public void Revoked_token_store_should_expire_entries()
    {
        var store = new RevokedTokenStore();
        store.Revoke("token", DateTimeOffset.UtcNow.AddMinutes(1));
        store.IsRevoked("token").Should().BeTrue();
        store.Revoke("expired", DateTimeOffset.UtcNow.AddSeconds(-1));
        store.IsRevoked("expired").Should().BeFalse();
        store.IsRevoked("missing").Should().BeFalse();
    }

    /// <summary>Verifies that token claims and metadata survive a create-and-validate round trip.</summary>
    [Fact]
    public void Token_service_should_round_trip_claims_and_metadata()
    {
        var store = new RevokedTokenStore();
        var service = CreateService(store);
        var tokens = service.CreateTokens("user-id", "user@example.com", ["Administrator"], [new Claim(IdentityClaimTypes.Permission, "system.admin")]);

        var principal = service.ValidateToken(tokens.AccessToken);
        principal.Should().NotBeNull();
        principal!.FindFirstValue(ClaimTypes.Role).Should().Be("Administrator");
        principal.FindFirstValue(IdentityClaimTypes.Permission).Should().Be("system.admin");
        service.GetTokenId(tokens.AccessToken).Should().NotBeNullOrWhiteSpace();
        service.GetExpiration(tokens.AccessToken).Should().BeAfter(DateTimeOffset.UtcNow);
        service.ValidateToken("not-a-token").Should().BeNull();
        service.ValidateToken(string.Empty).Should().BeNull();
        service.GetTokenId("not-a-token").Should().BeNull();
        service.GetExpiration("not-a-token").Should().BeNull();
    }

    /// <summary>Verifies that a revoked access token cannot be validated.</summary>
    [Fact]
    public void Token_service_should_reject_a_revoked_token()
    {
        var store = new RevokedTokenStore();
        var service = CreateService(store);
        var tokens = service.CreateTokens("user-id", "user@example.com", [], []);
        string? tokenId = service.GetTokenId(tokens.AccessToken);
        tokenId.Should().NotBeNull();

        store.Revoke(tokenId!, DateTimeOffset.UtcNow.AddMinutes(1));

        service.ValidateToken(tokens.AccessToken).Should().BeNull();
    }

    private static JwtTokenService CreateService(RevokedTokenStore store) =>
        new(Options.Create(new JwtOptions
        {
            SecretKey = "RemoteTickets-test-secret-key-with-at-least-256-bits-2026",
            Issuer = "RemoteTickets",
            Audience = "RemoteTickets",
            AccessTokenLifetime = TimeSpan.FromMinutes(5),
            RefreshTokenLifetime = TimeSpan.FromHours(1)
        }), store);
}
