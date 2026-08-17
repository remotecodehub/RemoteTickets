namespace RemoteTickets.Application.System.Validators;

public class MasterDatabaseSetupCommandValidator : AbstractValidator<MasterDatabaseSetupCommand>
{
    private static readonly string[] SqlServerKeys = 
    { 
        "Data Source", "Server", "Initial Catalog", "Database", 
        "Integrated Security", "User ID", "Password", "Encrypt", 
        "TrustServerCertificate", "Connection Timeout" 
    };
    public MasterDatabaseSetupCommandValidator()
    {
        RuleFor(x => x)
            .NotNull()
            .NotEmpty()
            .WithErrorCode("BAD_REQUEST")
            .WithMessage("The request cannot be null or empty."); 

        RuleFor(x => x.Request)
            .NotNull()
            .NotEmpty()
            .WithErrorCode("BAD_REQUEST")
            .WithMessage("The request data cannot be null or empty.");
        
        RuleFor(x => x.Request.ConnectionString)
            .NotNull()
            .NotEmpty()
            .WithErrorCode("UNPROCESSABLE_ENTITY")
            .WithMessage("The connection string cannot be null or empty")
            .IsValidConnectionString(SqlServerKeys)
            .WithErrorCode("UNPROCESSABLE_ENTITY")
            .WithMessage("The connection string isn't valid");
    }
}
