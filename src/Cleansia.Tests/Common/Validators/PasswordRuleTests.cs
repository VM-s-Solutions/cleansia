using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using FluentValidation;

namespace Cleansia.Tests.Common.Validators;

/// <summary>
/// The single source of truth for password complexity. One shared rule extension is the
/// only definition; every auth/password feature composes it. The complexity is: minimum 8
/// characters, at least one letter and one digit.
/// </summary>
public class PasswordRuleTests
{
    private sealed record Subject(string Password);

    private sealed class SubjectValidator : AbstractValidator<Subject>
    {
        public SubjectValidator()
        {
            RuleFor(x => x.Password).ValidatePassword();
        }
    }

    private readonly SubjectValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_Password_Fails_With_Required(string? password)
    {
        var result = _validator.Validate(new Subject(password!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Theory]
    [InlineData("short1")]      // 6 chars, has letter + digit but too short
    [InlineData("password")]    // 8 chars, letters only, no digit
    [InlineData("12345678")]    // 8 chars, digits only, no letter
    [InlineData("abc12")]       // letter + digit but < 8
    public void Weak_Password_Fails_With_InvalidPasswordFormat(string password)
    {
        var result = _validator.Validate(new Subject(password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPasswordFormat);
    }

    [Theory]
    [InlineData("password1")]   // 9 chars, letter + digit
    [InlineData("Abcd1234")]    // 8 chars, mixed case + digit
    [InlineData("aaaa1111")]    // minimal valid
    public void Strong_Password_Passes(string password)
    {
        var result = _validator.Validate(new Subject(password));

        Assert.True(result.IsValid);
    }
}
