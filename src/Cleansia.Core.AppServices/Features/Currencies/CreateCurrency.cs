using Microsoft.EntityFrameworkCore;
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
        decimal? LoyaltyPointsDivisor = null) : ICommand<Response>;

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

            // Zero is a division by zero and a negative is a negative earn; null is "earns nothing yet".
            RuleFor(x => x.LoyaltyPointsDivisor)
                .GreaterThan(0m)
                .When(x => x.LoyaltyPointsDivisor.HasValue)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }
    }

    internal class Handler(ICurrencyRepository currencyRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var currency = Currency.Create(command.Code, command.Symbol, command.Name);
            currency.SetLoyaltyPointsDivisor(command.LoyaltyPointsDivisor);

            currencyRepository.Add(currency);

            // The validator's ExistsWithCodeAsync and this insert cross a snapshot boundary with no
            // lock, so IX_Currencies_Code_Unique is what actually arbitrates two simultaneous
            // creations. FLUSH here and own the loser's 23505: the pipeline commit runs after this
            // handler returns, where the same violation can only surface as a 500 -- and it surfaces as
            // a PLAIN TEXT 500 that the client's error interceptor cannot read, so the admin gets
            // "An error occurred" while the correct key sits translated in all five locales. Same shape
            // as CreateAdminUser.
            try
            {
                await currencyRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(Command.Code), BusinessErrorMessage.CurrencyCodeAlreadyExists));
            }

            return BusinessResult.Success(new Response(currency.Id));
        }
    }
}