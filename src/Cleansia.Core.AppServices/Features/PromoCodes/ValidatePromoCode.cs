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
        decimal OrderSubtotal) : ICommand<Response>;

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
            var defaultCurrency = await currencyRepository.GetDefaultAsync(cancellationToken);

            var preview = await promoCodeService.PreviewAsync(
                command.Code,
                userId,
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
