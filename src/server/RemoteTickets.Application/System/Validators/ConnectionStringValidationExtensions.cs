namespace RemoteTickets.Application.System.Validators;

public static class ConnectionStringValidationExtensions
{
    /// <summary>
    /// Extension method to be used into ab AbstractValidator to validate connection strings.
    /// </summary>
    /// <param name="ruleBuilder">Rule Builder from abstract validator</param>
    /// <param name="allowedKeys">Allowed keys of connections string according to desired db engine.</param>
    /// <typeparam name="T">an <see cref="T"/> instance to be validated</typeparam>
    /// <returns></returns>
    public static IRuleBuilderOptions<T, string> IsValidConnectionString<T>(
        this IRuleBuilder<T, string> ruleBuilder, IEnumerable<string> allowedKeys) 
        => ruleBuilder.SetValidator(
            new ConnectionStringPropertyValidator<T>(allowedKeys));
}
