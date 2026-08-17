namespace RemoteTickets.UnitTests;

/// <summary>Covers application contracts, tenant handlers, setup validation, and connection-string validation branches.</summary>
public sealed class ApplicationFoundationTests
{
    [Fact]
    public void CreateTenantRequest_should_convert_valid_request_and_validate_required_fields()
    {
        var request = new CreateTenantRequest("id", "tenant", "database", "Server=.;Database=database", "admin@example.com", "Password1!");
        request.ToCommand().Name.Should().Be("tenant");
        AssertArgument(() => (request with { Name = string.Empty }).ToCommand());
        AssertArgument(() => (request with { Name = "   " }).ToCommand());
        AssertArgument(() => (request with { DatabaseName = string.Empty }).ToCommand());
        AssertArgument(() => (request with { DatabaseName = "   " }).ToCommand());
        AssertArgument(() => (request with { ConnectionString = string.Empty }).ToCommand());
        AssertArgument(() => (request with { ConnectionString = "   " }).ToCommand());
        AssertArgument(() => (request with { AdminEmail = string.Empty }).ToCommand());
        AssertArgument(() => (request with { AdminEmail = "   " }).ToCommand());
        AssertArgument(() => (request with { AdminPassword = string.Empty }).ToCommand());
        AssertArgument(() => (request with { AdminPassword = "   " }).ToCommand());
    }

    [Fact]
    public void CreateTenantCommand_should_convert_valid_command_and_validate_required_fields()
    {
        var command = new CreateTenantCommand("tenant", "database", "Server=.;Database=database", "admin@example.com", "Password1!");
        command.ToRequest().Name.Should().Be("tenant");
        AssertArgument(() => (command with { Name = string.Empty }).ToRequest());
        AssertArgument(() => (command with { Name = "   " }).ToRequest());
        AssertArgument(() => (command with { DatabaseName = string.Empty }).ToRequest());
        AssertArgument(() => (command with { DatabaseName = "   " }).ToRequest());
        AssertArgument(() => (command with { ConnectionString = string.Empty }).ToRequest());
        AssertArgument(() => (command with { ConnectionString = "   " }).ToRequest());
        AssertArgument(() => (command with { AdminEmail = string.Empty }).ToRequest());
        AssertArgument(() => (command with { AdminEmail = "   " }).ToRequest());
        AssertArgument(() => (command with { AdminPassword = string.Empty }).ToRequest());
        AssertArgument(() => (command with { AdminPassword = "   " }).ToRequest());
    }

    [Fact]
    public void Connection_string_validator_should_cover_empty_valid_invalid_and_malformed_values()
    {
        var validator = new ConnectionStringPropertyValidator<object>(["Server", "Database"]);
        var context = new ValidationContext<object>(new object());
        validator.IsValid(context, string.Empty).Should().BeTrue();
        validator.IsValid(context, "Server=localhost;Database=master").Should().BeTrue();
        validator.IsValid(context, "Server=localhost;Password=secret").Should().BeFalse();
        validator.IsValid(context, "Server=\"unterminated").Should().BeFalse();
        validator.Name.Should().Be("ConnectionStringValidator");
    }

    [Fact]
    public void Master_database_validator_should_accept_valid_connection_and_reject_invalid_values()
    {
        var validator = new MasterDatabaseSetupCommandValidator();
        validator.Validate(new MasterDatabaseSetupCommand(new MasterDatabaseSetupRequest("Server=localhost;Database=master;Integrated Security=True", 30))).IsValid.Should().BeTrue();
        validator.Validate(new MasterDatabaseSetupCommand(new MasterDatabaseSetupRequest(string.Empty, 30))).IsValid.Should().BeFalse();
        validator.Validate(new MasterDatabaseSetupCommand(new MasterDatabaseSetupRequest("Server=localhost;Unsupported=1", 30))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Tenant_handlers_should_delegate_all_operations()
    {
        var service = new FakeTenantService();
        var handlers = new TenantHandlers(service);
        var cancellationToken = TestContext.Current.CancellationToken;
        (await handlers.Handle(new ReceiveContext<GetTenantSetupStatusQuery>(new("tenant")), cancellationToken)).IsSetupComplete.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<CompleteTenantSetupCommand>(new("tenant")), cancellationToken)).IsSetupComplete.Should().BeTrue();
        (await handlers.Handle(new ReceiveContext<CreateTenantCommand>(new("tenant", "db", "Server=.;Database=db", "admin@example.com", "Password1!")), cancellationToken)).Name.Should().Be("Tenant");
        (await handlers.Handle(new ReceiveContext<GetTenantQuery>(new("tenant")), cancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task System_handler_should_delegate_master_database_setup()
    {
        var service = new FakeSystemService();
        var handler = new SystemHandlers(service);
        var response = await handler.Handle(new ReceiveContext<MasterDatabaseSetupCommand>(new(new MasterDatabaseSetupRequest("Server=.", 15))), TestContext.Current.CancellationToken);
        response.Status.Should().BeTrue();
        service.ConnectionString.Should().Be("Server=.");
        service.CommandTimeout.Should().Be(15);
    }

    private static void AssertArgument(Action action) => action.Should().Throw<ArgumentNullException>();

    private sealed class FakeTenantService : ITenantManagementService
    {
        public Task<TenantResponse?> GetAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult<TenantResponse?>(new TenantResponse(tenantId, "Tenant", "db", true, true));
        public Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken) => Task.FromResult(new TenantResponse(request.Id ?? "tenant", request.Name, request.DatabaseName, true, false));
        public Task<TenantSetupStatusResponse> GetSetupStatusAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(new TenantSetupStatusResponse(false, true));
        public Task<TenantSetupStatusResponse> CompleteSetupAsync(string tenantId, CancellationToken cancellationToken) => Task.FromResult(new TenantSetupStatusResponse(false, true));
    }

    private sealed class FakeSystemService : ISystemService
    {
        public string ConnectionString { get; private set; } = string.Empty;
        public int CommandTimeout { get; private set; }
        public Task<MasterDatabaseSetupResponse> PerformMasterDatabaseSetup(string connectionString, int commandTimeout, CancellationToken cancellationToken)
        {
            ConnectionString = connectionString;
            CommandTimeout = commandTimeout;
            return Task.FromResult(new MasterDatabaseSetupResponse(true, "ok"));
        }
    }
}
