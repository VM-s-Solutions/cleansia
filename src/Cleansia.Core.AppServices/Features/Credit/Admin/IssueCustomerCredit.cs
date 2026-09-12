using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Credit.Admin;

/// <summary>
/// An admin puts money on a customer's credit balance.
///
/// <para><b>This is the only way credit is created, and it is deliberately human-only.</b> Owner
/// ruling 2026-09-05: "person in loop 100%" — no dispute resolution, no cancellation and no job of
/// any kind issues credit on its own. A dispute that deserves compensation ends with a person
/// deciding the number and calling this.</para>
/// </summary>
[AuditAction("credit.issue", Sensitive = true, ResourceType = "CreditAccount")]
public class IssueCustomerCredit
{
    /// <summary>
    /// <paramref name="RequestId"/> (S7a) is a REQUIRED client-generated idempotency token, persisted
    /// as the ledger row's <c>IdempotencyKey</c>. <c>CreditTransactions.IdempotencyKey</c> carries a
    /// plain UNIQUE index — not a tenant-prefixed one — so a double-submit, a proxy retry or an
    /// impatient second click raises 23505 and grants the money once.
    ///
    /// <para><paramref name="DisputeId"/> and <paramref name="OrderId"/> are optional provenance: what
    /// this credit was for. They are not FKs and are not validated against those tables, deliberately
    /// — the ledger's job is to record what an admin said they were compensating, and a dispute or an
    /// order can be erased under GDPR while the money movement must survive.</para>
    /// </summary>
    /// <remarks>
    /// <paramref name="CurrencyId"/> is REQUIRED and names the unit of <paramref name="Amount"/>. It
    /// used to be resolved to the platform default inside the handler while the admin dialog labelled
    /// the amount with the customer's largest balance's currency -- so the label and the write could
    /// disagree the moment a customer held a non-default balance. The admin names it now, and the
    /// grant lands in THAT currency's account, opening one if the customer has none in it.
    /// </remarks>
    public record Command(
        string UserId,
        decimal Amount,
        string CurrencyId,
        CreditTransactionReason Reason,
        string Note,
        string RequestId,
        string? OrderId = null,
        string? DisputeId = null) : ICommand<Response>;

    public record Response(string UserId, decimal Amount, decimal NewBalance);

    private record BalanceSnapshot(string UserId, decimal Balance);

    public class Validator : AbstractValidator<Command>
    {
        /// <summary>
        /// A ceiling on one manual grant. Not a business rule — a typo guard, in the same spirit as
        /// <c>GrantPointsManually</c>'s 100k points cap. An admin who genuinely owes a customer more
        /// than this issues it twice, and both rows are in the ledger under their name.
        ///
        /// <para><b>Unit-free on purpose.</b> It caps the NUMBER typed, in whatever currency the grant
        /// is denominated in: CZK-sized (≈ 400 EUR), so in EUR it is 25× looser and catches almost
        /// nothing — accepted, because a per-currency table for a typo guard is worse than the typo
        /// (→ /product/business-rules#money-constants). The client mirrors it as <c>AMOUNT_MAX</c> in
        /// the issue-credit dialog.</para>
        /// </summary>
        public const decimal SanityCap = 10_000m;

        public Validator(IUserRepository userRepository, ICurrencyRepository currencyRepository)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.UserNotFound);

            RuleFor(x => x.Amount)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0m)
                .WithMessage(BusinessErrorMessage.MustBePositive)
                .LessThanOrEqualTo(SanityCap)
                .WithMessage(BusinessErrorMessage.CreditAmountExceedsSanityCap)
                // Whole minor units only. The balance is spent against Stripe, which takes integer
                // cents, so a third of a cent on the balance is money that can never be spent and
                // never quite reconciles.
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage(BusinessErrorMessage.CreditAmountNotWholeMinorUnits);

            // The unit of the amount. Must exist and be ACTIVE: credit is spendable only on an order in
            // the same currency, and an order can only be placed in a currency the platform operates
            // in, so a grant in a switched-off one is money the customer could never spend. Same two
            // keys, in the same order, as SetDefaultCurrency.
            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(currencyRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.CurrencyNotFound)
                .MustAsync(async (id, ct) =>
                {
                    var currency = await currencyRepository.GetByIdAsync(id, ct);
                    return currency is not null && currency.IsActive;
                })
                .WithMessage(BusinessErrorMessage.InvalidCurrency);

            // Reason is server-owned and closed. An out-of-range int on the wire would otherwise be
            // written to the column verbatim and read back as an enum value that does not exist.
            RuleFor(x => x.Reason)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue)
                // OrderPayment is the one genuinely spend-side reason: it describes money LEAVING the
                // balance, and a positive row carrying it would read as "the customer was credited for
                // paying us". OrderPaymentReturned is NOT spend-side despite the name - it is the
                // reason the automatic return path writes, and an admin correcting a return by hand
                // (a refund that failed to give the credit back, say) needs to write the same reason
                // rather than filing it as Goodwill and losing the provenance.
                .Must(reason => reason != CreditTransactionReason.OrderPayment)
                .WithMessage(BusinessErrorMessage.CreditReasonNotIssuable);

            // Free text, and the ONLY record of why a person decided this. Required for the same
            // reason GrantPointsManually requires one.
            RuleFor(x => x.Note)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            // S7a — present, and bounded to the persisted IdempotencyKey column width (120).
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

            // THE CURRENCY THE ADMIN NAMED, validated above as real and operated. The grant lands in
            // that currency's account, opening one if the customer has none in it. The lookup is keyed
            // on the currency on purpose: it previously returned whatever account existed and added a
            // default-currency number to it, which is how a CZK refund could land on a EUR balance.
            var account = await creditAccountRepository.EnsureForUserAsync(
                command.UserId, command.CurrencyId, cancellationToken);

            var balanceBefore = account.Balance;

            // Increases go through the tracked graph, not the conditional UPDATE. Two concurrent
            // grants both increase the balance and both are correct; the direction that needs the
            // database to arbitrate is spending, and it does not live here.
            account.Issue(
                amount: command.Amount,
                reason: command.Reason,
                idempotencyKey: command.RequestId,
                issuedBy: actorId,
                orderId: command.OrderId,
                disputeId: command.DisputeId,
                note: command.Note);

            auditContext.RecordChange(
                "CreditAccount",
                account.Id,
                new BalanceSnapshot(command.UserId, balanceBefore),
                new BalanceSnapshot(command.UserId, account.Balance),
                command.Note);

            return BusinessResult.Success(new Response(
                UserId: command.UserId,
                Amount: command.Amount,
                NewBalance: account.Balance));
        }
    }
}
