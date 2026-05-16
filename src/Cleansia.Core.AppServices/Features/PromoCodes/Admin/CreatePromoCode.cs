using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin;

/// <summary>
/// Admin creation of a new promo code. The Code is normalised to uppercase
/// before being stored; uniqueness within the tenant scope is enforced
/// here (DB has a unique index but we want a friendly error code instead
/// of a constraint violation).
/// </summary>
public class CreatePromoCode
{
    private static readonly Regex CodeRegex = new("^[A-Z0-9]{3,20}$", RegexOptions.Compiled);

    public record Command(
        string Code,
        PromoCodeType Type,
        decimal? DiscountPercent,
        decimal? DiscountAmount,
        string? CurrencyId,
        decimal? MinimumOrderAmount,
        int MaxRedemptionsPerUser,
        int? GlobalMaxRedemptions,
        DateTimeOffset? ValidFrom,
        DateTimeOffset? ValidUntil,
        string? Description) : ICommand<Response>;

    public record Response(string PromoCodeId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(code => CodeRegex.IsMatch((code ?? string.Empty).Trim().ToUpperInvariant()))
                .WithMessage(BusinessErrorMessage.PromoCodeInvalidFormat);

            RuleFor(x => x.Type)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.MaxRedemptionsPerUser)
                .GreaterThanOrEqualTo(1)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.GlobalMaxRedemptions)
                .GreaterThanOrEqualTo(1)
                .When(x => x.GlobalMaxRedemptions.HasValue)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.MinimumOrderAmount)
                .GreaterThanOrEqualTo(0m)
                .When(x => x.MinimumOrderAmount.HasValue)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            // Validity range — when both bounds are provided, From <= Until.
            RuleFor(x => x)
                .Must(x => !x.ValidFrom.HasValue || !x.ValidUntil.HasValue || x.ValidFrom.Value <= x.ValidUntil.Value)
                .WithMessage(BusinessErrorMessage.PromoCodeValidityRangeInvalid);

            When(x => x.Type == PromoCodeType.PercentDiscount, () =>
            {
                RuleFor(x => x.DiscountPercent)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                    .WithMessage(BusinessErrorMessage.Required)
                    .Must(p => p > 0m && p <= 1m)
                    .WithMessage(BusinessErrorMessage.PromoCodePercentOutOfRange);
            });

            When(x => x.Type == PromoCodeType.FixedDiscount, () =>
            {
                RuleFor(x => x.DiscountAmount)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                    .WithMessage(BusinessErrorMessage.Required)
                    .GreaterThan(0m)
                    .WithMessage(BusinessErrorMessage.PromoCodeAmountMustBePositive);

                RuleFor(x => x.CurrencyId)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required)
                    .MustAsync(async (id, ct) => await currencyRepository.ExistsAsync(id!, ct))
                    .WithMessage(BusinessErrorMessage.CurrencyNotFound);
            });
        }
    }

    public class Handler(IPromoCodeRepository promoCodeRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var normalised = command.Code.Trim().ToUpperInvariant();

            var existing = await promoCodeRepository.GetByCodeAsync(normalised, cancellationToken);
            if (existing != null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(BusinessErrorMessage.PromoCodeAlreadyExists, BusinessErrorMessage.PromoCodeAlreadyExists));
            }

            var entity = command.Type == PromoCodeType.PercentDiscount
                ? PromoCode.CreatePercent(
                    code: normalised,
                    percent: command.DiscountPercent!.Value,
                    minimumOrderAmount: command.MinimumOrderAmount,
                    maxRedemptionsPerUser: command.MaxRedemptionsPerUser,
                    globalMaxRedemptions: command.GlobalMaxRedemptions,
                    validFrom: command.ValidFrom,
                    validUntil: command.ValidUntil,
                    description: command.Description)
                : PromoCode.CreateFixed(
                    code: normalised,
                    amount: command.DiscountAmount!.Value,
                    currencyId: command.CurrencyId!,
                    minimumOrderAmount: command.MinimumOrderAmount,
                    maxRedemptionsPerUser: command.MaxRedemptionsPerUser,
                    globalMaxRedemptions: command.GlobalMaxRedemptions,
                    validFrom: command.ValidFrom,
                    validUntil: command.ValidUntil,
                    description: command.Description);

            promoCodeRepository.Add(entity);

            return BusinessResult.Success(new Response(entity.Id));
        }
    }
}
