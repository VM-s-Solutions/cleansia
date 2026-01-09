using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

public class UpdateCurrency
{
    public record Command(
        string CurrencyId,
        string Code,
        string Symbol,
        string Name,
        decimal ExchangeRate) : ICommand<Response>;

    public record Response(string Id);

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
                .WithMessage(BusinessErrorMessage.CurrencyNotFound);

            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(3)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(async (command, code, ct) =>
                {
                    var existing = await currencyRepository.GetByCodeAsync(code, ct);
                    return existing == null || existing.Id == command.CurrencyId;
                })
                .WithMessage(BusinessErrorMessage.CurrencyCodeAlreadyExists);

            RuleFor(x => x.Symbol)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(5)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.ExchangeRate)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.ExchangeRateMustBePositive);
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

            currency.Update(command.Code, command.Symbol, command.Name, command.ExchangeRate);

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}