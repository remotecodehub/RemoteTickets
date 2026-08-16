namespace RemoteTickets.UnitTests;

/// <summary>Verifies authenticator-based two-factor configuration and login behavior.</summary>
public sealed class IdentityTwoFactorTests
{
    /// <summary>Verifies that two-factor authentication can be enabled, used during login, and disabled.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Two_factor_should_be_configurable_and_used_during_login()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);

        var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
        user.Should().NotBeNull();

        await fixture.UserManager.ResetAuthenticatorKeyAsync(user!);
        var key = await fixture.UserManager.GetAuthenticatorKeyAsync(user);
        key.Should().NotBeNullOrWhiteSpace();
        var code = CreateAuthenticatorCode(key!, DateTimeOffset.UtcNow);

        var enabled = await fixture.Service.ConfigureTwoFactorAsync(user!.Id, true, code, true, false, false, CancellationToken.None);
        enabled.Should().NotBeNull();
        enabled!.IsTwoFactorEnabled.Should().BeTrue();
        enabled.RecoveryCodes.Should().NotBeNullOrEmpty();

        var loginKey = await fixture.UserManager.GetAuthenticatorKeyAsync(user);
        var loginCode = CreateAuthenticatorCode(loginKey!, DateTimeOffset.UtcNow);
        var login = await fixture.Service.LoginAsync("admin@example.com", "Password1!", loginCode, null, CancellationToken.None);
        login.Should().NotBeNull();

        var disabled = await fixture.Service.ConfigureTwoFactorAsync(user.Id, false, null, false, true, false, CancellationToken.None);
        disabled.Should().NotBeNull();
        disabled!.IsTwoFactorEnabled.Should().BeFalse();
    }

    /// <summary>Verifies that two-factor configuration rejects unknown users and invalid authenticator codes.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Two_factor_should_reject_unknown_user_and_invalid_code()
    {
        await using var fixture = await IdentityFixture.CreateAsync();

        (await fixture.Service.ConfigureTwoFactorAsync("missing", true, "123456", false, false, false, CancellationToken.None)).Should().BeNull();

        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
        user.Should().NotBeNull();

        (await fixture.Service.ConfigureTwoFactorAsync(user!.Id, true, "123456", false, false, false, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>Verifies that recovery codes can be regenerated without enabling two-factor authentication.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Two_factor_should_generate_recovery_codes_when_requested()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
        user.Should().NotBeNull();

        var result = await fixture.Service.ConfigureTwoFactorAsync(user!.Id, null, null, true, false, true, CancellationToken.None);
        result.Should().NotBeNull();
        result!.RecoveryCodes.Should().NotBeNullOrEmpty();
        result.IsTwoFactorEnabled.Should().BeFalse();
    }

    private static string CreateAuthenticatorCode(string secret, DateTimeOffset timestamp)
    {
        var key = Base32Decode(secret);
        var counter = BitConverter.GetBytes(timestamp.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string value)
    {
        var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var buffer = 0;
        var bitsLeft = 0;
        var result = new List<byte>();

        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new FormatException("Invalid Base32 value.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft < 8)
            {
                continue;
            }

            bitsLeft -= 8;
            result.Add((byte)(buffer >> bitsLeft));
            buffer &= (1 << bitsLeft) - 1;
        }

        return result.ToArray();
    }
}
