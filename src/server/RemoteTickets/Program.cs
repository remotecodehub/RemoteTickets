await WebApplication
    .CreateBuilder(args)
    .RunRemoteTicketsAsync<App>();