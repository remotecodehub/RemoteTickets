namespace RemoteTickets.Application.Tenant.Handlers;

/// <summary>
/// Class that implement all Tenant queries and commands handlers 
/// </summary>
/// <param name="service">An <see cref="ITenantManagementService"/> injected instance.</param>
public class TenantHandlers(ITenantManagementService service) : 
IRequestHandler<GetTenantSetupStatusQuery, TenantSetupStatusResponse>,
IRequestHandler<CompleteTenantSetupCommand, TenantSetupStatusResponse>,
IRequestHandler<CreateTenantCommand, TenantResponse>,
IRequestHandler<GetTenantQuery, TenantResponse?>
{
    public async Task<TenantSetupStatusResponse> Handle(
        IReceiveContext<GetTenantSetupStatusQuery> context,
        CancellationToken cancellationToken) 
        => await service.GetSetupStatusAsync(
            context.Message.TenantId,
            cancellationToken);
    public async Task<TenantSetupStatusResponse> Handle(
        IReceiveContext<CompleteTenantSetupCommand> context,
        CancellationToken cancellationToken) 
        => await service.CompleteSetupAsync(
            context.Message.TenantId,
            cancellationToken);
    public async Task<TenantResponse> Handle(
        IReceiveContext<CreateTenantCommand> context,
         CancellationToken cancellationToken) 
         => await service.CreateAsync(context.Message.ToRequest() , cancellationToken);
    public async Task<TenantResponse?> Handle(
        IReceiveContext<GetTenantQuery> context,
        CancellationToken cancellationToken) 
        => await service.GetAsync(context.Message.TenantId, cancellationToken);
}