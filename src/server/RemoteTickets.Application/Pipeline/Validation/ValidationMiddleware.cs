namespace RemoteTickets.Application.Pipeline.Validation;

/// <summary>Validates application messages before they enter the request pipeline.</summary>
public interface IMessageValidator
{
    /// <summary>Validates a message using its registered validator.</summary>
    /// <param name="message">The message to validate.</param>
    /// <param name="cancellationToken">The token used to cancel validation.</param>
    /// <returns>A task that completes when validation succeeds.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ValidationException">Thrown when a registered validator reports validation errors.</exception>
    Task ValidateAsync(object message, CancellationToken cancellationToken);
}

/// <summary>Resolves and executes FluentValidation validators for application messages.</summary>
/// <param name="serviceProvider">The service provider used to resolve message validators.</param>
public sealed class FluentMessageValidator(IServiceProvider serviceProvider) : IMessageValidator
{
    /// <inheritdoc />
    public async Task ValidateAsync(object message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        Type validatorType = typeof(IValidator<>).MakeGenericType(message.GetType());

        if (serviceProvider.GetService(validatorType) is not IValidator validator)
        {
            return;
        }

        var context = new ValidationContext<object>(message);
        ValidationResult result = await validator.ValidateAsync(context, cancellationToken);

        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }
}

/// <summary>Adds message validation to a Mediator.Net pipeline.</summary>
/// <typeparam name="TContext">The pipeline context type.</typeparam>
/// <param name="validator">The message validator used by the pipeline.</param>
public sealed class ValidationMiddleware<TContext>(IMessageValidator validator) : IPipeSpecification<TContext>
    where TContext : IContext<IMessage>
{
    /// <summary>Determines whether validation should execute for the current context.</summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns><see langword="true"/> for every message.</returns>
    public bool ShouldExecute(TContext context, CancellationToken cancellationToken) => true;

    /// <summary>Validates the message before the handler executes.</summary>
    /// <param name="context">The pipeline context containing the message.</param>
    /// <param name="cancellationToken">The token used to cancel validation.</param>
    /// <returns>A task that completes when validation succeeds.</returns>
    public Task BeforeExecute(TContext context, CancellationToken cancellationToken) =>
        validator.ValidateAsync(context.Message, cancellationToken);

    /// <summary>Executes the middleware's main stage.</summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task Execute(TContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Executes the middleware's post-handler stage.</summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task AfterExecute(TContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Handles exceptions raised by the pipeline without transforming them.</summary>
    /// <param name="ex">The exception raised by the pipeline.</param>
    /// <param name="context">The pipeline context.</param>
    /// <returns>A completed task.</returns>
    public Task OnException(Exception ex, TContext context) => Task.CompletedTask;
}

/// <summary>Provides extension methods for adding validation to Mediator.Net pipelines.</summary>
public static class ValidationMiddlewareExtensions
{
    /// <summary>Adds the validation middleware to a Mediator.Net pipeline.</summary>
    /// <typeparam name="TContext">The pipeline context type.</typeparam>
    /// <param name="configurator">The pipeline configurator.</param>
    public static void UseValidation<TContext>(this IPipeConfigurator<TContext> configurator)
        where TContext : IContext<IMessage>
    {
        configurator.AddPipeSpecification(
            new ValidationMiddleware<TContext>(configurator.DependencyScope.Resolve<IMessageValidator>()));
    }
}
