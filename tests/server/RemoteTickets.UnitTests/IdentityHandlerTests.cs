namespace RemoteTickets.UnitTests;

/// <summary>Verifies that identity handlers delegate requests to the identity application service.</summary>
public sealed class IdentityHandlerTests
{
    [Fact]
    public async Task All_identity_handlers_should_delegate_to_identity_service()
    {
        var service = new FakeIdentityService();
        IdentityHandlers handlers = new(service);
        (await handlers.Handle(new ReceiveContext<RegisterCommand>(new("a@b.com", "Password1!")), CancellationToken.None)).Should().Be(IdentityResultResponse.Success());
        (await handlers.Handle(new ReceiveContext<LoginCommand>(new("a@b.com", "Password1!")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<RefreshTokenCommand>(new("refresh")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<RevokeTokenCommand>(new("access")), CancellationToken.None)).Data.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<ConfirmEmailCommand>(new("id", "code")), CancellationToken.None)).Data.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<ResendConfirmationEmailCommand>(new("a@b.com")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<ForgotPasswordCommand>(new("a@b.com")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<ResetPasswordCommand>(new("a@b.com", "code", "Password2!")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<GetIdentityInfoQuery>(new("id")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<UpdateIdentityInfoCommand>(new("id", null, null, "Password1!")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<ConfigureTwoFactorCommand>(new("id", null, null, false, false, false)), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<GetSetupStatusQuery>(new()), CancellationToken.None)).IsSetupComplete.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<InitializeSetupCommand>(new("a@b.com", "Password1!")), CancellationToken.None)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Identity_handlers_should_cover_tenant_and_failure_responses()
    {
        var service = new FakeIdentityService
        {
            ReturnNullTokens = true,
            ReturnNullIdentity = true,
            ReturnNullTwoFactor = true
        };
        IdentityHandlers handlers = new(service);

        (await handlers.Handle(new ReceiveContext<LoginCommand>(new("tenant-a", "a@b.com", "Password1!", null, null)), CancellationToken.None)).Succeeded.Should().BeFalse();
        (await handlers.Handle(new ReceiveContext<RefreshTokenCommand>(new("tenant-a", "refresh")), CancellationToken.None)).Succeeded.Should().BeFalse();
        (await handlers.Handle(new ReceiveContext<GetIdentityInfoQuery>(new("missing")), CancellationToken.None)).Succeeded.Should().BeFalse();
        (await handlers.Handle(new ReceiveContext<ConfigureTwoFactorCommand>(new("id", null, null, false, false, false)), CancellationToken.None)).Succeeded.Should().BeFalse();
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public bool ReturnNullTokens { get; init; }
        public bool ReturnNullIdentity { get; init; }
        public bool ReturnNullTwoFactor { get; init; }
        public Task<IdentityResultResponse> RegisterAsync(string email, string password, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TokenResponse?> LoginAsync(string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(ReturnNullTokens ? null : new("Bearer", "access", 900, "refresh"));
        public Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(ReturnNullTokens ? null : new("Bearer", "access", 900, "refresh"));
        public Task<bool> RevokeAsync(string accessToken, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ConfirmEmailAsync(string userId, string code, string? changedEmail, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IdentityResultResponse> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<IdentityResultResponse> ForgotPasswordAsync(string email, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<IdentityResultResponse> ResetPasswordAsync(string email, string resetCode, string newPassword, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<IdentityInfoResponse?> GetInfoAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IdentityInfoResponse?>(ReturnNullIdentity ? null : new("a@b.com", true));
        public Task<IdentityResultResponse> UpdateInfoAsync(string userId, string? newEmail, string? newPassword, string oldPassword, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TwoFactorResponse?> ConfigureTwoFactorAsync(string userId, bool? enable, string? twoFactorCode, bool resetRecoveryCodes, bool resetSharedKey, bool forgetMachine, CancellationToken cancellationToken) => Task.FromResult<TwoFactorResponse?>(ReturnNullTwoFactor ? null : new(null, 0, null, false, false));
        public Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new SetupStatusResponse(false, true));
        public Task<IdentityResultResponse> InitializeSetupAsync(string email, string password, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TokenResponse?> LoginAsync(string tenantId, string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(ReturnNullTokens ? null : new("Bearer", "access", 900, "refresh"));
        public Task<IdentityResultResponse> CreateTenantAdminAsync(string tenantId, string email, string password, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TokenResponse?> RefreshAsync(string tenantId, string refreshToken, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(ReturnNullTokens ? null : new("Bearer", "access", 900, "refresh"));
    }
}
