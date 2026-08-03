using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum OrderStatus
{
    New = 0,

    /// <summary>
    /// DEAD — no production writer, and none may be added (ADR-0037 D5). The state it names, "card
    /// payment initiated, waiting for the webhook", is real but is tracked on the PAYMENT axis
    /// (<see cref="PaymentType.Card"/> + <see cref="PaymentStatus.Pending"/>), which is what the
    /// live sweeps and the offerability rule read. A writer here would be a second source of truth
    /// for one fact.
    ///
    /// <para>Not deleted: the integer is on the wire to three generated clients, and legacy rows may
    /// hold it. Readers must keep TOLERATING it in the conservative direction — a Pending row is
    /// live/active for the calendar and for GDPR — and it stays in the admin override's rank array
    /// so those rows can still be ranked. It is never offerable.</para>
    /// </summary>
    Pending = 1,

    Confirmed = 2,
    OnTheWay = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
}