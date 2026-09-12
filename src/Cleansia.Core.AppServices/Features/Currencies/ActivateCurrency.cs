using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

/// <summary>
/// Reverses <see cref="DeactivateCurrency"/>, and is the ONLY writer of <c>IsActive = true</c> on a
/// Currency -- <c>Currency.Create</c> makes one switched off.
///
/// <para>Switching on is deliberate because of what it changes: from this commit every catalogue save
/// must price the currency (<c>MustCoverAllActiveCurrencies</c>), the booking path accepts it once
/// anything is priced in it, and it becomes promotable on the same condition. It is NOT gated on
/// prices -- an active, unpriced currency is simply not offerable, and the price rule is the forcing
/// function that gets it priced. The last DeactivatedBy/DeactivatedOn trail is kept. Idempotent.</para>
/// </summary>
public class ActivateCurrency
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

            currency.IsActive = true;

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}
