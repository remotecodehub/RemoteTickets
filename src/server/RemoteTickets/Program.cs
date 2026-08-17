await WebApplication
    .CreateBuilder(args)
    .BuildRemoteTickets()
    .RunAsync();
