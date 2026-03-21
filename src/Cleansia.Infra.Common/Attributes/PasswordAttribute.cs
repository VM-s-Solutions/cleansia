using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Cleansia.Infra.Common.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class PasswordAttribute : ValidationAttribute
{
    // Requires: minimum 8 characters, at least one letter and one digit
    private const string PasswordRegex = @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$";

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
        return "Password must be at least 8 characters and contain at least one letter and one number";
    }
}
