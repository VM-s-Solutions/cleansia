using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Cleansia.Infra.Common.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class PasswordAttribute : ValidationAttribute
{
    private const string PasswordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

    public override bool IsValid(object? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return true;
        }

        var password = value.ToString();
        return Regex.IsMatch(password!, PasswordRegex);
    }
}
