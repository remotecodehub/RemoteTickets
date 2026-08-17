namespace RemoteTickets.UnitTests;

/// <summary>Verifies Entity Framework Core persistence behavior for soft-deletable and auditable identity entities.</summary>
public sealed class PersistenceTests
{
    /// <summary>Verifies that soft-deleted users are hidden by the configured query filter and audited as deletions.</summary>
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
        (await fixture.Context.EntityAuditHistory.Where(x => x.EntityId == user.Id).OrderBy(x => x.UpdatedAt).LastAsync(TestContext.Current.CancellationToken)).Operation.Should().Be("Deleted");
    }

    /// <summary>Verifies that asynchronous save operations apply the same soft-delete behavior.</summary>
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

    /// <summary>Verifies that an added and subsequently modified user produce audit history containing both states.</summary>
    [Fact]
    public async Task Auditable_entities_should_record_creation_and_update_states()
    {
        await using ContextFixture fixture = await CreateContextAsync();
        var user = new User("audit@example.com") { Email = "audit@example.com", EmailConfirmed = true };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        user.DisplayName = "Updated";
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<EntityAuditRecord> history = await fixture.Context.EntityAuditHistory.Where(x => x.EntityId == user.Id).OrderBy(x => x.UpdatedAt).ToListAsync(TestContext.Current.CancellationToken);
        history.Should().HaveCount(2);
        history[0].Operation.Should().Be("Created");
        history[0].PreviousEntityState.Should().Be("{}");
        history[0].CurrentEntityState.Should().Contain("audit@example.com");
        history[1].Operation.Should().Be("Updated");
        history[1].PreviousEntityState.Should().Contain("audit@example.com");
        history[1].CurrentEntityState.Should().Contain("Updated");
        user.CreatedAt.Should().NotBe(default);
        user.CreatedBy.Should().Be("system");
    }

    /// <summary>Verifies that audit records capture the authenticated actor and that the history table belongs to the audit schema.</summary>
    [Fact]
    public async Task Audit_history_should_record_authenticated_actor_and_schema()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "actor-1")], "test")) } };
        await using ContextFixture fixture = await CreateContextAsync(accessor);
        var user = new User("actor@example.com") { Email = "actor@example.com", EmailConfirmed = true };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        EntityAuditRecord history = await fixture.Context.EntityAuditHistory.SingleAsync(x => x.EntityId == user.Id, TestContext.Current.CancellationToken);
        history.UpdatedBy.Should().Be("actor-1");
        var auditEntity = fixture.Context.Model.FindEntityType(typeof(EntityAuditRecord));
        auditEntity.Should().NotBeNull();
        auditEntity!.GetSchema().Should().Be("audit");
        auditEntity.GetTableName().Should().Be("EntityHistory");
    }

    /// <summary>Verifies that the audit actor resolver falls back from subject to email and then to the system actor.</summary>
    [Fact]
    public async Task Audit_actor_should_fallback_to_subject_email_and_system()
    {
        var subAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "subject-1")], "test")) } };
        await using (ContextFixture fixture = await CreateContextAsync(subAccessor))
        {
            var user = new User("sub@example.com") { Email = "sub@example.com" };
            fixture.Context.Users.Add(user);
            await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
            (await fixture.Context.EntityAuditHistory.SingleAsync(x => x.EntityId == user.Id, TestContext.Current.CancellationToken)).UpdatedBy.Should().Be("subject-1");
        }

        var emailAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "actor@example.com")], "test")) } };
        await using (ContextFixture fixture = await CreateContextAsync(emailAccessor))
        {
            var user = new User("email@example.com") { Email = "email@example.com" };
            fixture.Context.Users.Add(user);
            await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
            (await fixture.Context.EntityAuditHistory.SingleAsync(x => x.EntityId == user.Id, TestContext.Current.CancellationToken)).UpdatedBy.Should().Be("actor@example.com");
        }

        await using (ContextFixture fixture = await CreateContextAsync())
        {
            var user = new User("system@example.com") { Email = "system@example.com" };
            fixture.Context.Users.Add(user);
            await fixture.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
            (await fixture.Context.EntityAuditHistory.SingleAsync(x => x.EntityId == user.Id, TestContext.Current.CancellationToken)).UpdatedBy.Should().Be("system");
        }
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

    private static async Task<ContextFixture> CreateContextAsync(IHttpContextAccessor? accessor = null)
    {
        DbContextOptions<RemoteTicketsDbContext> options = new DbContextOptionsBuilder<RemoteTicketsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        var context = new RemoteTicketsDbContext(options, accessor);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return new ContextFixture(context);
    }

    private sealed class ContextFixture(RemoteTicketsDbContext context) : IAsyncDisposable
    {
        public RemoteTicketsDbContext Context { get; } = context;
        public ValueTask DisposeAsync() { Context.Dispose(); return ValueTask.CompletedTask; }
    }
}
