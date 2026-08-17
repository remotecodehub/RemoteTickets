namespace RemoteTickets.UnitTests;

/// <summary>
/// Covers identity service branch behavior that requires end-to-end Identity infrastructure.
/// </summary>
public sealed class IdentityBranchTests
{
    /// <summary>
    /// Verifies that duplicate registration returns identity validation errors.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Register_should_return_identity_errors_for_duplicate_email()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        (await fixture.Service.RegisterAsync("user@example.com", "Password1!", CancellationToken.None)).Succeeded.Should().BeTrue();

        var result = await fixture.Service.RegisterAsync("user@example.com", "Password1!", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies that invalid credentials and an unconfirmed account cannot obtain tokens.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Login_should_reject_bad_password_and_unconfirmed_user()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.RegisterAsync("user@example.com", "Password1!", CancellationToken.None);

        (await fixture.Service.LoginAsync("user@example.com", "WrongPassword1!", null, null, CancellationToken.None)).Should().BeNull();
        (await fixture.Service.LoginAsync("user@example.com", "Password1!", null, null, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that invalid authenticator and recovery codes cannot obtain tokens after two-factor authentication is enabled.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Login_should_reject_invalid_two_factor_code_and_recovery_code()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
        user.Should().NotBeNull();

        var twoFactorSetup = await fixture.Service.ConfigureTwoFactorAsync(
            user!.Id,
            null,
            null,
            false,
            true,
            false,
            CancellationToken.None);
        twoFactorSetup.Should().NotBeNull();
        twoFactorSetup!.SharedKey.Should().NotBeNullOrWhiteSpace();

        // AuthenticatorTokenProvider intentionally does not generate codes; the client authenticator generates TOTP from SharedKey.
        string setupCode = GenerateTotp(twoFactorSetup.SharedKey!);
        setupCode.Should().NotBeNullOrWhiteSpace();

        var configured = await fixture.Service.ConfigureTwoFactorAsync(
            user.Id,
            true,
            setupCode,
            true,
            false,
            false,
            CancellationToken.None);
        configured.Should().NotBeNull();
        configured!.IsTwoFactorEnabled.Should().BeTrue();

        (await fixture.Service.LoginAsync("admin@example.com", "Password1!", "000000", null, CancellationToken.None)).Should().BeNull();
        (await fixture.Service.LoginAsync("admin@example.com", "Password1!", null, "invalid-recovery-code", CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that refresh rejects access tokens, tokens without a subject, and tokens for unknown users.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Refresh_should_reject_access_tokens_empty_subjects_and_unknown_users()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
        user.Should().NotBeNull();
        var access = await fixture.Service.LoginAsync("admin@example.com", "Password1!", null, null, CancellationToken.None);
        access.Should().NotBeNull();
        (await fixture.Service.RefreshAsync(access!.AccessToken, CancellationToken.None)).Should().BeNull();

        string emptySubject = fixture.TokenService.CreateTokens(string.Empty, "empty@example.com", [], []).RefreshToken;
        (await fixture.Service.RefreshAsync(emptySubject, CancellationToken.None)).Should().BeNull();

        string unknownUser = fixture.TokenService.CreateTokens("missing-user", "missing@example.com", [], []).RefreshToken;
        (await fixture.Service.RefreshAsync(unknownUser, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// Verifies that email confirmation supports an email change and rejects an invalid confirmation code.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Confirm_email_should_support_changed_email_and_reject_invalid_code()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.RegisterAsync("old@example.com", "Password1!", CancellationToken.None);
        string link = fixture.EmailSender.ConfirmationLinks.Single();
        var query = new Uri("https://localhost" + link).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));

        var user = await fixture.UserManager.FindByIdAsync(query["userId"]);
        user.Should().NotBeNull();
        string changeCode = await fixture.UserManager.GenerateChangeEmailTokenAsync(user!, "new@example.com");
        (await fixture.Service.ConfirmEmailAsync(user!.Id, changeCode, "new@example.com", CancellationToken.None)).Should().BeTrue();
        (await fixture.Service.ConfirmEmailAsync(user.Id, "invalid", null, CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that password reset requests are silent for users without a password.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Forgot_password_should_not_send_for_a_user_without_password()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var user = new User("external@example.com") { Email = "external@example.com", EmailConfirmed = true };
        (await fixture.UserManager.CreateAsync(user)).Succeeded.Should().BeTrue();

        (await fixture.Service.ForgotPasswordAsync("external@example.com", CancellationToken.None)).Succeeded.Should().BeTrue();
        fixture.EmailSender.PasswordResetLinks.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that an invalid password reset token is reported for an existing user.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Reset_password_should_report_invalid_token_for_existing_user()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);

        var result = await fixture.Service.ResetPasswordAsync("admin@example.com", "invalid", "Password2!", CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies missing-user, invalid-password, and no-change update paths.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Update_info_should_cover_missing_user_wrong_password_and_no_changes()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
        user.Should().NotBeNull();

        (await fixture.Service.UpdateInfoAsync("missing", null, null, "Password1!", CancellationToken.None)).Succeeded.Should().BeFalse();
        (await fixture.Service.UpdateInfoAsync(user!.Id, null, null, "wrong", CancellationToken.None)).Succeeded.Should().BeFalse();
        (await fixture.Service.UpdateInfoAsync(user.Id, user.Email, null, "Password1!", CancellationToken.None)).Succeeded.Should().BeTrue();
    }

    /// <summary>
    /// Verifies setup behavior when the administrator role already exists and when the password is invalid.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Setup_should_accept_an_existing_administrator_role_and_reject_invalid_password()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var role = new Role("Administrator");
        (await fixture.RoleManager.CreateAsync(role)).Succeeded.Should().BeTrue();

        var invalid = await fixture.Service.InitializeSetupAsync("admin@example.com", "weak", CancellationToken.None);
        invalid.Succeeded.Should().BeFalse();

        var valid = await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        valid.Succeeded.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the logging email sender completes confirmation and password-reset messages.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Logging_email_sender_should_complete_both_messages()
    {
        using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var sender = new LoggingIdentityEmailSender(provider.GetRequiredService<ILogger<LoggingIdentityEmailSender>>());

        await sender.SendConfirmationAsync("user@example.com", "/confirm", CancellationToken.None);
        await sender.SendPasswordResetAsync("user@example.com", "/reset", CancellationToken.None);
    }

    /// <summary>
    /// Generates the current RFC 6238-compatible TOTP from an Identity authenticator shared key.
    /// </summary>
    /// <param name="sharedKey">The Base32-encoded authenticator shared key.</param>
    /// <returns>A six-digit TOTP code.</returns>
    /// <exception cref="ArgumentException">Thrown when the shared key is null or whitespace.</exception>
    private static string GenerateTotp(string sharedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);

        byte[] key = DecodeBase32(sharedKey);
        byte[] counter = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30));
        using var hmac = new HMACSHA1(key);
        byte[] hash = hmac.ComputeHash(counter);
        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Decodes a Base32-encoded authenticator secret.
    /// </summary>
    /// <param name="value">The Base32-encoded value.</param>
    /// <returns>The decoded secret bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    private static byte[] DecodeBase32(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        int buffer = 0;
        int bits = 0;
        var bytes = new List<byte>();

        foreach (char character in value.TrimEnd('=').ToUpperInvariant())
        {
            int index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new ArgumentException("The authenticator shared key is not valid Base32.", nameof(value));
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits < 8)
            {
                continue;
            }

            bits -= 8;
            bytes.Add((byte)(buffer >> bits));
            buffer &= (1 << bits) - 1;
        }

        return [.. bytes];
    }
}