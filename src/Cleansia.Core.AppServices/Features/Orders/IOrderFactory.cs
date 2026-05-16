using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Builds + persists <see cref="Order"/> aggregates with full pricing/discount
/// snapshot, services + packages relationships, VAT breakdown, and an initial
/// <see cref="OrderStatus.New"/> status track. Shared by the customer-facing
/// <see cref="CreateOrder.Handler"/> (one-off booking) and by
/// <see cref="Bookings.MaterializeRecurringBookings.Handler"/> (recurring
/// pipeline) so the order-creation contract — pricing rules, discount math,
/// VAT, status track — is in exactly one place.
///
/// The caller is responsible for everything around the factory:
///   * address + currency resolution (the factory takes already-loaded entities)
///   * Stripe checkout session creation (one-off card flow)
///   * post-create side effects (queue receipt generation, promo
///     <c>ApplyAsync</c>, referral acceptance, push notifications)
///
/// The factory itself does NOT create a Stripe session — recurring orders
/// stay <see cref="PaymentStatus.Pending"/> until the customer confirms,
/// at which point the confirm flow creates the session.
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
    /// Optional promo discount + code id from <c>PromoCodeService.Preview</c>.
    /// Caller computes; factory only feeds these into best-of-three.
    /// </summary>
    decimal PromoDiscountAmount = 0m,
    string? PromoCodeId = null,
    /// <summary>Optional preferred-cleaner hint (Plus perk).</summary>
    string? PreferredEmployeeId = null,
    /// <summary>FK back to recurring template (set by materializer; null for one-off).</summary>
    string? RecurringTemplateId = null);
