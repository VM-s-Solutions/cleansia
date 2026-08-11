#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Employees;

/// <summary>
/// The cleaner's own payout destination (ADR-0034). This is the <b>single</b> write path for
/// <see cref="EmployeePayoutDetails"/>: it is a create-or-update keyed on the employee, so the
/// <c>(TenantId, EmployeeId)</c> unique index never has to arbitrate, and it is the only place
/// <c>Employee.HasPayoutDetails</c> is raised.
/// </summary>
public class UpdateBankDetails
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly IPayoutDetailsValidator _payoutDetailsValidator;

        public Validator(
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider,
            IPayoutDetailsValidator payoutDetailsValidator)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));
            _payoutDetailsValidator = payoutDetailsValidator ?? throw new ArgumentNullException(nameof(payoutDetailsValidator));

            RuleFor(c => c)
                .MustAsync(CallerIsAnEmployee)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateEmployee);

            RuleFor(c => c.BankName)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(c => c.HolderName)
                .MaximumLength(200)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(c => c).CustomAsync(ValidatePayoutDetails);
        }

        private async Task ValidatePayoutDetails(Command command, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            var employee = await ResolveCallerAsync(cancellationToken);
            if (employee is null)
            {
                return;
            }

            var result = await _payoutDetailsValidator.ValidateAsync(ToInput(command, employee.WorkCountryId), cancellationToken);
            if (result.IsValid)
            {
                return;
            }

            // The service returns the specific key (bad checksum vs. wrong bank code vs. a card number),
            // and the client has to be able to tell them apart or the cleaner just retypes the same value.
            context.AddFailure(new ValidationFailure(nameof(Command.AccountNumber), result.ErrorKey)
            {
                ErrorCode = nameof(Command.AccountNumber),
            });
        }

        // Not an ownership comparison — the subject is server-resolved, so there is nothing for a client
        // to get wrong. What survives is the precondition the handler dereferences.
        private async Task<bool> CallerIsAnEmployee(Command command, CancellationToken cancellationToken) =>
            await ResolveCallerAsync(cancellationToken) is not null;

        private Task<Employee?> ResolveCallerAsync(CancellationToken cancellationToken) =>
            _employeeRepository.GetByUserEmailAsync(
                _userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);
    }

    /// <param name="Iban">
    /// Optional and cross-checked, never collected as the source of truth: for a domestic scheme the
    /// server derives it from the parts and rejects a supplied value that disagrees (ADR-0034 D5.2).
    /// </param>
    public record Command(
        // [OWN-DATA] (S1): inert. The record written is always the JWT caller's; this stays on the wire
        // only so the shipped clients keep serializing unchanged. Nullable is load-bearing — a
        // non-nullable reference member makes MVC reject an ABSENT id before MediatR is reached.
        string? EmployeeId,
        string? Iban,
        string? BankCountryId = null,
        string? AccountPrefix = null,
        string? AccountNumber = null,
        string? BankCode = null,
        string? Swift = null,
        string? BankName = null,
        string? HolderName = null) : ICommand<Response>;

    public record Response(string EmployeeId);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider,
        IEmployeePayoutDetailsRepository payoutDetailsRepository,
        IPayoutDetailsValidator payoutDetailsValidator,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByUserEmailAsync(
                userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(BusinessErrorMessage.EmployeeNotFound), BusinessErrorMessage.EmployeeNotFound));
            }

            var validation = await payoutDetailsValidator.ValidateAsync(
                ToInput(command, employee.WorkCountryId), cancellationToken);

            if (validation.Canonical is not { } canonical)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.AccountNumber), validation.ErrorKey ?? BusinessErrorMessage.PayoutCountryNotSupported));
            }

            var existing = await payoutDetailsRepository.GetByEmployeeIdAsync(employee.Id, cancellationToken);
            if (existing is null)
            {
                payoutDetailsRepository.Add(EmployeePayoutDetails.Create(
                    employee.Id,
                    canonical.Scheme,
                    canonical.BankCountryId,
                    PayoutDetailsStatus.Provided,
                    canonical.AccountPrefix,
                    canonical.AccountNumber,
                    canonical.BankCode,
                    canonical.Iban,
                    canonical.Swift,
                    canonical.BankName,
                    canonical.HolderName,
                    confirmedAt: DateTime.UtcNow));
            }
            else
            {
                existing.Update(
                    canonical.Scheme,
                    canonical.BankCountryId,
                    PayoutDetailsStatus.Provided,
                    canonical.AccountPrefix,
                    canonical.AccountNumber,
                    canonical.BankCode,
                    canonical.Iban,
                    canonical.Swift,
                    canonical.BankName,
                    canonical.HolderName,
                    confirmedAt: DateTime.UtcNow);
            }

            employee.MarkPayoutDetailsProvided();

            // The payout invoice still renders the supplier block from Employee.IBAN
            // (FileExtensions.CreateSupplierData); mirroring the derived value keeps the printed account
            // in step with the record until T-0522 moves that read onto the payout row.
            employee.UpdateBankDetails(canonical.Iban);

            if (await payoutDetailsRepository.IsIbanUsedByAnotherEmployeeAsync(canonical.Iban, employee.Id, cancellationToken))
            {
                logger.LogWarning(
                    "Payout destination for employee {EmployeeId} is shared with another employee", employee.Id);
            }

            return BusinessResult.Success(new Response(employee.Id));
        }
    }

    private static PayoutDetailsInput ToInput(Command command, string? workCountryId) => new(
        command.BankCountryId,
        workCountryId,
        command.AccountPrefix,
        command.AccountNumber,
        command.BankCode,
        command.Iban,
        command.Swift,
        command.BankName,
        command.HolderName);
}
