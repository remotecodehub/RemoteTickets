namespace RemoteTickets.Application.System.Handlers;

public class SystemHandlers(ISystemService service) : IRequestHandler<MasterDatabaseSetupCommand, MasterDatabaseSetupResponse>
{
    public Task<MasterDatabaseSetupResponse> Handle(IReceiveContext<MasterDatabaseSetupCommand> context, CancellationToken cancellationToken) 
        => service.PerformMasterDatabaseSetup(context.Message.Request.ConnectionString, context.Message.Request.CommandTimeout, cancellationToken);    
}
