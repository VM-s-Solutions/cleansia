using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

/// <summary>
/// Promotes a currency to the platform default. The previous default is cleared in the same unit
/// of work (single pipeline commit), preserving the exactly-one-default invariant the delete
/// protection (CannotDeleteDefaultCurrency) and overview sort rely on. Mirrors
/// SetDefaultSavedAddress's clear-then-set. Idempotent on the current default.
/// </summary>
public class SetDefaultCurrency
{
    public record Command(string CurrencyId) : ICommand<Response>;

    public record Response(string CurrencyId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(currencyRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CurrencyNotFound);
        }
    }

    public class Handler(ICurrencyRepository currencyRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var currency = await currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);

            if (currency!.IsDefault)
            {
                return BusinessResult.Success(new Response(currency.Id));
            }

            var previousDefault = await currencyRepository.GetDefaultAsync(cancellationToken);
            previousDefault.SetAsDefault(false);
            currency.SetAsDefault(true);

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}
