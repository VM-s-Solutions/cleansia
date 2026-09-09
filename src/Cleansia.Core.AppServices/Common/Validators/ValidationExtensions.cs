using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Common.Validators;

public static class ValidationExtensions
{
    // Single source of truth for password complexity: minimum 8 characters, at least one
    // letter and one digit. Every auth/password feature composes ValidatePassword — there is no
    // second copy of this regex on the backend, and the frontend constant mirrors it exactly.
    private const string PasswordPattern = @"^(?=.*[a-zA-Z])(?=.*\d).{8,}$";

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

    public static IRuleBuilderOptions<T, Dictionary<string, TTranslation>?> MustCoverAllActiveLanguages<T, TTranslation>(
        this IRuleBuilderInitial<T, Dictionary<string, TTranslation>?> ruleBuilder,
        ILanguageRepository languageRepository)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(BusinessErrorMessage.TranslationsRequired)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.TranslationsRequired)
            .MustAsync(async (translations, cancellationToken) =>
            {
                var activeLanguages = await languageRepository.GetAll()
                    .Where(l => l.IsActive)
                    .ToListAsync(cancellationToken);
                var activeLanguageCodes = activeLanguages.Select(l => l.Code).ToHashSet();
                var providedCodes = translations!.Keys.ToHashSet();
                return activeLanguageCodes.SetEquals(providedCodes);
            })
            .WithMessage(BusinessErrorMessage.MissingTranslationForLanguage);
    }

    /// <summary>
    /// A catalogue entry is priced per currency, and this is what stops one being saved half-priced.
    ///
    /// <para><b>Every ACTIVE currency must be present.</b> An entry with no price row in a currency is
    /// not offerable in it, so a save covering only some of the operated currencies quietly takes the
    /// entry off sale in the rest. The admin form renders a block per active currency, which makes a
    /// missing key a client bug rather than a choice.</para>
    ///
    /// <para><b>An INACTIVE currency may be present, and is not required.</b> On a Currency,
    /// <c>IsActive</c> is not the soft-delete flag it is elsewhere — <c>DeleteCurrency</c> hard-deletes,
    /// and nothing else ever writes it false. It is the market switch: the DEV seed carries CZK active
    /// and EUR inactive on purpose, and <c>SetDefaultCurrency</c> refuses to promote an inactive one so
    /// the CZK catalogue can never be charged under another code. EUR becomes active in the same change
    /// that gives it price rows, so pricing it BEFORE the flip is exactly the intended path and must be
    /// allowed; requiring it before then would be the opposite of the rule above.</para>
    ///
    /// <para>Codes that name no currency at all are refused rather than dropped -- the handlers key
    /// their upsert by code, so an unrecognised one would otherwise vanish behind a 200.</para>
    /// </summary>
    public static IRuleBuilderOptions<T, Dictionary<string, TPrice>?> MustCoverAllActiveCurrencies<T, TPrice>(
        this IRuleBuilderInitial<T, Dictionary<string, TPrice>?> ruleBuilder,
        ICurrencyRepository currencyRepository)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage(BusinessErrorMessage.PricesRequired)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.PricesRequired)
            .MustAsync(async (prices, cancellationToken) =>
            {
                var currencies = await currencyRepository.GetAll().ToListAsync(cancellationToken);
                return currencies.All(c => !c.IsActive || prices!.ContainsKey(c.Code));
            })
            .WithMessage(BusinessErrorMessage.MissingPriceForCurrency)
            .MustAsync(async (prices, cancellationToken) =>
            {
                var known = (await currencyRepository.GetAll().ToListAsync(cancellationToken))
                    .Select(c => c.Code)
                    .ToHashSet();
                return prices!.Keys.All(known.Contains);
            })
            .WithMessage(BusinessErrorMessage.CurrencyNotFound);
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

    public static IRuleBuilderOptions<T, string> ValidateFirstName<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength);
    }

    public static IRuleBuilderOptions<T, string> ValidateLastName<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength);
    }

    public static IRuleBuilderOptions<T, string> ValidateUserEmail<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .EmailAddress()
            .WithMessage(BusinessErrorMessage.InvalidEmailFormat)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength);
    }

    public static IRuleBuilderOptions<T, string> ValidatePassword<T>(this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .Matches(PasswordPattern)
            .WithMessage(BusinessErrorMessage.InvalidPasswordFormat);
    }

    // Same single-source-of-truth password complexity as ValidatePassword, but with the offending
    // field's name stamped on every error (the auth surface keys frontend i18n off the property name).
    public static IRuleBuilderOptions<T, string> ValidatePassword<T>(
        this IRuleBuilderInitial<T, string> ruleBuilder, string errorCode)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(errorCode)
            .Matches(PasswordPattern)
            .WithMessage(BusinessErrorMessage.InvalidPasswordFormat)
            .WithErrorCode(errorCode);
    }
}