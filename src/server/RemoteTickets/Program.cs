await WebApplication
    .CreateBuilder(args)
    .AddRemoteTickets()
    .Build()
    .UseRemoteTickets<App>()
    .RunAsync();
