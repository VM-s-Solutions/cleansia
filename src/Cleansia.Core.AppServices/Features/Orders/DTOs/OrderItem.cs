using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderItem(
    string Id,
    string DisplayOrderNumber,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    OrderAddress? Address,
    /// <summary>
    /// City + zip-prefix combo safe to show before the cleaner accepts the job, e.g. "Praha · 120".
    /// The same value and the same builder as <c>OrderListItem.CustomerAddressApproximate</c>: the
    /// board row and the detail behind it describe one order to one caller, so the coarse location
    /// cannot be on one and missing from the other. Always populated when the address has a city;
    /// the backend never blanks this field, including for a browsing cleaner whose
    /// <see cref="Address"/> is nulled — deciding whether a job is worth taking is a question about
    /// distance.
    /// </summary>
    string CustomerAddressApproximate,
    int Rooms,
    int Bathrooms,
    Dictionary<string, bool> Extras,
    DateTime CleaningDateTime,
    Code PaymentType,
    Code PaymentStatus,
    decimal TotalPrice,
    decimal OriginalSubtotal,
    AppliedDiscountSource AppliedDiscountSource,
    decimal? TierDiscountAmount,
    decimal? MembershipDiscountAmount,
    decimal? PromoDiscountAmount,
    int EstimatedTime,
    int? ActualCompletionTime,
    DateTime? CompletedAt,
    string? CompletionNotes,
    Code OrderStatus,
    string ConfirmationCode,
    string? Notes,
    string? SpecialInstructions,
    string? AccessInstructions,
    /// <summary>
    /// FK back to the recurring booking template that spawned this order.
    /// Null for one-off orders. Mobile uses this + <c>PaymentStatus.Pending</c>
    /// to show the "Confirm and pay" CTA on the Order Detail screen.
    /// </summary>
    string? RecurringTemplateId,
    IEnumerable<PackageDetails> SelectedPackages,
    CurrencyDetailDto Currency,
    IEnumerable<ServiceDetails> SelectedServices,
    IEnumerable<OrderStatusTrackDto> StatusHistory,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn,
    IEnumerable<AssignedEmployeeDto> AssignedEmployees,
    /// <summary>
    /// Crew size and seat occupancy, the same five members and the same values as on
    /// <c>OrderListItem</c>. The board row already shows all five to a browsing cleaner, so the detail
    /// behind it discloses nothing new by repeating them — and without them the screen a cleaner taps
    /// Take on cannot say whether a seat is free, while <c>TakeOrder</c>'s free-seat conjunct refuses
    /// the take anyway. Neither <see cref="RequiredEmployees"/> nor <see cref="MaxEmployees"/> is
    /// derivable client-side, so a client cannot reconstruct the block from the rest of the payload.
    /// </summary>
    int RequiredEmployees,
    int MaxEmployees,
    /// <summary>
    /// <c>MaxEmployees - AssignedEmployeesCount</c>, and <c>AvailableSpots &gt; 0</c>. Both are
    /// derivable from the three members above and are carried anyway, because the list carries them
    /// and a client that reads one shape and computes the other is how the two come to disagree.
    /// Server-computed on both shapes from one source, so they cannot.
    /// </summary>
    int AvailableSpots,
    int AssignedEmployeesCount,
    bool HasAvailableSpots,
    string? ReceiptNumber,
    IEnumerable<OrderNoteDto> OrderNotes,
    IEnumerable<OrderIssueDto> OrderIssues,
    OrderReviewDto? Review,
    /// <summary>
    /// What the calling employee would earn for this order, in the
    /// order's currency. Null for non-employee callers (admin /
    /// customer) and for employees with no matching pay config. Mirrors
    /// the same field on <c>OrderListItem</c>; the detail screen uses
    /// it to anchor the hero card's earnings chip without a separate
    /// network round-trip.
    /// </summary>
    decimal? EstimatedCleanerPay,
    /// <summary>
    /// True when the calling employee is one of the order's assigned
    /// cleaners. Drives the partner-mobile detail screen's primary
    /// action gating (Take button only renders when this is false on
    /// an Available offer; Notify / Start / Complete only when true).
    /// Always false for non-employee callers.
    /// </summary>
    bool IsAssignedToCurrentUser,
    /// <summary>
    /// True when the order has at least one uploaded "after" photo.
    /// Partner-mobile uses this to gate the Slide-to-complete action
    /// client-side so the cleaner sees an instant message instead of
    /// round-tripping to the server's <c>AfterPhotosRequired</c>
    /// validator. The validator stays on as a safety net.
    /// </summary>
    bool HasAfterPhotos,
    /// <summary>
    /// Cancelling right now would consume one of the customer's free express bookings this month.
    /// Server-computed — no client can derive it.
    ///
    /// <para><b>The cancel confirmation MUST say so when true, and especially when the fee is 0</b> —
    /// inside the oops window and on every cash order the fee is zero and the forfeiture is otherwise
    /// invisible. A silent forfeiture against a paid subscription is the one thing this design refused to
    /// ship. Nullable so it is additive on the wire. → /flows/loyalty-and-memberships</para>
    /// </summary>
    bool? ExpressWaiverForfeitedOnCancel = null,
    /// <summary>
    /// ADR-0045 D7.2 — the state of the customer's favourite-cleaner reservation on this order.
    /// CUSTOMER-ONLY: null for every other caller, so no cleaner ever learns an order was reserved for
    /// someone else and nobody ever learns they were passed over.
    ///
    /// <para>Nullable + defaulted so it is additive on the wire: a client built before this field omits
    /// it and behaves exactly as before. An absent block must render as "no offer" and must never drop
    /// the order row — both mobile mappers carry a row-dropping idiom that would.</para>
    /// </summary>
    PreferredOfferDetails? PreferredOffer = null,
    /// <summary>
    /// Whether this order carries entry instructions at all — WITHOUT carrying them.
    ///
    /// <para>An admin read has <c>AccessInstructions</c> withheld (see
    /// <c>OrderPiiRedaction.WithholdAccessInstructions</c>), so the admin UI needs a way to tell
    /// "nothing to reveal" from "something to reveal" — otherwise it offers a reveal on every order and
    /// half of them return nothing. Every other caller gets both this flag and the text.</para>
    ///
    /// <para>Nullable + defaulted so it is additive on the wire: a client built before this field omits
    /// it and behaves exactly as before.</para>
    /// </summary>
    bool? HasAccessInstructions = null,

    /// <summary>
    /// Why the PLATFORM cancelled this order, as a key the client localises — null for every order
    /// the platform did not cancel itself.
    ///
    /// <para><b>System cancellations only, and that is a privacy boundary rather than a filter.</b>
    /// <c>Order.CancellationReason</c> also holds free text when an admin cancels, written by staff
    /// for staff. The mapper exposes this field only when <c>CancelledBy</c> is <c>System</c>, so an
    /// internal note cannot reach a customer through it.</para>
    ///
    /// <para>Nullable + defaulted so it is additive on the wire: a client built before this field
    /// omits it and behaves exactly as before.</para>
    /// </summary>
    string? SystemCancellationReason = null
);