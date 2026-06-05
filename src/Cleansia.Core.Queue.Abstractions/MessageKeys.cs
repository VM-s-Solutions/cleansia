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

    /// <summary>generate-invoice → <c>invoice:{PayPeriodId}:{EmployeeId}</c> (one invoice per employee per period).</summary>
    public static string Invoice(string payPeriodId, string employeeId) => $"invoice:{payPeriodId}:{employeeId}";
}
