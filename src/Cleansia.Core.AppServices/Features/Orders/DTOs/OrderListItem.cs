using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderListItem(
    string Id,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string CustomerAddress,
    string DisplayOrderNumber,
    int Rooms,
    int Bathrooms,
    Dictionary<string, bool> Extras,
    DateTime CleaningDateTime,
    Code PaymentType,
    Code PaymentStatus,
    decimal TotalPrice,
    int EstimatedTime,
    Code OrderStatus,
    string ConfirmationCode,
    string StripeSessionId,
    IEnumerable<PackageListItem> SelectedPackages,
    string CurrencyId,
    CurrencyListItem Currency,
    IEnumerable<string> AssignedEmployees,
    IEnumerable<ServiceListItem> SelectedServices,
    int RequiredEmployees,
    int MaxEmployees,
    int AvailableSpots,
    int AssignedEmployeesCount,
    bool HasAvailableSpots);