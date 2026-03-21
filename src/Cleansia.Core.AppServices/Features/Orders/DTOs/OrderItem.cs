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
    int EstimatedTime,
    int? ActualCompletionTime,
    string? CompletionNotes,
    Code OrderStatus,
    string ConfirmationCode,
    string? StripeSessionId,
    string? Notes,
    string? SpecialInstructions,
    string? AccessInstructions,
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