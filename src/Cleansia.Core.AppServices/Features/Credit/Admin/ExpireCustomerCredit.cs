using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Credit.Admin;

/// <summary>
/// Discharge a customer's whole credit balance now, before it expires on its own.
///
/// <para><b>This is how a customer who wants to leave actually gets to leave.</b> Erasure is refused
/// while a balance is positive (owner ruling 2026-09-05, GdprDeletionService), and Cleansia does not
/// do Stripe payouts — so without this the platform had a customer it could neither pay nor erase.
/// They ask, an admin discharges the balance, and the erasure proceeds.</para>
///
/// <para>The movement and its ledger reason are the same as the nightly sweep's, because they ARE the
/// same thing: money leaving because it will not be spent. The note says which it was, and it is
/// required for exactly that reason — a row that says only "Expired" on a date the sweep did not run
/// is a mystery to whoever reads it next.</para>
///
/// <para>It only ever takes the WHOLE balance. A partial discharge would need a rule for what the
/// remainder is now for, and there is no case that wants one: either the customer is leaving, or they
/// are not.</para>
/// </summary>
[AuditAction("credit.expire", Sensitive = true, ResourceType = "CreditAccount")]
public class ExpireCustomerCredit
{
    /// <param name="RequestId">
    /// S7a. Persisted as the ledger row's <c>IdempotencyKey</c>, where a plain unique index collapses
    /// a double-submit. Less load-bearing here than on the issue path — draining an empty balance is
    /// already a no-op — but the shape stays the same as its sibling so neither is the odd one out.
    /// </param>
    public record Command(string UserId, string Note, string RequestId) : ICommand<Response>;

    public record Response(string UserId, decimal AmountExpired);

    private record BalanceSnapshot(string UserId, decimal Balance);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.UserNotFound);

            // Required, and the only record of why money the company owed stopped being owed.
            RuleFor(x => x.Note)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.RequestId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(120)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        ICreditAccountRepository creditAccountRepository,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(
            Command command, CancellationToken cancellationToken)
        {
            var actorId = userSessionProvider.GetUserId() ?? string.Empty;

            // EVERY account, because a customer's credit is now per currency and this command's job is
            // to leave nothing owed. Reading one account meant discharging an arbitrary one — and since
            // the GDPR erasure gate refuses while ANY balance is positive, discharging the wrong one
            // would leave erasure permanently blocked with nothing on the admin's screen to act on.
            var accounts = await creditAccountRepository.GetAllForUserAsync(
                command.UserId, cancellationToken);
            var funded = accounts.Where(a => a.Balance > 0m).ToList();

            // No account, or nothing on any of them. Both are SUCCESS with zero: the admin's intent —
            // "make this balance not block anything" — is already true, and an error here would send
            // them hunting for a problem that does not exist.
            if (funded.Count == 0)
            {
                return BusinessResult.Success(new Response(command.UserId, 0m));
            }

            // MORE THAN ONE FUNDED BALANCE MEANS MORE THAN ONE CURRENCY — the unique index allows only
            // one account per currency — and this command cannot express that outcome. Response carries
            // a single unlabelled decimal, so draining both would report 200 CZK + 50 EUR as "250", in
            // the admin's confirmation AND in the audit record of money being destroyed. A number with
            // no unit is not a number.
            //
            // So it REFUSES rather than guesses. Reachable from the admin screen since IssueCustomerCredit
            // names its currency: an admin can fund a second account beside the first. The refusal is
            // what makes that a visible gap rather than a silent miscount; a per-currency discharge --
            // a CurrencyId on this command -- is the follow-up, and until it lands a customer holding
            // two funded balances cannot be discharged from here.
            if (funded.Count > 1)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.UserId), BusinessErrorMessage.CreditHeldInMultipleCurrencies));
            }

            var account = funded[0];

            var balanceBefore = account.Balance;
            var taken = account.Drain(actorId, DateTimeOffset.UtcNow);
            account.RecordExpiry(taken, command.RequestId, actorId, command.Note);

            auditContext.RecordChange(
                "CreditAccount",
                account.Id,
                new BalanceSnapshot(command.UserId, balanceBefore),
                new BalanceSnapshot(command.UserId, 0m),
                command.Note);

            return BusinessResult.Success(new Response(command.UserId, taken));
        }
    }
}
