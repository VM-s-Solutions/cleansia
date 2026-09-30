using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Disputes;

[AuditAction("dispute.resolve", Sensitive = true, ResourceType = "Dispute")]
public class ResolveDispute
{
    public record ResolutionSnapshot(
        string DisputeId,
        DisputeStatus Status,
        decimal? RefundAmount,
        decimal? CardRefundedAmount,
        decimal? CreditReturnedAmount,
        string? ChargedEmployeeId = null,
        decimal? CleanerChargeAmount = null);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DisputeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.ResolutionNotes)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(x => x.RefundAmount)
                .GreaterThanOrEqualTo(0)
                .When(x => x.RefundAmount.HasValue)
                .WithMessage(BusinessErrorMessage.InvalidRefundAmount);

            When(x => x.ChargeToCleaner is not null, () =>
            {
                RuleFor(x => x.ChargeToCleaner!.EmployeeId)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required);

                RuleFor(x => x.ChargeToCleaner!.Amount)
                    .Cascade(CascadeMode.Stop)
                    .GreaterThan(0m)
                    .WithMessage(BusinessErrorMessage.MustBePositive)
                    // Deduction and total are stored to the cent each; a fraction rounds them apart and
                    // the invoice line no longer adds up.
                    .Must(amount => decimal.Round(amount, 2) == amount)
                    .WithMessage(BusinessErrorMessage.DisputeCleanerChargeNotWholeMinorUnits);

                RuleFor(x => x.ChargeToCleaner!.Reason)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required)
                    .MaximumLength(500)
                    .WithMessage(BusinessErrorMessage.MaxLengthExceeded);
            });
        }
    }

    /// <summary>
    /// <paramref name="RefundAmount"/> is the settlement: a card refund, or credit when the customer chose
    /// credit on filing. The administrator decides the amount, never the tender (owner ruling 2026-09-28).
    /// </summary>
    public record Command(
        string DisputeId,
        decimal? RefundAmount,
        string ResolutionNotes,
        CleanerCharge? ChargeToCleaner = null
    ) : ICommand;

    /// <summary>
    /// The finding that a named cleaner was at fault: a deduction from their pay on the disputed order,
    /// linked to the dispute, with the reason shown to them on that pay record. Only ever an
    /// administrator's explicit finding; a refund alone never touches a cleaner's pay (owner ruling
    /// 2026-09-28).
    /// </summary>
    public record CleanerCharge(string? EmployeeId, decimal Amount, string? Reason);

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider,
        IRefundService refundService,
        IRefundRepository refundRepository,
        ICreditAccountRepository creditAccountRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        INotificationProducer notificationProducer,
        IAuditContext auditContext) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetForUpdateAsync(request.DisputeId, cancellationToken);

            if (dispute == null)
            {
                return BusinessResult.Failure(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var before = new ResolutionSnapshot(
                dispute.Id, dispute.Status, dispute.RefundAmount, dispute.CardRefundedAmount, dispute.CreditReturnedAmount);

            // A terminal dispute (Resolved/Closed) is never re-resolved: a second Resolve would overwrite
            // the recorded RefundAmount/notes of the settled dispute. Resolve owns the Resolved state and
            // does not run through CanTransitionTo, so the terminal check is enforced here at the seam.
            if (dispute.IsTerminal)
            {
                return BusinessResult.Failure(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeAlreadyResolved));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;

            var charge = request.ChargeToCleaner;
            var chargedPay = charge is null
                ? null
                : await orderEmployeePayRepository.GetByOrderAndEmployeeAsync(dispute.OrderId, charge.EmployeeId!, cancellationToken);
            if (charge is not null && chargedPay?.CanTakeDisputeCharge(charge.Amount) != true)
            {
                return BusinessResult.Failure(new Error(
                    nameof(request.ChargeToCleaner), BusinessErrorMessage.DisputeCleanerChargeNotChargeable));
            }

            // The money moves BEFORE the resolution is written. Resolving first and refunding second
            // left a dispute recorded as settled with a RefundAmount the customer never received when
            // Stripe refused: the failure was read only to skip a notification, and the terminal status
            // then blocked every retry.
            RefundResult? refundResult = null;
            decimal? creditSettled = null;
            if (request.RefundAmount is > 0m
                && dispute.SettlementPreference == DisputeSettlementPreference.Credit
                && !string.IsNullOrEmpty(dispute.UserId))
            {
                var settled = await SettleWithCreditAsync(request, dispute, request.RefundAmount.Value, actorId, cancellationToken);
                if (settled.IsFailure)
                {
                    return BusinessResult.Failure(settled.Error!);
                }

                creditSettled = settled.Value ? request.RefundAmount.Value : null;
            }

            if (request.RefundAmount is > 0m && creditSettled is null)
            {
                var refundRequest = await HeldToWhatIsLeftAsync(
                    new RefundRequest(
                        dispute.OrderId,
                        request.RefundAmount.Value,
                        RefundReason.DisputeResolution,
                        actorId,
                        DisputeId: dispute.Id),
                    dispute,
                    cancellationToken);
                if (refundRequest is null)
                {
                    return BusinessResult.Failure(new Error(nameof(request.RefundAmount), BusinessErrorMessage.RefundNothingRefundable));
                }

                var refund = await refundService.IssueRefundAsync(refundRequest, cancellationToken);

                if (refund.IsFailure)
                {
                    return BusinessResult.Failure(refund.Error!);
                }

                refundResult = refund.Value!;
            }

            dispute.Resolve(
                resolvedBy: actorId,
                refundAmount: request.RefundAmount,
                resolutionNotes: request.ResolutionNotes,
                cardRefundedAmount: refundResult?.Amount,
                creditReturnedAmount: creditSettled ?? refundResult?.CreditReturned
            );

            if (charge is not null)
            {
                chargedPay!.ChargeForDispute(dispute.Id, charge.Amount, charge.Reason!);
            }

            auditContext.RecordChange(
                "Dispute",
                dispute.Id,
                before,
                new ResolutionSnapshot(
                    dispute.Id, dispute.Status, dispute.RefundAmount, dispute.CardRefundedAmount, dispute.CreditReturnedAmount,
                    ChargedEmployeeId: charge?.EmployeeId,
                    CleanerChargeAmount: charge?.Amount));

            if (refundResult is not null && !string.IsNullOrEmpty(dispute.UserId))
            {
                await notificationProducer.NotifyAsync(
                    dispute.UserId,
                    NotificationEventCatalog.OrderRefunded,
                    new Dictionary<string, string>
                    {
                        ["orderId"] = dispute.OrderId,
                        // Display-only, resolved AFTER the Stripe refund settled: a missing
                        // Order must degrade to the factory's tolerated empty loc-arg, never
                        // throw and unwind the resolution while the money already moved.
                        ["orderNumber"] = dispute.Order?.DisplayOrderNumber ?? string.Empty,
                        ["disputeId"] = dispute.Id,
                    },
                    // The dedup subject is the REFUND, not the order. Three handlers raise this one
                    // event — an admin refund, an admin cancellation that refunds, and a dispute
                    // resolved with a refund — and all three keyed it on the order, so the second
                    // refund an order ever saw minted a key the first had written. The outbox's unique
                    // index raises that at the pipeline's commit, AFTER the Stripe refund has already
                    // settled: the money left, the transaction rolled back, and the customer was never
                    // told. RefundResult.RefundId is stable per refund and the service already resolves
                    // a repeat to the existing one, so a genuinely duplicate notice still collapses.
                    dispute.TenantId,
                    refundResult.RefundId,
                    cancellationToken);
            }

            return BusinessResult.Success();
        }

        /// <summary>
        /// The customer chose credit, so the settlement lands on their balance in the order's currency
        /// instead of the card. Keyed on the dispute, which settles once. Bounded by what the order has
        /// not already given back (card refunds, credit returned to the balance, earlier complaints settled
        /// in credit), as the card leg is by its refundable ceiling; otherwise a second dispute on a
        /// refunded order is paid again in credit.
        ///
        /// <para>False when the customer's account is erased: credit there would be forfeited, so the
        /// settlement goes to the card, as it does for a customer who never chose.</para>
        /// </summary>
        private async Task<BusinessResult<bool>> SettleWithCreditAsync(
            Command request,
            Dispute dispute,
            decimal amount,
            string actorId,
            CancellationToken cancellationToken)
        {
            if (dispute.Order is not { } order
                || decimal.Round(amount, 2) != amount
                || amount > await OutstandingAsync(order, cancellationToken))
            {
                return BusinessResult.Failure<bool>(new Error(nameof(request.RefundAmount), BusinessErrorMessage.InvalidRefundAmount));
            }

            var account = await creditAccountRepository.EnsureForUserAsync(dispute.UserId!, order.CurrencyId, cancellationToken);
            if (account is null)
            {
                return BusinessResult.Success(false);
            }

            account.Issue(
                amount: amount,
                reason: CreditTransactionReason.DisputeSettlement,
                idempotencyKey: $"dispute-settlement:{dispute.Id}",
                issuedBy: actorId,
                orderId: dispute.OrderId,
                disputeId: dispute.Id);

            return BusinessResult.Success(true);
        }

        /// <summary>
        /// The card settlement, held to what the order has left once an earlier complaint on it was settled
        /// in credit; null when nothing is left. The seam bounds each tender by what that tender gave back,
        /// and settlement credit is on neither, so the order could otherwise go back once in credit and
        /// again to the card.
        ///
        /// <para>A replay of this dispute's own settled refund passes untouched: what is left already
        /// counts it, and the seam answers the replay with that refund and moves nothing.</para>
        /// </summary>
        private async Task<RefundRequest?> HeldToWhatIsLeftAsync(
            RefundRequest refundRequest, Dispute dispute, CancellationToken cancellationToken)
        {
            if (await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(dispute.OrderId, cancellationToken) <= 0m
                || await refundRepository.GetByRefundKeyAsync(RefundService.BuildRefundKey(refundRequest), cancellationToken)
                    is { Status: RefundStatus.Succeeded })
            {
                return refundRequest;
            }

            var left = dispute.Order is { } order ? await OutstandingAsync(order, cancellationToken) : 0m;
            return left > 0m ? refundRequest with { Amount = Math.Min(refundRequest.Amount, left) } : null;
        }

        private async Task<decimal> OutstandingAsync(Order order, CancellationToken cancellationToken) =>
            order.TotalPrice
            - await refundRepository.GetSucceededRefundTotalForOrderAsync(order.Id, cancellationToken)
            - await creditAccountRepository.GetReturnedTotalForOrderAsync(order.Id, cancellationToken)
            - await creditAccountRepository.GetDisputeSettledTotalForOrderAsync(order.Id, cancellationToken);
    }
}
