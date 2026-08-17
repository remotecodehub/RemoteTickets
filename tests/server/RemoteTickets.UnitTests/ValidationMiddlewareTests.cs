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
}
