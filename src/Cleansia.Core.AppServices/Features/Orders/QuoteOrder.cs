using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

public class QuoteOrder
{
    public record Command(
        IEnumerable<string> SelectedServiceIds,
        IEnumerable<string> SelectedPackageIds,
        int Rooms,
        int Bathrooms,
        string? CurrencyId) : ICommand<Response>;

    public record Response(
        decimal TotalPrice,
        string CurrencyId,
        string CurrencyCode,
        decimal ServicesSubtotal,
        decimal PackagesSubtotal,
        decimal ExchangeRate);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.Rooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.Bathrooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.SelectedServiceIds)
                .MustAsync(serviceRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices);

            RuleFor(x => x.SelectedPackageIds)
                .MustAsync(packageRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage);

            When(x => !string.IsNullOrEmpty(x.CurrencyId), () =>
            {
                RuleFor(x => x.CurrencyId!)
                    .MustAsync(currencyRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.InvalidCurrency);
            });
        }
    }

    public class Handler(IOrderPricingCalculator pricingCalculator)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var result = await pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                command.Rooms,
                command.Bathrooms,
                command.CurrencyId,
                cancellationToken);

            // NOTE: this handler does NOT yet apply membership discounts. The
            // Quote endpoint is [AllowAnonymous] and Command does not carry a
            // UserId — to surface a Plus-discounted live quote we'd need to
            // either (a) plumb UserId into Command + enrich it from JWT in the
            // controller, or (b) split into a separate authenticated quote
            // endpoint. Both are deferred until the Plus product actually
            // launches; until then, MembershipPlans is empty and the quote
            // path matches CreateOrder's authoritative price exactly.

            return BusinessResult.Success(new Response(
                TotalPrice: result.TotalPrice,
                CurrencyId: result.CurrencyId,
                CurrencyCode: result.CurrencyCode,
                ServicesSubtotal: result.ServicesSubtotal,
                PackagesSubtotal: result.PackagesSubtotal,
                ExchangeRate: result.ExchangeRate));
        }
    }
}
