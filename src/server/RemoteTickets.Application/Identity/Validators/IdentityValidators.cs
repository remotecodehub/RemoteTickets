namespace RemoteTickets.Application.Identity.Validators;

/// <summary>Validates registration requests.</summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    /// <summary>Initializes registration validation rules.</summary>
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

/// <summary>Validates login requests and prevents simultaneous second-factor credentials.</summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    /// <summary>Initializes login validation rules.</summary>
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.TwoFactorCode) || string.IsNullOrWhiteSpace(x.TwoFactorRecoveryCode))
            .WithMessage("Only one two-factor authentication code may be supplied.");
    }
}

/// <summary>Validates refresh-token requests.</summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>Initializes refresh-token validation rules.</summary>
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

/// <summary>Validates token-revocation requests.</summary>
public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
    /// <summary>Initializes token-revocation validation rules.</summary>
    public RevokeTokenCommandValidator() => RuleFor(x => x.AccessToken).NotEmpty();
}

/// <summary>Validates email-confirmation requests.</summary>
public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    /// <summary>Initializes email-confirmation validation rules.</summary>
    public ConfirmEmailCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}

/// <summary>Validates requests to resend email confirmation.</summary>
public sealed class ResendConfirmationEmailCommandValidator : AbstractValidator<ResendConfirmationEmailCommand>
{
    /// <summary>Initializes resend-confirmation validation rules.</summary>
    public ResendConfirmationEmailCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

/// <summary>Validates password recovery requests.</summary>
public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    /// <summary>Initializes password-recovery validation rules.</summary>
    public ForgotPasswordCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

/// <summary>Validates password reset requests.</summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    /// <summary>Initializes password-reset validation rules.</summary>
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.ResetCode).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

/// <summary>Validates identity information update requests.</summary>
public sealed class UpdateIdentityInfoCommandValidator : AbstractValidator<UpdateIdentityInfoCommand>
{
    /// <summary>Initializes identity-information validation rules.</summary>
    public UpdateIdentityInfoCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OldPassword).NotEmpty();
        RuleFor(x => x.NewEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.NewEmail));
        RuleFor(x => x.NewPassword).MinimumLength(8).When(x => !string.IsNullOrWhiteSpace(x.NewPassword));
    }
}

/// <summary>Validates initial setup requests.</summary>
public sealed class InitializeSetupCommandValidator : AbstractValidator<InitializeSetupCommand>
{
    /// <summary>Initializes first-time setup validation rules.</summary>
    public InitializeSetupCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
