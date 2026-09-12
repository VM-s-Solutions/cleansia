using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

/// <summary>
/// Switches a market OFF. On a Currency, <c>IsActive</c> is not the soft-delete flag it is on services
/// and packages -- <c>DeleteCurrency</c> hard-deletes -- it is whether the platform sells in the
/// currency: the catalogue price rule stops requiring it, the booking path stops accepting it and
/// <see cref="SetDefaultCurrency"/> refuses to promote it.
///
/// <para>Everything already denominated in it -- orders, pay rates, invoices, credit, price rows -- is
/// untouched, which is why an IN-USE currency may be switched off (the same contract as
/// <see cref="Services.DeactivateService"/>: <c>IsInUseAsync</c> is never consulted) and why the
/// DEFAULT may not: the default is what every quote falls back to, and an inactive default is exactly
/// the empty-catalogue state this switch exists to prevent. The deactivating admin is recorded via
/// the auditable domain primitive. Idempotent.</para>
/// </summary>
public class DeactivateCurrency
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
                .WithMessage(BusinessErrorMessage.CurrencyNotFound)
                .MustAsync(async (id, ct) =>
                {
                    var currency = await currencyRepository.GetByIdAsync(id, ct);
                    return currency is null || !currency.IsDefault;
                })
                .WithMessage(BusinessErrorMessage.CannotDeactivateDefaultCurrency);
        }
    }

    public class Handler(
        ICurrencyRepository currencyRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var currency = await currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);
            if (currency is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.CurrencyNotFound));
            }

            // Re-checked here as well as in the validator, like DeleteCurrency: the star can move
            // between the two reads, and an inactive default is the one state nothing may create.
            if (currency.IsDefault)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CurrencyId), BusinessErrorMessage.CannotDeactivateDefaultCurrency));
            }

            if (!currency.IsActive)
            {
                return BusinessResult.Success(new Response(currency.Id));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            currency.Deactivated(actorId, DateTimeOffset.UtcNow);

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}
