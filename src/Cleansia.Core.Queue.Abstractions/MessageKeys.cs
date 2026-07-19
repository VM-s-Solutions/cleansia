using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// ADR-0002 D2.1 — the FROZEN, deterministic <c>MessageKey</c> formulas (one per queue). The single
/// source of truth shared by producers (the Bucket-A handlers that build the <see cref="QueueEnvelope{T}"/>)
/// and by consumers that must SYNTHESIZE the same key from a bare payload at the deploy boundary
/// (D2.1a dual-read). Every key is a PURE function of its domain inputs — same inputs ⇒ same key
/// (no <c>Guid.NewGuid()</c>, no timestamp), which is the property the whole dispatch contract rests
/// on (TC-KEY-0). Changing any formula is a SUPERSEDING ADR, never an edit.
/// </summary>
public static class MessageKeys
{
    /// <summary>generate-receipt → <c>receipt:{OrderId}</c> (one receipt per order).</summary>
    public static string Receipt(string orderId) => $"receipt:{orderId}";

    /// <summary>
    /// notifications-dispatch → <c>push:{UserId}:{EventKey}:{OrderId?}</c> (one push per user per
    /// event per subject). The subject segment is optional — a null/empty subject keeps the trailing
    /// separator so a subjectless push still dedups per (user, event).
    /// </summary>
    public static string Push(string userId, string eventKey, string? subject) =>
        $"push:{userId}:{eventKey}:{subject}";

    /// <summary>calculate-order-pay → <c>pay:{OrderId}:{EmployeeId}</c> (one pay row per order per cleaner).</summary>
    public static string Pay(string orderId, string employeeId) => $"pay:{orderId}:{employeeId}";

    /// <summary>
    /// live-activity-dispatch → <c>liveactivity:{OrderId}:{EventKey}:{Sequence}</c> (one activity send
    /// per order per transition). <paramref name="sequence"/> is the transition's
    /// <c>OrderStatusTrack.Sequence</c> — a pure function of domain state, never a timestamp or Guid.
    /// The Sequence segment is DEFENSIVE (ADR-0029 RV-4): no current code path re-appends the same
    /// (order, event) — <c>AdminOverrideOrderStatus</c> rejects same-status revisits — but a frozen key
    /// stays collision-free if any future handler ever does.
    /// </summary>
    public static string LiveActivity(string orderId, string eventKey, int sequence) =>
        $"liveactivity:{orderId}:{eventKey}:{sequence}";

    /// <summary>generate-invoice → <c>invoice:{PayPeriodId}:{EmployeeId}</c> (one invoice per employee per period).</summary>
    public static string Invoice(string payPeriodId, string employeeId) => $"invoice:{payPeriodId}:{employeeId}";

    /// <summary>
    /// sitewide-promo-fanout → <c>promo:{tenant}:{contentHash}</c>. The fan-out has no inbound dedup, but
    /// its outbox row still needs a deterministic key: the same admin campaign re-submitted in one request
    /// collapses onto one row, while two distinct campaigns hash differently. The content of the campaign
    /// (its per-locale title+body) is the only stable identity an admin-authored send has — there is no
    /// domain id — so a content hash, not a fresh Guid, is what keeps the key deterministic.
    /// </summary>
    public static string SitewidePromo(string? tenantId, string contentSignature) =>
        $"promo:{tenantId}:{HashCode(contentSignature)}";

    /// <summary>
    /// send-email → <c>email:{purpose}:{userId}:{codeHash}</c> (one email per user per type per
    /// generated code). <paramref name="codeHash"/> is the <see cref="HashCode"/> of the generated
    /// confirmation/reset token, so a reissued token (a genuine resend) yields a distinct key and a new
    /// email, while a redelivery of the same logical email collapses onto one key. The raw token is
    /// never put in the key — only its hash.
    /// </summary>
    public static string Email(EmailType emailType, string userId, string codeHash) =>
        $"email:{Purpose(emailType)}:{userId}:{codeHash}";

    /// <summary>
    /// Deterministic, non-reversible short hash of a raw email token, used as the code segment of the
    /// send-email key so the secret never appears in a key or a log line. Producer and consumer
    /// (dual-read key synthesis) compute it the same way.
    /// </summary>
    public static string HashCode(string rawCode) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)))[..16].ToLowerInvariant();

    private static string Purpose(EmailType emailType) => emailType switch
    {
        EmailType.ConfirmationEmail => "confirmation",
        EmailType.ResetPassword => "reset",
        _ => emailType.ToString().ToLowerInvariant(),
    };
}
