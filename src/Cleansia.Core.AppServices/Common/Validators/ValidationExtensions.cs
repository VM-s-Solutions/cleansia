using Cleansia.Core.AppServices.Common;
using FluentValidation;

namespace Cleansia.Core.AppServices.Common.Validators;

public static class ValidationExtensions
{
    public static bool BeAValidDate(DateOnly date)
    {
        return date != default;
    }

    public static bool BeInPast(DateOnly date)
    {
        return date < DateOnly.FromDateTime(DateTime.Today);
    }

    public static bool BeReasonableAge(DateOnly date)
    {
        var minDate = DateOnly.FromDateTime(DateTime.Today).AddYears(-120);
        var maxDate = DateOnly.FromDateTime(DateTime.Today).AddYears(-18);
        return date >= minDate && date <= maxDate;
    }

    public static IRuleBuilderOptions<T, DateOnly> MustBeValidDate<T>(this IRuleBuilder<T, DateOnly> ruleBuilder)
    {
        return ruleBuilder
            .Must(BeAValidDate)
            .WithMessage(BusinessErrorMessage.InvalidDate);
    }

    public static IRuleBuilderOptions<T, DateOnly> MustBeInPast<T>(this IRuleBuilder<T, DateOnly> ruleBuilder)
    {
        return ruleBuilder
            .Must(BeInPast)
            .WithMessage(BusinessErrorMessage.DateMustBeInPast);
    }

    public static IRuleBuilderOptions<T, DateOnly> MustBeReasonableAge<T>(this IRuleBuilder<T, DateOnly> ruleBuilder)
    {
        return ruleBuilder
            .Must(BeReasonableAge)
            .WithMessage(BusinessErrorMessage.InvalidAge);
    }

    public static IRuleBuilderOptions<T, string> ValidateStreetAddress<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .Length(5, 255)
            .WithMessage(BusinessErrorMessage.InvalidLength);
    }

    public static IRuleBuilderOptions<T, string> ValidateCity<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .Length(2, 100)
            .WithMessage(BusinessErrorMessage.InvalidLength);
    }

    public static IRuleBuilderOptions<T, string> ValidateZipCode<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .Length(3, 20)
            .WithMessage(BusinessErrorMessage.InvalidLength);
    }

    public static IRuleBuilderOptions<T, string> ValidatePassportId<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .Length(5, 20)
            .WithMessage(BusinessErrorMessage.InvalidLength);
    }

    public static IRuleBuilderOptions<T, string> ValidateTaxId<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(20)
            .WithMessage(BusinessErrorMessage.InvalidLength);
    }

    public static IRuleBuilderOptions<T, string> ValidateIban<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .Length(15, 34)
            .WithMessage(BusinessErrorMessage.InvalidLength);
    }

    public static IRuleBuilderOptions<T, string> ValidateEmergencyName<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(100)
            .WithMessage(BusinessErrorMessage.InvalidLength);
    }

    public static IRuleBuilderOptions<T, string> ValidatePhoneNumber<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required);
    }
}