namespace RemoteTickets.UnitTests;

/// <summary>Verifies that identity handlers delegate requests to the identity application service.</summary>
public sealed class IdentityHandlerTests
{
    /// <summary>Verifies that every identity handler forwards its request and returns the service result.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task All_identity_handlers_should_delegate_to_identity_service()
    {
        var service = new FakeIdentityService();

        (await new IdentityHandlers(service).Handle(new ReceiveContext<RegisterCommand>(new("a@b.com", "Password1!")), CancellationToken.None)).Should().Be(IdentityResultResponse.Success());
        (await new IdentityHandlers(service).Handle(new ReceiveContext<LoginCommand>(new("a@b.com", "Password1!")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<RefreshTokenCommand>(new("refresh")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<RevokeTokenCommand>(new("access")), CancellationToken.None)).Data.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<ConfirmEmailCommand>(new("id", "code")), CancellationToken.None)).Data.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<ResendConfirmationEmailCommand>(new("a@b.com")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<ForgotPasswordCommand>(new("a@b.com")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<ResetPasswordCommand>(new("a@b.com", "code", "Password2!")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<GetIdentityInfoQuery>(new("id")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<UpdateIdentityInfoCommand>(new("id", null, null, "Password1!")), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<ConfigureTwoFactorCommand>(new("id", null, null, false, false, false)), CancellationToken.None)).Succeeded.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<GetSetupStatusQuery>(new()), CancellationToken.None)).IsSetupComplete.Should().BeTrue();
        (await new IdentityHandlers(service).Handle(new ReceiveContext<InitializeSetupCommand>(new("a@b.com", "Password1!")), CancellationToken.None)).Succeeded.Should().BeTrue();
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public Task<IdentityResultResponse> RegisterAsync(string email, string password, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TokenResponse?> LoginAsync(string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(new("Bearer", "access", 900, "refresh"));
        public Task<TokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(new("Bearer", "access", 900, "refresh"));
        public Task<bool> RevokeAsync(string accessToken, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> ConfirmEmailAsync(string userId, string code, string? changedEmail, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IdentityResultResponse> ResendConfirmationEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<IdentityResultResponse> ForgotPasswordAsync(string email, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<IdentityResultResponse> ResetPasswordAsync(string email, string resetCode, string newPassword, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<IdentityInfoResponse?> GetInfoAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IdentityInfoResponse?>(new("a@b.com", true));
        public Task<IdentityResultResponse> UpdateInfoAsync(string userId, string? newEmail, string? newPassword, string oldPassword, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TwoFactorResponse?> ConfigureTwoFactorAsync(string userId, bool? enable, string? twoFactorCode, bool resetRecoveryCodes, bool resetSharedKey, bool forgetMachine, CancellationToken cancellationToken) => Task.FromResult<TwoFactorResponse?>(new(null, 0, null, false, false));
        public Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new SetupStatusResponse(false, true));
        public Task<IdentityResultResponse> InitializeSetupAsync(string email, string password, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TokenResponse?> LoginAsync(string tenantId, string email, string password, string? twoFactorCode, string? twoFactorRecoveryCode, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(new("Bearer", "access", 900, "refresh"));
        public Task<IdentityResultResponse> CreateTenantAdminAsync(string tenantId, string email, string password, CancellationToken cancellationToken) => Task.FromResult(IdentityResultResponse.Success());
        public Task<TokenResponse?> RefreshAsync(string tenantId, string refreshToken, CancellationToken cancellationToken) => Task.FromResult<TokenResponse?>(new("Bearer", "access", 900, "refresh"));
    }
}
