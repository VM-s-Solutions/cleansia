using System.ComponentModel.DataAnnotations;

namespace Cleansia.Infra.Common.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class DateRangeControlAttribute(int yearsRange) : ValidationAttribute
{
    public int YearsRange { get; } = yearsRange;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is DateTimeOffset date)
        {
            var minDate = DateTimeOffset.UtcNow.AddYears(-YearsRange);

            if (date < minDate || date > DateTimeOffset.UtcNow)
            {
                return new ValidationResult($"Date must be between {minDate:yyyy-MM-dd} and {DateTimeOffset.UtcNow:yyyy-MM-dd}");
            }
        }

        return ValidationResult.Success;
    }
}