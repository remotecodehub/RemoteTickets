namespace RemoteTickets.UnitTests;

/// <summary>Verifies Entity Framework Core persistence behavior for soft-deletable identity entities.</summary>
public sealed class PersistenceTests
{
    /// <summary>Verifies that soft-deleted users are hidden by the configured query filter.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Soft_deleted_users_should_be_hidden_by_query_filter()
    {
        await using ContextFixture fixture = await CreateContextAsync();
        var user = new User("deleted@example.com") { Email = "deleted@example.com", EmailConfirmed = true };
        fixture.Context.Users.Add(user);
        fixture.Context.SaveChanges();

        fixture.Context.Users.Remove(user);
        fixture.Context.SaveChanges();

        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
        (await fixture.Context.Users.SingleOrDefaultAsync(x => x.Id == user.Id)).Should().BeNull();
        (await fixture.Context.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == user.Id)).IsDeleted.Should().BeTrue();
    }

    /// <summary>Verifies that asynchronous save operations apply the same soft-delete behavior.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Async_soft_delete_should_apply_the_same_behavior()
    {
        await using ContextFixture fixture = await CreateContextAsync();
        var user = new User("async@example.com") { Email = "async@example.com", EmailConfirmed = true };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        fixture.Context.Users.Remove(user);
        await fixture.Context.SaveChangesAsync(false, TestContext.Current.CancellationToken);

        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
    }

    /// <summary>Verifies that user and role constructors assign non-empty identity identifiers.</summary>
    [Fact]
    public void User_and_role_constructors_should_initialize_identity_ids()
    {
        var user = new User();
        var namedUser = new User("user");
        var role = new Role();
        var namedRole = new Role("Administrator");

        user.Id.Should().NotBeNullOrWhiteSpace();
        namedUser.Id.Should().NotBeNullOrWhiteSpace();
        role.Id.Should().NotBeNullOrWhiteSpace();
        namedRole.Id.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<ContextFixture> CreateContextAsync()
    {
        DbContextOptions<RemoteTicketsDbContext> options = new DbContextOptionsBuilder<RemoteTicketsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var context = new RemoteTicketsDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return new ContextFixture(context);
    }

    private sealed class ContextFixture(RemoteTicketsDbContext context) : IAsyncDisposable
    {
        public RemoteTicketsDbContext Context { get; } = context;

        public ValueTask DisposeAsync()
        {
            Context.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
