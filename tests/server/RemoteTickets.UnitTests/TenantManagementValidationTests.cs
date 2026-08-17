namespace RemoteTickets.UnitTests;

/// <summary>Verifies validation performed before tenant database provisioning.</summary>
public sealed class TenantManagementValidationTests
{
    [Fact]
    public async Task Tenant_provisioning_should_reject_missing_id_database_and_connection_string()
    {
        var service = new TenantManagementService(null!, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantManagementService>.Instance);

        await FluentActions.Awaiting(() => service.CreateAsync(new CreateTenantRequest(string.Empty, "database", "Server=.", "admin@example.com", "Password1!"), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => service.CreateAsync(new CreateTenantRequest("tenant", string.Empty, "Server=.", "admin@example.com", "Password1!"), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => service.CreateAsync(new CreateTenantRequest("tenant", "database", string.Empty, "admin@example.com", "Password1!"), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
    }
}
