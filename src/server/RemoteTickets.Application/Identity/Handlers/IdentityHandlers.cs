namespace RemoteTickets.Application.Identity.Handlers;

/// <summary>Handles the identity requests.</summary>
/// <param name="identityService">The identity service that performs registration and authentication.</param>
public sealed class IdentityHandlers(IIdentityService identityService) :
IRequestHandler<RegisterCommand, IdentityResultResponse>,
IRequestHandler<LoginCommand, Response<TokenResponse>>,
IRequestHandler<RefreshTokenCommand, Response<TokenResponse>>,
IRequestHandler<RevokeTokenCommand, Response<bool>>,
IRequestHandler<ConfirmEmailCommand, Response<bool>>,
IRequestHandler<ResendConfirmationEmailCommand, IdentityResultResponse>,
IRequestHandler<ForgotPasswordCommand, IdentityResultResponse>,
IRequestHandler<ResetPasswordCommand, IdentityResultResponse>,
IRequestHandler<GetIdentityInfoQuery, Response<IdentityInfoResponse>>,
IRequestHandler<UpdateIdentityInfoCommand, IdentityResultResponse>,
IRequestHandler<ConfigureTwoFactorCommand, Response<TwoFactorResponse>>,
IRequestHandler<GetSetupStatusQuery, SetupStatusResponse>,
IRequestHandler<InitializeSetupCommand, IdentityResultResponse>
{
    /// <summary>Executes the registration request.</summary>
    public Task<IdentityResultResponse> Handle(IReceiveContext<RegisterCommand> context, CancellationToken cancellationToken) => identityService.RegisterAsync(context.Message.Email, context.Message.Password, cancellationToken);
    /// <summary>Executes the login request.</summary>
    public async Task<Response<TokenResponse>> Handle(IReceiveContext<LoginCommand> context, CancellationToken cancellationToken)
    {
        var message = context.Message;
        var result = message.TenantId is null ? await identityService.LoginAsync(message.Email, message.Password, message.TwoFactorCode, message.TwoFactorRecoveryCode, cancellationToken) : await identityService.LoginAsync(message.TenantId, message.Email, message.Password, message.TwoFactorCode, message.TwoFactorRecoveryCode, cancellationToken);
        return result is null ? Response<TokenResponse>.Failure(["Invalid credentials."]) : Response<TokenResponse>.Success(result);
    }
    /// <summary>Executes a tenant-bound refresh-token exchange.</summary>
    public async Task<Response<TokenResponse>> Handle(IReceiveContext<RefreshTokenCommand> context, CancellationToken cancellationToken)
    {
        var message = context.Message;
        var result = message.TenantId is null ? await identityService.RefreshAsync(message.RefreshToken, cancellationToken) : await identityService.RefreshAsync(message.TenantId, message.RefreshToken, cancellationToken);
        return result is null ? Response<TokenResponse>.Failure(["Invalid refresh token."]) : Response<TokenResponse>.Success(result);
    }
    /// <summary>Executes token revocation.</summary>
    public async Task<Response<bool>> Handle(IReceiveContext<RevokeTokenCommand> context, CancellationToken cancellationToken) => Response<bool>.Success(await identityService.RevokeAsync(context.Message.AccessToken, cancellationToken));
    /// <summary>Executes email confirmation.</summary>
    public async Task<Response<bool>> Handle(IReceiveContext<ConfirmEmailCommand> context, CancellationToken cancellationToken) => Response<bool>.Success(await identityService.ConfirmEmailAsync(context.Message.UserId, context.Message.Code, context.Message.ChangedEmail, cancellationToken));
    /// <summary>Executes a confirmation-email resend request.</summary>
    public Task<IdentityResultResponse> Handle(IReceiveContext<ResendConfirmationEmailCommand> context, CancellationToken cancellationToken) => identityService.ResendConfirmationEmailAsync(context.Message.Email, cancellationToken);
    /// <summary>Executes a password-recovery request.</summary>
    public Task<IdentityResultResponse> Handle(IReceiveContext<ForgotPasswordCommand> context, CancellationToken cancellationToken) => identityService.ForgotPasswordAsync(context.Message.Email, cancellationToken);
    /// <summary>Executes a password reset.</summary>
    public Task<IdentityResultResponse> Handle(IReceiveContext<ResetPasswordCommand> context, CancellationToken cancellationToken) => identityService.ResetPasswordAsync(context.Message.Email, context.Message.ResetCode, context.Message.NewPassword, cancellationToken);
    /// <summary>Executes an identity information query.</summary>
    public async Task<Response<IdentityInfoResponse>> Handle(IReceiveContext<GetIdentityInfoQuery> context, CancellationToken cancellationToken)
    {
        var result = await identityService.GetInfoAsync(context.Message.UserId, cancellationToken);
        return result is null ? Response<IdentityInfoResponse>.Failure(["User not found."]) : Response<IdentityInfoResponse>.Success(result);
    }
    /// <summary>Executes an identity information update.</summary>
    public Task<IdentityResultResponse> Handle(IReceiveContext<UpdateIdentityInfoCommand> context, CancellationToken cancellationToken) => identityService.UpdateInfoAsync(context.Message.UserId, context.Message.NewEmail, context.Message.NewPassword, context.Message.OldPassword, cancellationToken);
    /// <summary>Executes two-factor configuration.</summary>
    public async Task<Response<TwoFactorResponse>> Handle(IReceiveContext<ConfigureTwoFactorCommand> context, CancellationToken cancellationToken)
    {
        var result = await identityService.ConfigureTwoFactorAsync(context.Message.UserId, context.Message.Enable, context.Message.TwoFactorCode, context.Message.ResetRecoveryCodes, context.Message.ResetSharedKey, context.Message.ForgetMachine, cancellationToken);
        return result is null ? Response<TwoFactorResponse>.Failure(["The two-factor configuration request is invalid."]) : Response<TwoFactorResponse>.Success(result);
    }
    /// <summary>Reads the current setup status.</summary>
    public Task<SetupStatusResponse> Handle(IReceiveContext<GetSetupStatusQuery> context, CancellationToken cancellationToken) => identityService.GetSetupStatusAsync(cancellationToken);
    /// <summary>Executes first-time setup initialization.</summary>
    public Task<IdentityResultResponse> Handle(IReceiveContext<InitializeSetupCommand> context, CancellationToken cancellationToken) => identityService.InitializeSetupAsync(context.Message.Email, context.Message.Password, cancellationToken);
}
