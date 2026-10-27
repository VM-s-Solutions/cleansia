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

    /// <summary>
    /// A letter is a letter in any script, as the apps' "Contains a letter" rule counts it, so a password
    /// whose only letters are Cyrillic or accented is not refused after the app showed every rule met.
    /// </summary>
    [Theory]
    [InlineData("Пароль2024")]
    [InlineData("ěščřžýáí1")]
    [InlineData("пароль12")]
    public void A_Letter_From_Any_Script_Counts(string password)
    {
        var result = _validator.Validate(new Subject(password));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// A digit is a decimal digit in any script (.NET's <c>\d</c> is Unicode), as a letter is a letter in
    /// any script, so a password whose only digits are Arabic-Indic or full-width is not refused.
    /// </summary>
    [Theory]
    [InlineData("Пароль٢٠٢٤")]
    [InlineData("Heslo１２３")]
    public void A_Digit_From_Any_Script_Counts(string password)
    {
        var result = _validator.Validate(new Subject(password));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Пароль")]      // letters only, no digit
    [InlineData("1234567٣")]    // digits only, one of them Arabic-Indic
    [InlineData("Пар1")]        // a letter and a digit, too short
    public void A_Password_Missing_A_Rule_Is_Still_Refused_Whatever_Its_Script(string password)
    {
        var result = _validator.Validate(new Subject(password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPasswordFormat);
    }
}
