using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.CashHeld;

/// <summary>
/// An administrator records cash a cleaner handed back to the company (owner ruling 2026-09-28, decision
/// 23), which takes it off what the cleaner holds in that currency.
/// </summary>
[AuditAction("cash_held.remittance", ResourceType = "Employee")]
public class RecordCashRemittance
{
    public record Command(string EmployeeId, string CurrencyId, decimal Amount, string? Note) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        private readonly ICashLedgerRepository _cashLedgerRepository;

        public Validator(
            IEmployeeRepository employeeRepository,
            ICurrencyRepository currencyRepository,
            ICashLedgerRepository cashLedgerRepository)
        {
            _cashLedgerRepository = cashLedgerRepository;

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(currencyRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CurrencyNotFound);

            RuleFor(x => x.Amount)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0m)
                .WithMessage(BusinessErrorMessage.CashHeldAmountInvalid)
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage(BusinessErrorMessage.CashHeldAmountInvalid)
                .MustAsync(NotExceedTheCashHeldAsync)
                .WithMessage(BusinessErrorMessage.CashHeldAmountExceedsBalance);

            RuleFor(x => x.Note)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> NotExceedTheCashHeldAsync(Command command, decimal amount, CancellationToken cancellationToken)
        {
            var balances = await _cashLedgerRepository.GetBalancesAsync(command.EmployeeId, cancellationToken);
            return amount <= balances.Where(b => b.CurrencyId == command.CurrencyId).Sum(b => b.Amount);
        }
    }

    public class Handler(ICashLedgerRepository cashLedgerRepository, TimeProvider timeProvider)
        : ICommandHandler<Command, Response>
    {
        public Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var entry = CashLedgerEntry.ForRemittance(
                command.EmployeeId, command.CurrencyId, command.Amount, command.Note, timeProvider.GetUtcNow().UtcDateTime);
            cashLedgerRepository.Add(entry);

            return Task.FromResult(BusinessResult.Success(new Response(entry.Id)));
        }
    }
}
