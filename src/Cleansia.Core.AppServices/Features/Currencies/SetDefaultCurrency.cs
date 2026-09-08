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
            if (currency is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.CurrencyNotFound));
            }

            if (currency.IsDefault)
            {
                return BusinessResult.Success(new Response(currency.Id));
            }

            // THE DEFAULT CURRENCY IS THE PRICING CURRENCY, so it may only ever be one the catalogue is
            // actually priced in. Until per-currency price tables exist (Wave B) the catalogue carries a
            // single unlabelled set of numbers, and the pricing calculator no longer scales them by an
            // exchange rate — so promoting a second currency here would charge the CZK figures under its
            // code. On the seeded basket that is roughly a 25x overcharge, on the card and on the fiscal
            // receipt, from one star icon in Admin -> Currencies.
            //
            // `IsActive` is the gate, and this is its FIRST reader anywhere in the platform: no
            // repository predicate, no query filter and no endpoint consulted it before. Wave B replaces
            // the condition with "has price rows in this currency" and activates EUR in the same commit
            // that gives it those rows.
            if (!currency.IsActive)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.InvalidCurrency));
            }

            var previousDefault = await currencyRepository.GetDefaultAsync(cancellationToken);
            previousDefault.SetAsDefault(false);
            currency.SetAsDefault(true);

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}
