namespace RemoteTickets.UnitTests;

/// <summary>Verifies tenant isolation and system-administrator cross-tenant access.</summary>
public sealed class TenantIdentityTests
{
    /// <summary>Verifies that a tenant user cannot authenticate against another tenant by changing only the route tenant identifier.</summary>
    [Fact]
    public async Task Tenant_user_should_not_login_to_another_tenant()
    {
        await using IdentityFixture fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("sysadmin@example.com", "Password1!", CancellationToken.None);
        IdentityResultResponse created = await fixture.Service.CreateTenantAdminAsync("tenant-a", "admin-a@example.com", "Password1!", CancellationToken.None);
        created.Succeeded.Should().BeTrue();

        TokenResponse? valid = await fixture.Service.LoginAsync("tenant-a", "admin-a@example.com", "Password1!", null, null, CancellationToken.None);
        valid.Should().NotBeNull();

        TokenResponse? wrongTenant = await fixture.Service.LoginAsync("tenant-b", "admin-a@example.com", "Password1!", null, null, CancellationToken.None);
        wrongTenant.Should().BeNull();
    }

    /// <summary>Verifies that a system administrator can authenticate through any tenant route.</summary>
    [Fact]
    public async Task Sysadmin_should_login_through_any_tenant_route()
    {
        await using IdentityFixture fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("sysadmin@example.com", "Password1!", CancellationToken.None);
        await fixture.Service.CreateTenantAdminAsync("tenant-a", "admin-a@example.com", "Password1!", CancellationToken.None);

        TokenResponse? tokens = await fixture.Service.LoginAsync("tenant-b", "sysadmin@example.com", "Password1!", null, null, CancellationToken.None);
        tokens.Should().NotBeNull();
        fixture.TokenService.ValidateToken(tokens!.AccessToken)!.IsInRole(TenantRoles.SysAdmin).Should().BeTrue();
    }

    /// <summary>Verifies that a refresh token cannot be replayed through another tenant route.</summary>
    [Fact]
    public async Task Refresh_token_should_remain_bound_to_its_tenant()
    {
        await using IdentityFixture fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("sysadmin@example.com", "Password1!", CancellationToken.None);
        await fixture.Service.CreateTenantAdminAsync("tenant-a", "admin-a@example.com", "Password1!", CancellationToken.None);
        TokenResponse? tokens = await fixture.Service.LoginAsync("tenant-a", "admin-a@example.com", "Password1!", null, null, CancellationToken.None);
        tokens.Should().NotBeNull();

        TokenResponse? invalid = await fixture.Service.RefreshAsync("tenant-b", tokens!.RefreshToken, CancellationToken.None);
        invalid.Should().BeNull();

        TokenResponse? refreshed = await fixture.Service.RefreshAsync("tenant-a", tokens.RefreshToken, CancellationToken.None);
        refreshed.Should().NotBeNull();
        fixture.TokenService.ValidateToken(refreshed!.AccessToken)!.FindFirst(TenantClaimTypes.TenantId)!.Value.Should().Be("tenant-a");
    }

    /// <summary>Verifies that a non-tenant user cannot authenticate through a tenant route and that tenant administrator validation rejects missing tenants.</summary>
    [Fact]
    public async Task Non_tenant_non_sysadmin_and_missing_tenant_admin_should_be_rejected()
    {
        await using IdentityFixture fixture = await IdentityFixture.CreateAsync();
        User user = new("operator@example.com") { Email = "operator@example.com", EmailConfirmed = true };
        (await fixture.UserManager.CreateAsync(user, "Password1!", TestContext.Current.CancellationToken)).Succeeded.Should().BeTrue();

        (await fixture.Service.LoginAsync("tenant-a", "operator@example.com", "Password1!", null, null, TestContext.Current.CancellationToken)).Should().BeNull();
        (await fixture.Service.CreateTenantAdminAsync(string.Empty, "admin@example.com", "Password1!", TestContext.Current.CancellationToken)).Succeeded.Should().BeFalse();
    }

    /// <summary>Verifies that duplicate tenant administrator emails are rejected after the role is provisioned.</summary>
    [Fact]
    public async Task Duplicate_tenant_admin_email_should_be_rejected()
    {
        await using IdentityFixture fixture = await IdentityFixture.CreateAsync();
        (await fixture.Service.CreateTenantAdminAsync("tenant-a", "admin@example.com", "Password1!", TestContext.Current.CancellationToken)).Succeeded.Should().BeTrue();
        IdentityResultResponse duplicate = await fixture.Service.CreateTenantAdminAsync("tenant-b", "admin@example.com", "Password1!", TestContext.Current.CancellationToken);

        duplicate.Succeeded.Should().BeFalse();
    }
}
