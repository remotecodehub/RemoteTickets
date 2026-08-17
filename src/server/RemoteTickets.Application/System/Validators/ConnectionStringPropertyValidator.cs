namespace RemoteTickets.Application.System.Validators;

public class ConnectionStringPropertyValidator<T> : PropertyValidator<T, string>
{
    private readonly HashSet<string> _allowedKeys;

    public ConnectionStringPropertyValidator(IEnumerable<string> allowedKeys)
    {
        _allowedKeys = new HashSet<string>(allowedKeys, StringComparer.OrdinalIgnoreCase);
    }

    public override string Name => "ConnectionStringValidator";

    public override bool IsValid(ValidationContext<T> context, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) { return true; }

        try
        {
            // The DbConnectionStringBuilder parses any generic connection string 
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            foreach (string key in builder.Keys)
            {
                if (!_allowedKeys.Contains(key))
                {
                    context.MessageFormatter.AppendArgument("InvalidKey", key);
                    return false;
                }
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    protected override string GetDefaultMessageTemplate(string errorCode) 
        => "The connection string have an not supported property: '{InvalidKey}'";
}
 