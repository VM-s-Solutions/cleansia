using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// <b>The single seam through which money leaves via Stripe.</b> Every refund flows through here.
///
/// <para>It clamps to the refundable ceiling, calls Stripe with the deterministic key, and records the
/// projection and status transition <b>only after Stripe confirms</b>. A concurrent double-issue
/// collapses on the unique refund-key index. It does NOT tell the customer — that is the caller's — and does
/// NOT enforce the refund window; the seam enforces only the ceiling and idempotency. The one notice it raises
/// is to administrators: when a retry finds Stripe failed or canceled the refund made on its key, it raises
/// <c>admin.payment.refund_needs_retry</c>, once per order, in its own commit with the close.
/// → /flows/cancellation-refund-dispute#refund</para>
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// A retry of a pending refund that the refunds Stripe confirmed have left nothing on the card for is closed
    /// as <see cref="RefundStatus.Failed"/> for the caller to commit, and fails as <c>refund.nothing_refundable</c>.
    /// A retry that finds Stripe failed or canceled the refund made on its key closes it as
    /// <see cref="RefundStatus.Failed"/> and raises <c>admin.payment.refund_needs_retry</c>, once per order, in
    /// its own commit, and fails as <c>refund.failed</c>.
    /// </summary>
    Task<BusinessResult<RefundResult>> IssueRefundAsync(RefundRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Re-drive a refund Stripe refused or could not be reached for, on the idempotency key it was
    /// created with, so a refund Stripe did take is never taken twice. Clamped to the live ceiling, as a
    /// retried <see cref="IssueRefundAsync"/> is; a refund with nothing left to give back is closed as
    /// <see cref="RefundStatus.Failed"/> for the caller to commit, unless only other refunds still pending
    /// left it nothing, in which case it stays pending and fails as <c>refund.failed</c>. One Stripe failed or
    /// canceled on its key is closed as <see cref="RefundStatus.Failed"/> and raises
    /// <c>admin.payment.refund_needs_retry</c>, once per order, in its own commit, and fails as <c>refund.failed</c>.
    /// </summary>
    Task<BusinessResult<RefundResult>> RedriveAsync(string refundId, string actorId, CancellationToken cancellationToken);
}

/// <summary>
/// The refund the caller wants issued (ADR-0006 D1). <see cref="Amount"/> is the caller-computed
/// amount (window + fee-bearer policy already applied caller-side); the seam clamps it to the
/// refundable ceiling. Authorization is already checked by the caller (ADR-0006 D6).
/// </summary>
public sealed record RefundRequest(
    string OrderId,
    decimal Amount,
    RefundReason Reason,
    string ActorId,
    string? DisputeId = null,
    string? RefundRequestId = null,
    // Audit-only passthrough (ADR-0009 D1): the seam forwards this to the Refund row, it never reads,
    // validates, or branches on it. The window decision stays caller-side in RefundPolicy/the handler.
    string? WindowOverrideReason = null);

/// <summary>
/// Outcome of a confirmed refund. <see cref="Amount"/> is the amount Stripe accepted (the clamped
/// amount), which on a resolve-to-existing is the already-recorded refund's amount.
/// <see cref="ResolvedToExisting"/> is true when the call collapsed onto an existing refund for the
/// same key (a retry/redelivery or the loser of a concurrent double-issue) — no second Stripe refund
/// was issued. <see cref="CreditReturned"/> is the credit leg the same refund put back on the
/// customer's balance, read from its ledger row; the card and credit legs together are what moved.
/// </summary>
public sealed record RefundResult(
    string RefundId,
    string RefundKey,
    decimal Amount,
    RefundStatus Status,
    bool ResolvedToExisting,
    decimal CreditReturned = 0m);
