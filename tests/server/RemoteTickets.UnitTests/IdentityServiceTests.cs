namespace RemoteTickets.UnitTests;

/// <summary>Verifies end-to-end identity service workflows using the test Identity infrastructure.</summary>
public sealed class IdentityServiceTests
{
    /// <summary>Verifies initial setup, administrator claims, refresh-token rotation, and access-token revocation.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Setup_should_create_administrator_and_issue_revocable_tokens()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var service = fixture.Service;
        var before = await service.GetSetupStatusAsync(CancellationToken.None);
        before.IsSetupRequired.Should().BeTrue();
        before.IsSetupComplete.Should().BeFalse();
        var setup = await service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        setup.Succeeded.Should().BeTrue();
        var after = await service.GetSetupStatusAsync(CancellationToken.None);
        after.IsSetupRequired.Should().BeFalse();
        after.IsSetupComplete.Should().BeTrue();
        var duplicate = await service.InitializeSetupAsync("second@example.com", "Password1!", CancellationToken.None);
        duplicate.Succeeded.Should().BeFalse();
        var tokens = await service.LoginAsync("admin@example.com", "Password1!", null, null, CancellationToken.None);
        tokens.Should().NotBeNull();
        var principal = fixture.TokenService.ValidateToken(tokens!.AccessToken);
        principal.Should().NotBeNull();
        principal!.FindAll(ClaimTypes.Role).Select(x => x.Value).Should().Contain("Administrator");
        principal.FindAll(IdentityClaimTypes.Permission).Select(x => x.Value).Should().Contain("system.admin");
        var refreshed = await service.RefreshAsync(tokens.RefreshToken, CancellationToken.None);
        refreshed.Should().NotBeNull();
        fixture.TokenService.ValidateToken(tokens.RefreshToken).Should().BeNull();
        var revoked = await service.RevokeAsync(refreshed!.AccessToken, CancellationToken.None);
        revoked.Should().BeTrue();
        fixture.TokenService.ValidateToken(refreshed.AccessToken).Should().BeNull();
    }

    /// <summary>Verifies registration, email confirmation, password recovery, password reset, and subsequent login.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Registration_confirmation_and_password_reset_should_work()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var registration = await fixture.Service.RegisterAsync("user@example.com", "Password1!", CancellationToken.None);
        registration.Succeeded.Should().BeTrue();
        fixture.EmailSender.ConfirmationLinks.Should().ContainSingle();
        var confirmationQuery = ParseQuery(fixture.EmailSender.ConfirmationLinks.Single());
        var confirmed = await fixture.Service.ConfirmEmailAsync(confirmationQuery["userId"], confirmationQuery["code"], null, CancellationToken.None);
        confirmed.Should().BeTrue();
        var info = await fixture.Service.GetInfoAsync(confirmationQuery["userId"], CancellationToken.None);
        info.Should().NotBeNull();
        info!.IsEmailConfirmed.Should().BeTrue();
        var forgot = await fixture.Service.ForgotPasswordAsync("user@example.com", CancellationToken.None);
        forgot.Succeeded.Should().BeTrue();
        fixture.EmailSender.PasswordResetLinks.Should().ContainSingle();
        var resetQuery = ParseQuery(fixture.EmailSender.PasswordResetLinks.Single());
        var reset = await fixture.Service.ResetPasswordAsync("user@example.com", resetQuery["code"], "Password2!", CancellationToken.None);
        reset.Succeeded.Should().BeTrue();
        var login = await fixture.Service.LoginAsync("user@example.com", "Password2!", null, null, CancellationToken.None);
        login.Should().NotBeNull();
    }

    /// <summary>Verifies that email and password changes require the current password.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Update_info_should_change_email_and_password_after_current_password_validation()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("admin@example.com", "Password1!", CancellationToken.None);
        var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
        user.Should().NotBeNull();
        var result = await fixture.Service.UpdateInfoAsync(user!.Id, "new-admin@example.com", "Password2!", "Password1!", CancellationToken.None);
        result.Succeeded.Should().BeTrue();
        var login = await fixture.Service.LoginAsync("new-admin@example.com", "Password2!", null, null, CancellationToken.None);
        login.Should().NotBeNull();
    }

    private static Dictionary<string, string> ParseQuery(string uri)
    {
        var query = new Uri("https://localhost" + uri).Query.TrimStart('?');
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => part[0], part => Uri.UnescapeDataString(part[1]));
    }
}

internal sealed class IdentityFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private IdentityFixture(ServiceProvider provider, CapturingEmailSender emailSender)
    {
        _provider = provider;
        EmailSender = emailSender;
    }

    public IdentityService Service => _provider.GetRequiredService<IdentityService>();
    public IJwtTokenService TokenService => _provider.GetRequiredService<IJwtTokenService>();
    public UserManager<User> UserManager => _provider.GetRequiredService<UserManager<User>>();
    public RoleManager<Role> RoleManager => _provider.GetRequiredService<RoleManager<Role>>();
    public CapturingEmailSender EmailSender { get; }

    public static async Task<IdentityFixture> CreateAsync()
    {
        var emailSender = new CapturingEmailSender();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthentication(options => options.DefaultScheme = IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme);
        services.AddDbContext<RemoteTicketsDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<Role>()
            .AddSignInManager<SignInManager<User>>()
            .AddEntityFrameworkStores<RemoteTicketsDbContext>()
            .AddDefaultTokenProviders();
        services.Configure<JwtOptions>(options =>
        {
            options.SecretKey = "RemoteTickets-test-secret-key-with-at-least-256-bits-2026";
            options.Issuer = "RemoteTickets";
            options.Audience = "RemoteTickets";
            options.AccessTokenLifetime = TimeSpan.FromMinutes(15);
            options.RefreshTokenLifetime = TimeSpan.FromDays(14);
        });
        services.AddSingleton<IRevokedTokenStore, RevokedTokenStore>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IIdentityEmailSender>(emailSender);
        services.AddScoped<IdentityService>();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<RemoteTicketsDbContext>().Database.EnsureCreatedAsync();
        return new IdentityFixture(provider, emailSender);
    }

    public ValueTask DisposeAsync()
    {
        _provider.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class CapturingEmailSender : IIdentityEmailSender
{
    public List<string> ConfirmationLinks { get; } = [];
    public List<string> PasswordResetLinks { get; } = [];

    public Task SendConfirmationAsync(string email, string confirmationLink, CancellationToken cancellationToken)
    {
        ConfirmationLinks.Add(confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string resetLink, CancellationToken cancellationToken)
    {
        PasswordResetLinks.Add(resetLink);
        return Task.CompletedTask;
    }
}