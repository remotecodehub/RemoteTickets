namespace RemoteTickets.Application.System.Requests;

/// <summary>Requests the current first-time setup status.</summary>
public sealed record GetSetupStatusQuery : IRequest;
/// <summary>Requests creation of the initial administrator account.</summary>
/// <param name="Email">The administrator email address.</param>
/// <param name="Password">The administrator password.</param>
public sealed record InitializeSetupCommand(string Email, string Password) : IRequest;

/// <summary>Represents the command for perform setup of master database. </summary>
/// <param name="Request">The request for this command.</param>
public sealed record MasterDatabaseSetupCommand(MasterDatabaseSetupRequest Request) : IRequest;

/// <summary>Response for a <see cref="MasterDatabaseSetupCommand"/> command.</summary>
/// <param name="Status">The status of command execution.</param>
/// <param name="Message">Optional message about the result..</param>
public sealed record MasterDatabaseSetupResponse(bool Status, string Message) : IResponse;
