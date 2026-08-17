namespace RemoteTickets.UnitTests;

/// <summary>Verifies message validation behavior in the application pipeline.</summary>
public sealed class ValidationMiddlewareTests
{
    /// <summary>Verifies that registered validators accept valid messages and reject invalid ones.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Fluent_message_validator_should_execute_registered_validator()
    {
        var services = new ServiceCollection();
        services.AddScoped<IValidator<RegisterCommand>, RegisterCommandValidator>();
        using ServiceProvider provider = services.BuildServiceProvider();
        var validator = new FluentMessageValidator(provider);

        Func<Task> valid = () => validator.ValidateAsync(new RegisterCommand("user@example.com", "Password1!"), CancellationToken.None);
        await valid();

        Func<Task> invalid = () => validator.ValidateAsync(new RegisterCommand("invalid", "short"), CancellationToken.None);
        await invalid.Should().ThrowAsync<ValidationException>();
    }

    /// <summary>Verifies that messages without a registered validator pass through unchanged.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Fluent_message_validator_should_skip_unregistered_messages()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var validator = new FluentMessageValidator(provider);

        await validator.ValidateAsync(new object(), CancellationToken.None);
    }

    /// <summary>Verifies that null messages are rejected before validator resolution.</summary>
    [Fact]
    public async Task Fluent_message_validator_should_reject_null_messages()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var validator = new FluentMessageValidator(provider);

        await FluentActions.Awaiting(() => validator.ValidateAsync(null!, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>Verifies that the validation middleware delegates its lifecycle to the validator.</summary>
    [Fact]
    public async Task Validation_middleware_should_execute_all_lifecycle_stages()
    {
        var validator = new FakeMessageValidator();
        var middleware = new ValidationMiddleware<ReceiveContext<RegisterCommand>>(validator);
        var context = new ReceiveContext<RegisterCommand>(new RegisterCommand("user@example.com", "Password1!"));

        middleware.ShouldExecute(context, TestContext.Current.CancellationToken).Should().BeTrue();
        await middleware.BeforeExecute(context, TestContext.Current.CancellationToken);
        await middleware.Execute(context, TestContext.Current.CancellationToken);
        await middleware.AfterExecute(context, TestContext.Current.CancellationToken);
        await middleware.OnException(new InvalidOperationException(), context);
        validator.Message.Should().Be(context.Message);
    }

    private sealed class FakeMessageValidator : IMessageValidator
    {
        public object? Message { get; private set; }

        public Task ValidateAsync(object message, CancellationToken cancellationToken)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }
}
