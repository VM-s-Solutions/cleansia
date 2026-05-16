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
    OrderReviewDto? Review
);