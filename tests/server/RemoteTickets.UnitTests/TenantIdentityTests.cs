namespace RemoteTickets.UnitTests;

/// <summary>Verifies tenant isolation and system-administrator cross-tenant access.</summary>
public sealed class TenantIdentityTests
{
    /// <summary>Verifies that a tenant user cannot authenticate against another tenant by changing only the route tenant identifier.</summary>
    [Fact]
    public async Task Tenant_user_should_not_login_to_another_tenant()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("sysadmin@example.com", "Password1!", CancellationToken.None);
        var created = await fixture.Service.CreateTenantAdminAsync("tenant-a", "admin-a@example.com", "Password1!", CancellationToken.None);
        created.Succeeded.Should().BeTrue();

        var valid = await fixture.Service.LoginAsync("tenant-a", "admin-a@example.com", "Password1!", null, null, CancellationToken.None);
        valid.Should().NotBeNull();

        var wrongTenant = await fixture.Service.LoginAsync("tenant-b", "admin-a@example.com", "Password1!", null, null, CancellationToken.None);
        wrongTenant.Should().BeNull();
    }

    /// <summary>Verifies that a system administrator can authenticate through any tenant route.</summary>
    [Fact]
    public async Task Sysadmin_should_login_through_any_tenant_route()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("sysadmin@example.com", "Password1!", CancellationToken.None);
        await fixture.Service.CreateTenantAdminAsync("tenant-a", "admin-a@example.com", "Password1!", CancellationToken.None);

        var tokens = await fixture.Service.LoginAsync("tenant-b", "sysadmin@example.com", "Password1!", null, null, CancellationToken.None);
        tokens.Should().NotBeNull();
        fixture.TokenService.ValidateToken(tokens!.AccessToken)!.IsInRole(TenantRoles.SysAdmin).Should().BeTrue();
    }

    /// <summary>Verifies that a refresh token cannot be replayed through another tenant route.</summary>
    [Fact]
    public async Task Refresh_token_should_remain_bound_to_its_tenant()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        await fixture.Service.InitializeSetupAsync("sysadmin@example.com", "Password1!", CancellationToken.None);
        await fixture.Service.CreateTenantAdminAsync("tenant-a", "admin-a@example.com", "Password1!", CancellationToken.None);
        var tokens = await fixture.Service.LoginAsync("tenant-a", "admin-a@example.com", "Password1!", null, null, CancellationToken.None);
        tokens.Should().NotBeNull();

        var invalid = await fixture.Service.RefreshAsync("tenant-b", tokens!.RefreshToken, CancellationToken.None);
        invalid.Should().BeNull();

        var refreshed = await fixture.Service.RefreshAsync("tenant-a", tokens.RefreshToken, CancellationToken.None);
        refreshed.Should().NotBeNull();
        fixture.TokenService.ValidateToken(refreshed!.AccessToken)!.FindFirst(TenantClaimTypes.TenantId)!.Value.Should().Be("tenant-a");
    }
}
