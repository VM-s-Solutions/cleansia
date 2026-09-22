using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PromoCodes;

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
        }
    }

    public record Command(
        string Code,
        decimal OrderSubtotal,
        string? CurrencyId = null) : ICommand<Response>;

    public record Response(
        bool IsValid,
        decimal? DiscountAmount,
        string? ErrorCode);

    public class Handler(
        IPromoCodeService promoCodeService,
        ICurrencyRepository currencyRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;

            // The preview must ask the question the create asks: the quote's currency -- the service
            // address's country's -- is what CreateOrder previews in, and a code bound to another
            // currency previews valid against the platform default only to be refused at checkout.
            // Null is a client that quoted with no currency, which resolved to the default.
            var currencyId = string.IsNullOrEmpty(command.CurrencyId)
                ? (await currencyRepository.GetDefaultAsync(cancellationToken))?.Id
                : command.CurrencyId;

            var preview = await promoCodeService.PreviewAsync(
                command.Code,
                userId,
                command.OrderSubtotal,
                currencyId,
                cancellationToken);

            return BusinessResult.Success(new Response(
                IsValid: preview.Success,
                DiscountAmount: preview.Success ? preview.DiscountAmount : null,
                ErrorCode: preview.Error?.ToString()));
        }
    }
}
