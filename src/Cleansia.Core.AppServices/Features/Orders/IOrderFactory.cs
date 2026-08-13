using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Builds and persists <see cref="Order"/> aggregates — pricing snapshot, discounts, VAT, relationships
/// and the initial status track — shared by the one-off and recurring paths so <b>the order-creation
/// contract lives in exactly one place</b>.
///
/// <para>The caller owns everything around it: address and currency resolution, Stripe session
/// creation, and post-create side effects. → /flows/booking-and-pricing</para>
/// </summary>
public interface IOrderFactory
{
    Task<Order> CreateAsync(CreateOrderInput input, CancellationToken cancellationToken);
}

/// <summary>
/// Inputs for <see cref="IOrderFactory.CreateAsync"/>. All entities are
/// expected to be already resolved by the caller — this contract is a pure
/// "given these, build the Order" boundary, not a "look these up" boundary.
/// </summary>
public record CreateOrderInput(
    /// <summary>
    /// Booking user id. Empty/null is allowed for the legacy anonymous guest
    /// checkout path; those orders skip discount lookups entirely.
    /// </summary>
    string? UserId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    Address Address,
    int Rooms,
    int Bathrooms,
    Dictionary<string, bool> Extras,
    DateTime CleaningDate,
    PaymentType PaymentType,
    Currency Currency,
    IEnumerable<string> SelectedServiceIds,
    IEnumerable<string> SelectedPackageIds,
    /// <summary>
    /// Raw pre-discount subtotal (matches <c>IOrderPricingCalculator</c>).
    /// The factory applies discount + express surcharge on top.
    /// </summary>
    decimal RawSubtotal,
    /// <summary>
    /// The ONE clock reading for this creation, captured by the caller and threaded through the
    /// express-window decision, so the price the factory freezes cannot land on the other side of the 4h
    /// boundary from the waiver the caller already reserved. Required, never defaulted: a default is a
    /// second clock read wearing a parameter's name.
    /// </summary>
    DateTime NowUtc,
    /// <summary>
    /// An express waiver the caller has ALREADY RESOLVED <b>and RESERVED</b> (ADR-0035 D6/AM-9), or null.
    /// The factory consumes the answer and never resolves or reserves — that is what makes "exactly one
    /// consuming call site" true by construction rather than by grep, and it keeps this contract's
    /// collaborator count unchanged. <c>MaterializeRecurringBookings</c> passes null <b>explicitly</b>:
    /// a recurring occurrence never draws a waiver, as a rule rather than as an accident of the current
    /// template shape.
    /// </summary>
    MembershipBenefitUsage? ReservedExpressWaiver,
    /// <summary>
    /// Optional promo discount + code id from <c>PromoCodeService.Preview</c>.
    /// Caller computes; factory only feeds these into best-of-three.
    /// </summary>
    decimal PromoDiscountAmount = 0m,
    string? PromoCodeId = null,
    /// <summary>Optional preferred-cleaner hint (Plus perk).</summary>
    string? PreferredEmployeeId = null,
    /// <summary>FK back to recurring template (set by materializer; null for one-off).</summary>
    string? RecurringTemplateId = null,
    /// <summary>
    /// Free-text note the customer typed at booking time. Persisted verbatim on
    /// the Order and surfaced read-only to the partner/admin surfaces. Null for
    /// the recurring pipeline — a template carries no per-occurrence note.
    /// </summary>
    string? SpecialInstructions = null,
    /// <summary>
    /// Free-text entry instructions the customer typed at booking time. Kept
    /// separate from <see cref="SpecialInstructions"/> because they answer
    /// different questions, not because it is access-controlled — it is not.
    /// Null for the recurring pipeline: a template carries no per-occurrence note.
    /// </summary>
    string? AccessInstructions = null);
