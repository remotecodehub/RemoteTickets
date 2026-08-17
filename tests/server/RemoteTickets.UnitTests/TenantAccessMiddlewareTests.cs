namespace RemoteTickets.UnitTests;

/// <summary>Verifies tenant routing, setup gates, authentication, and system-administrator bypass behavior.</summary>
public sealed class TenantAccessMiddlewareTests
{
    [Fact]
    public async Task Infrastructure_paths_should_bypass_tenant_gate()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: false);
        var context = CreateContext("/_framework/blazor.web.js", "GET");
        bool called = false;
        var middleware = new TenantAccessMiddleware(_ => { called = true; return Task.CompletedTask; }, fixture.Tenants, fixture.Context);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Incomplete_system_setup_should_redirect_html_requests()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: false);
        var context = CreateContext("/anything", "GET");
        context.Request.Headers.Accept = "text/html";
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().Should().Be("/setup");
    }

    [Fact]
    public async Task Incomplete_system_setup_should_return_problem_details_for_api_requests()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: false);
        var context = CreateContext("/api/v1/foo", "POST");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task Setup_endpoint_should_remain_available_before_initialization()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: false);
        var context = CreateContext("/api/v1/setup/status", "GET");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Completed_setup_should_reject_system_setup_post()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/api/v1/setup/database", "POST");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Anonymous_request_without_tenant_should_continue()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/", "GET", anonymous: true);
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticated_request_without_tenant_should_be_rejected()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/api/v1/products", "GET");
        context.Request.Headers.Accept = "text/html";
        context.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().Should().Be("/setup");
    }

    [Fact]
    public async Task System_route_should_require_sysadmin()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/system", "GET");
        context.Request.RouteValues["tenantId"] = "system";
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Sysadmin_should_be_allowed_on_system_route()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/system", "GET");
        context.Request.RouteValues["tenantId"] = "system";
        context.User = Principal("sys-1", TenantRoles.SysAdmin, null);
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Missing_tenant_should_be_not_found()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/missing", "GET");
        context.Request.RouteValues["tenantId"] = "missing";
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Inactive_tenant_should_be_not_found()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        fixture.Tenants.Items["inactive"] = new TenantResponse("inactive", "Inactive", "db", false, true);
        var context = CreateContext("/inactive", "GET");
        context.Request.RouteValues["tenantId"] = "inactive";
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Anonymous_incomplete_tenant_should_redirect_to_tenant_setup()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, false);
        var context = CreateContext("/tenant-a/products", "GET", anonymous: true);
        context.Request.RouteValues["tenantId"] = "tenant-a";
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Anonymous_tenant_setup_and_login_endpoints_should_continue()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, false);

        var setupContext = CreateContext("/tenant-a/setup", "GET", anonymous: true);
        setupContext.Request.RouteValues["tenantId"] = "tenant-a";
        await CreateMiddleware(fixture, out _).InvokeAsync(setupContext);

        var loginContext = CreateContext("/api/v1/tenant-a/login", "POST", anonymous: true);
        loginContext.Request.RouteValues["tenantId"] = "tenant-a";
        await CreateMiddleware(fixture, out _).InvokeAsync(loginContext);

        setupContext.Response.StatusCode.Should().Be(200);
        loginContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Unauthenticated_tenant_request_should_return_unauthorized()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/tenant-a/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-a";
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Authenticated_user_from_another_tenant_should_be_forbidden()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/tenant-b/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-b";
        context.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Sysadmin_should_bypass_tenant_mismatch()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/tenant-b/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-b";
        context.User = Principal("sys-1", TenantRoles.SysAdmin, null);
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticated_user_should_be_redirected_to_tenant_setup_when_incomplete()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, false);
        var context = CreateContext("/tenant-a/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-a";
        context.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Tenant_setup_endpoint_should_continue_for_authenticated_user()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, false);
        var context = CreateContext("/api/v1/tenant-a/setup/complete", "POST");
        context.Request.RouteValues["tenantId"] = "tenant-a";
        context.User = Principal("user-1", TenantRoles.TenantAdmin, "tenant-a");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Matching_authenticated_user_should_continue_for_complete_tenant()
    {
        await using var fixture = await CreateFixtureAsync(setupComplete: true);
        var context = CreateContext("/tenant-a/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-a";
        context.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var middleware = CreateMiddleware(fixture, out bool called);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    private static TenantAccessMiddleware CreateMiddleware(Fixture fixture, out bool called)
    {
        called = false;
        return new TenantAccessMiddleware(_ => { called = true; return Task.CompletedTask; }, fixture.Tenants, fixture.Context);
    }

    private static DefaultHttpContext CreateContext(string path, string method, bool anonymous = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        if (anonymous)
        {
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "anonymous"));
        }
        return context;
    }

    private static ClaimsPrincipal Principal(string id, string role, string? tenantId)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Role, role)];
        if (tenantId is not null) claims.Add(new Claim(TenantClaimTypes.TenantId, tenantId));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static async Task<Fixture> CreateFixtureAsync(bool setupComplete)
    {
        DbContextOptions<RemoteTicketsDbContext> options = new DbContextOptionsBuilder<RemoteTicketsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var context = new RemoteTicketsDbContext(options);
        context.SystemSetup.Add(new SystemSetupState { Id = RemoteTicketsConstants.SystemSetupId, IsComplete = setupComplete });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Fixture(context, new FakeTenantManagementService());
    }

    private sealed class Fixture(RemoteTicketsDbContext context, FakeTenantManagementService tenants) : IAsyncDisposable
    {
        public RemoteTicketsDbContext Context { get; } = context;
        public FakeTenantManagementService Tenants { get; } = tenants;

        public ValueTask DisposeAsync()
        {
            Context.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTenantManagementService : ITenantManagementService
    {
        public Dictionary<string, TenantResponse> Items { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, true),
            ["tenant-b"] = new TenantResponse("tenant-b", "Tenant B", "db", true, true)
        };

        public Task<TenantResponse?> GetAsync(string tenantId, CancellationToken cancellationToken)
            => Task.FromResult(Items.GetValueOrDefault(tenantId));

        public Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<TenantSetupStatusResponse> GetSetupStatusAsync(string tenantId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<TenantSetupStatusResponse> CompleteSetupAsync(string tenantId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
