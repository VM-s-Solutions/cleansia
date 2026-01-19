using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Currencies;

public class CreateCurrency
{
    public record Command(
        string Code,
        string Symbol,
        string Name,
        decimal ExchangeRate) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(3)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(async (code, ct) =>
                    !await currencyRepository.ExistsWithCodeAsync(code, ct))
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
        public Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var currency = Currency.Create(command.Code, command.Symbol, command.Name, command.ExchangeRate);

            currencyRepository.Add(currency);

            return Task.FromResult(BusinessResult.Success(new Response(currency.Id)));
        }
    }
}