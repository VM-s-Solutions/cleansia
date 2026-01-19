using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

public class DeleteCurrency
{
    public record Command(string CurrencyId) : ICommand<Response>;

    public record Response(bool Success);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) =>
                    await currencyRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CurrencyNotFound)
                .MustAsync(async (id, ct) =>
                {
                    var currency = await currencyRepository.GetByIdAsync(id, ct);
                    return currency == null || !currency.IsDefault;
                })
                .WithMessage(BusinessErrorMessage.CannotDeleteDefaultCurrency)
                .MustAsync(async (id, ct) =>
                    !await currencyRepository.IsInUseAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CurrencyInUse);
        }
    }

    internal class Handler(ICurrencyRepository currencyRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var currency = await currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);

            if (currency is null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.CurrencyId), BusinessErrorMessage.CurrencyNotFound));
            }

            if (currency.IsDefault)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.CurrencyId), BusinessErrorMessage.CannotDeleteDefaultCurrency));
            }

            var isInUse = await currencyRepository.IsInUseAsync(command.CurrencyId, cancellationToken);
            if (isInUse)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.CurrencyId), BusinessErrorMessage.CurrencyInUse));
            }

            currencyRepository.Remove(currency);

            return BusinessResult.Success(new Response(true));
        }
    }
}