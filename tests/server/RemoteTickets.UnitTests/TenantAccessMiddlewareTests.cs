namespace RemoteTickets.UnitTests;

/// <summary>Verifies tenant routing, setup gates, authentication, and system-administrator bypass behavior.</summary>
public sealed class TenantAccessMiddlewareTests
{
    [Fact]
    public async Task Infrastructure_paths_should_bypass_tenant_gate()
    {
        await using var fixture = await CreateFixtureAsync(false);
        var context = CreateContext("/_framework/blazor.web.js", "GET");
        var tracker = new InvocationTracker();
        var middleware = CreateMiddleware(fixture, tracker);

        await middleware.InvokeAsync(context);

        tracker.Called.Should().BeTrue();
    }

    [Fact]
    public async Task Incomplete_system_setup_should_redirect_html_and_reject_api_requests()
    {
        await using var fixture = await CreateFixtureAsync(false);
        var html = CreateContext("/anything", "GET");
        html.Request.Headers.Accept = "text/html";
        var htmlTracker = new InvocationTracker();
        await CreateMiddleware(fixture, htmlTracker).InvokeAsync(html);

        htmlTracker.Called.Should().BeFalse();
        html.Response.StatusCode.Should().Be(StatusCodes.Status302Found);
        html.Response.Headers.Location.ToString().Should().Be("/setup");

        var api = CreateContext("/api/v1/foo", "POST");
        var apiTracker = new InvocationTracker();
        await CreateMiddleware(fixture, apiTracker).InvokeAsync(api);

        apiTracker.Called.Should().BeFalse();
        api.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Setup_endpoint_should_remain_available_before_initialization()
    {
        await using var fixture = await CreateFixtureAsync(false);
        var context = CreateContext("/api/v1/setup/status", "GET");
        var tracker = new InvocationTracker();

        await CreateMiddleware(fixture, tracker).InvokeAsync(context);

        tracker.Called.Should().BeTrue();
    }

    [Fact]
    public async Task Completed_setup_should_reject_system_setup_post()
    {
        await using var fixture = await CreateFixtureAsync(true);
        var context = CreateContext("/api/v1/setup/database", "POST");
        var tracker = new InvocationTracker();

        await CreateMiddleware(fixture, tracker).InvokeAsync(context);

        tracker.Called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task System_route_should_require_sysadmin_and_allow_sysadmin()
    {
        await using var fixture = await CreateFixtureAsync(true);
        var forbidden = CreateContext("/system", "GET");
        forbidden.Request.RouteValues["tenantId"] = "system";
        var forbiddenTracker = new InvocationTracker();
        await CreateMiddleware(fixture, forbiddenTracker).InvokeAsync(forbidden);
        forbiddenTracker.Called.Should().BeFalse();
        forbidden.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var allowed = CreateContext("/system", "GET");
        allowed.Request.RouteValues["tenantId"] = "system";
        allowed.User = Principal("sys-1", TenantRoles.SysAdmin, null);
        var allowedTracker = new InvocationTracker();
        await CreateMiddleware(fixture, allowedTracker).InvokeAsync(allowed);
        allowedTracker.Called.Should().BeTrue();
    }

    [Fact]
    public async Task Missing_or_inactive_tenant_should_be_not_found()
    {
        await using var fixture = await CreateFixtureAsync(true);
        var missing = CreateContext("/missing", "GET");
        missing.Request.RouteValues["tenantId"] = "missing";
        var missingTracker = new InvocationTracker();
        await CreateMiddleware(fixture, missingTracker).InvokeAsync(missing);
        missingTracker.Called.Should().BeFalse();
        missing.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        fixture.Tenants.Items["inactive"] = new TenantResponse("inactive", "Inactive", "db", false, true);
        var inactive = CreateContext("/inactive", "GET");
        inactive.Request.RouteValues["tenantId"] = "inactive";
        var inactiveTracker = new InvocationTracker();
        await CreateMiddleware(fixture, inactiveTracker).InvokeAsync(inactive);
        inactiveTracker.Called.Should().BeFalse();
        inactive.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Anonymous_incomplete_tenant_should_be_restricted_except_setup_and_login()
    {
        await using var fixture = await CreateFixtureAsync(true);
        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, false);

        var restricted = CreateContext("/tenant-a/products", "GET", true);
        restricted.Request.RouteValues["tenantId"] = "tenant-a";
        var restrictedTracker = new InvocationTracker();
        await CreateMiddleware(fixture, restrictedTracker).InvokeAsync(restricted);
        restrictedTracker.Called.Should().BeFalse();
        restricted.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var setup = CreateContext("/tenant-a/setup", "GET", true);
        setup.Request.RouteValues["tenantId"] = "tenant-a";
        var setupTracker = new InvocationTracker();
        await CreateMiddleware(fixture, setupTracker).InvokeAsync(setup);
        setupTracker.Called.Should().BeTrue();

        var login = CreateContext("/api/v1/tenant-a/login", "POST", true);
        login.Request.RouteValues["tenantId"] = "tenant-a";
        var loginTracker = new InvocationTracker();
        await CreateMiddleware(fixture, loginTracker).InvokeAsync(login);
        loginTracker.Called.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticated_user_without_tenant_should_be_rejected()
    {
        await using var fixture = await CreateFixtureAsync(true);
        var context = CreateContext("/api/v1/products", "GET");
        context.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var tracker = new InvocationTracker();

        await CreateMiddleware(fixture, tracker).InvokeAsync(context);

        tracker.Called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Authenticated_user_from_another_tenant_should_be_forbidden()
    {
        await using var fixture = await CreateFixtureAsync(true);
        var context = CreateContext("/tenant-b/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-b";
        context.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var tracker = new InvocationTracker();

        await CreateMiddleware(fixture, tracker).InvokeAsync(context);

        tracker.Called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Sysadmin_should_bypass_tenant_mismatch()
    {
        await using var fixture = await CreateFixtureAsync(true);
        var context = CreateContext("/tenant-b/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-b";
        context.User = Principal("sys-1", TenantRoles.SysAdmin, null);
        var tracker = new InvocationTracker();

        await CreateMiddleware(fixture, tracker).InvokeAsync(context);

        tracker.Called.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticated_user_should_be_restricted_until_tenant_setup_is_complete()
    {
        await using var fixture = await CreateFixtureAsync(true);
        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, false);
        var context = CreateContext("/tenant-a/products", "GET");
        context.Request.RouteValues["tenantId"] = "tenant-a";
        context.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var tracker = new InvocationTracker();

        await CreateMiddleware(fixture, tracker).InvokeAsync(context);

        tracker.Called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Tenant_setup_endpoint_and_matching_complete_tenant_should_continue()
    {
        await using var fixture = await CreateFixtureAsync(true);
        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, false);
        var setup = CreateContext("/api/v1/tenant-a/setup/complete", "POST");
        setup.Request.RouteValues["tenantId"] = "tenant-a";
        setup.User = Principal("user-1", TenantRoles.TenantAdmin, "tenant-a");
        var setupTracker = new InvocationTracker();
        await CreateMiddleware(fixture, setupTracker).InvokeAsync(setup);
        setupTracker.Called.Should().BeTrue();

        fixture.Tenants.Items["tenant-a"] = new TenantResponse("tenant-a", "Tenant A", "db", true, true);
        var normal = CreateContext("/tenant-a/products", "GET");
        normal.Request.RouteValues["tenantId"] = "tenant-a";
        normal.User = Principal("user-1", TenantRoles.TenantOperator, "tenant-a");
        var normalTracker = new InvocationTracker();
        await CreateMiddleware(fixture, normalTracker).InvokeAsync(normal);
        normalTracker.Called.Should().BeTrue();
    }

    private static TenantAccessMiddleware CreateMiddleware(Fixture fixture, InvocationTracker tracker)
        => new(_ => { tracker.Called = true; return Task.CompletedTask; }, fixture.Tenants, fixture.Context);

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
        if (tenantId is not null)
        {
            claims.Add(new Claim(TenantClaimTypes.TenantId, tenantId));
        }
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

    private sealed class InvocationTracker
    {
        public bool Called { get; set; }
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
