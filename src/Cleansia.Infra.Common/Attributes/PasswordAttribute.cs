using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Cleansia.Infra.Common.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class PasswordAttribute : ValidationAttribute
{
    // Requires: minimum 12 characters, at least one uppercase, one lowercase, one digit, one special character
    private const string PasswordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()]).{12,}$";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        var password = value.ToString()!;

        if (!Regex.IsMatch(password, PasswordRegex))
        {
            return new ValidationResult(GetErrorMessage());
        }

        return ValidationResult.Success;
    }

    private string GetErrorMessage()
    {
        return "Password must be at least 12 characters and contain: one uppercase letter, one lowercase letter, one number, and one special character (@$!%*?&#^())";
    }
}
