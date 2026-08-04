using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// ADR-0035 — an <b>order-linked</b> waiver ledger with a benefit discriminator, not a generic benefit
/// ledger: the row carries an <see cref="OrderId"/> and the whole release rule (D4) is expressed in
/// <see cref="Order"/> vocabulary. A benefit that is not order-shaped reuses the entity, the index, the
/// reservation statement and the period key, and needs its own release rule.
///
/// <para>One live row = one consumed slot. The row exists only where a waiver was actually granted —
/// no surcharge, no quota left, or a guest booking all write nothing (D1).</para>
///
/// <para><b>The quota is keyed on <c>(TenantId, UserId, BenefitKind, PeriodKey)</c> and nothing else</b>
/// (AM-19). <see cref="UserMembershipId"/> is a payload column carried for support ("which subscription
/// was this against?"); it must never appear in a <c>WHERE</c>, <c>GROUP BY</c>, <c>HAVING</c> or join
/// predicate on a counting path — a quota keyed on the enrolment would reset on re-subscribe.</para>
///
/// <para>Reserved by one atomic <c>INSERT … SELECT … ON CONFLICT DO NOTHING RETURNING</c> statement that
/// derives the smallest free ordinal in SQL; the filtered unique index behind it is the sole arbiter of
/// the race, which is why it is <c>NULLS NOT DISTINCT</c> (D3).</para>
/// </summary>
public class MembershipBenefitUsage : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(26)]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    [Required]
    public MembershipBenefitKind BenefitKind { get; private set; }

    /// <summary>
    /// The period this slot was drawn from — <c>"C:2026-08"</c> for the calendar rule. Computed once, at
    /// reservation, by the single period-key factory, and <b>never recomputed</b>: a stored key means a
    /// timezone or configuration correction cannot retroactively move a past reservation into another
    /// month (D2).
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string PeriodKey { get; private set; } = default!;

    /// <summary>
    /// 0-based, in <c>[0, MaxPerPeriod-1]</c>. Derived <b>inside</b> the reservation statement as the
    /// smallest free ordinal, never from a pre-read count — a count is the cardinality of the live set,
    /// which collides with a live row as soon as a release leaves a hole (AM-5).
    /// </summary>
    [Required]
    public int SlotOrdinal { get; private set; }

    /// <summary>
    /// The order the waiver was granted on. Nullable by design: the reservation auto-commits before the
    /// order exists, so stamping it at reservation time would violate the order's foreign key. The stamp
    /// is a change-tracked update that rides the unit of work (AM-4/D3.2); a row that never gets one is
    /// reclaimed by the orphan sweep.
    /// </summary>
    [MaxLength(26)]
    public string? OrderId { get; private set; }
    public Order? Order { get; private set; }

    [Required]
    [MaxLength(26)]
    public string UserMembershipId { get; private set; } = default!;
    public UserMembership? UserMembership { get; private set; }

    [Required]
    public DateTime ReservedAtUtc { get; private set; }

    private MembershipBenefitUsage() { }

    /// <summary>
    /// The reservation itself is the single SQL statement in the repository — this factory is for the
    /// paths that materialize a row through the ORM (seeds, tests, a non-racing backfill).
    /// </summary>
    public static MembershipBenefitUsage Create(
        string userId,
        MembershipBenefitKind benefitKind,
        string periodKey,
        int slotOrdinal,
        string userMembershipId,
        DateTime reservedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required", nameof(userId));
        }
        if (string.IsNullOrWhiteSpace(periodKey))
        {
            throw new ArgumentException("PeriodKey is required", nameof(periodKey));
        }
        if (string.IsNullOrWhiteSpace(userMembershipId))
        {
            throw new ArgumentException("UserMembershipId is required", nameof(userMembershipId));
        }
        if (slotOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotOrdinal), "Slot ordinal cannot be negative.");
        }

        return new MembershipBenefitUsage
        {
            UserId = userId,
            BenefitKind = benefitKind,
            PeriodKey = periodKey,
            SlotOrdinal = slotOrdinal,
            UserMembershipId = userMembershipId,
            ReservedAtUtc = reservedAtUtc,
        };
    }

    /// <summary>
    /// Link the reserved slot to the order it paid for, once that order is a tracked entity in the same
    /// unit of work. Idempotent: the first order wins.
    /// </summary>
    public MembershipBenefitUsage AttachOrder(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId is required", nameof(orderId));
        }

        OrderId ??= orderId;
        return this;
    }
}
