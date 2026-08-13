using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// <b>The single seam through which money leaves via Stripe.</b> Every refund flows through here.
///
/// <para>It clamps to the refundable ceiling, calls Stripe with the deterministic key, and records the
/// projection and status transition <b>only after Stripe confirms</b>. A concurrent double-issue
/// collapses on the unique refund-key index. It does NOT enqueue notifications and does NOT enforce the
/// refund window — the seam enforces only the ceiling and idempotency.
/// → /flows/cancellation-refund-dispute#refund</para>
/// </summary>
public interface IRefundService
{
    Task<BusinessResult<RefundResult>> IssueRefundAsync(RefundRequest request, CancellationToken cancellationToken);
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
/// was issued.
/// </summary>
public sealed record RefundResult(
    string RefundId,
    string RefundKey,
    decimal Amount,
    RefundStatus Status,
    bool ResolvedToExisting);
