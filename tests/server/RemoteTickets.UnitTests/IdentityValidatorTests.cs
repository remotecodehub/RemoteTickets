namespace RemoteTickets.UnitTests;

/// <summary>Verifies the validation rules for identity requests.</summary>
public sealed class IdentityValidatorTests
{
    /// <summary>Verifies that registration validation rejects an invalid email and short password.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Register_validator_should_reject_invalid_credentials()
    {
        ValidationResult result = await new RegisterCommandValidator().ValidateAsync(new RegisterCommand("bad", "short"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    /// <summary>Verifies that login validation rejects simultaneous authenticator and recovery codes.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Login_validator_should_reject_both_two_factor_codes()
    {
        ValidationResult result = await new LoginCommandValidator().ValidateAsync(new LoginCommand("user@example.com", "Password1!", "123456", "recovery"));
        result.IsValid.Should().BeFalse();
    }

    /// <summary>Verifies that password reset validation requires all required fields.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Reset_validator_should_require_email_code_and_password()
    {
        ValidationResult result = await new ResetPasswordCommandValidator().ValidateAsync(new ResetPasswordCommand("", "", "short"));
        result.IsValid.Should().BeFalse();
        result.Errors.Select(x => x.PropertyName).Should().Contain(["Email", "ResetCode", "NewPassword"]);
    }

    /// <summary>Verifies that identity updates allow optional email and password changes.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Update_validator_should_allow_optional_email_and_password()
    {
        ValidationResult result = await new UpdateIdentityInfoCommandValidator().ValidateAsync(new UpdateIdentityInfoCommand("user-id", null, null, "Password1!"));
        result.IsValid.Should().BeTrue();
    }

    /// <summary>Verifies valid and invalid first-time setup credentials.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Initialize_setup_validator_should_validate_email_and_password()
    {
        var validator = new InitializeSetupCommandValidator();
        (await validator.ValidateAsync(new InitializeSetupCommand("admin@example.com", "Password1!"))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new InitializeSetupCommand("invalid", "short"))).IsValid.Should().BeFalse();
    }

    /// <summary>Verifies that simple identity validators reject missing or malformed required fields.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Simple_identity_validators_should_require_expected_fields()
    {
        (await new RefreshTokenCommandValidator().ValidateAsync(new RefreshTokenCommand(""))).IsValid.Should().BeFalse();
        (await new RevokeTokenCommandValidator().ValidateAsync(new RevokeTokenCommand(""))).IsValid.Should().BeFalse();
        (await new ConfirmEmailCommandValidator().ValidateAsync(new ConfirmEmailCommand("", ""))).IsValid.Should().BeFalse();
        (await new ResendConfirmationEmailCommandValidator().ValidateAsync(new ResendConfirmationEmailCommand("invalid"))).IsValid.Should().BeFalse();
        (await new ForgotPasswordCommandValidator().ValidateAsync(new ForgotPasswordCommand("invalid"))).IsValid.Should().BeFalse();
    }
}
