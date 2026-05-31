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
    bool HasAfterPhotos
);