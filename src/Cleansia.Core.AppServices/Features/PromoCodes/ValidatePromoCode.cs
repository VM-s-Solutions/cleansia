using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PromoCodes;

/// <summary>
/// Customer-facing validation of a promo code at booking time. Returns the
/// computed discount + a stringified <see cref="PromoCodeError"/> the client
/// maps to its own i18n. Does NOT redeem the code — actual redemption happens
/// inside <c>CreateOrder.Handler</c> so the client cannot tamper.
/// </summary>
public class ValidatePromoCode
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Code)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.OrderSubtotal)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public record Command(
        string Code,
        decimal OrderSubtotal,
        // Enriched server-side from the JWT in the controller — clients pass
        // an empty string and the controller fills it in before forwarding.
        string UserId = "") : ICommand<Response>;

    public record Response(
        bool IsValid,
        decimal? DiscountAmount,
        string? ErrorCode);

    public class Handler(
        IPromoCodeService promoCodeService,
        ICurrencyRepository currencyRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // No order yet — validate against the tenant's default currency.
            // The CreateOrder handler re-validates with the actual order
            // currency when the user submits.
            var defaultCurrency = await currencyRepository.GetDefaultAsync(cancellationToken);

            var preview = await promoCodeService.PreviewAsync(
                command.Code,
                command.UserId,
                command.OrderSubtotal,
                defaultCurrency?.Id,
                cancellationToken);

            return BusinessResult.Success(new Response(
                IsValid: preview.Success,
                DiscountAmount: preview.Success ? preview.DiscountAmount : null,
                ErrorCode: preview.Error?.ToString()));
        }
    }
}
