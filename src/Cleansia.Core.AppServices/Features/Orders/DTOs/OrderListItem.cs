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
    /// <summary>
    /// Full street-level address. Visible to the assigned cleaner (or admins);
    /// blanked out by GetPagedOrders for non-assigned cleaners. Use
    /// <see cref="CustomerAddressApproximate"/> for the safe display value on
    /// the Available tab.
    /// </summary>
    string CustomerAddress,
    /// <summary>
    /// City + neighbourhood / zip-prefix combo safe to show before the
    /// cleaner accepts the job. e.g. "Praha 4 · Nusle" or "Brno · 60200".
    /// Always populated when CustomerAddress.City is non-empty; backend never
    /// blanks this field. Wolt/Bolt-style coarse signal: enough to evaluate
    /// distance / area, no PII leakage.
    /// </summary>
    string CustomerAddressApproximate,
    string DisplayOrderNumber,
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
    Code OrderStatus,
    string ConfirmationCode,
    IEnumerable<PackageListItem> SelectedPackages,
    string CurrencyId,
    CurrencyListItem Currency,
    IEnumerable<string> AssignedEmployees,
    IEnumerable<ServiceListItem> SelectedServices,
    int RequiredEmployees,
    int MaxEmployees,
    int AvailableSpots,
    int AssignedEmployeesCount,
    bool HasAvailableSpots,
    decimal? EstimatedCleanerPay,
    double? CustomerAddressLatitude,
    double? CustomerAddressLongitude,
    /// <summary>
    /// Whether this order already carries a review from its customer. Projected server-side rather
    /// than derived client-side, because the alternative is a per-order detail fetch: the mobile apps
    /// decide whether to raise the completion rating prompt from the WARM list cache, and the detail
    /// payload is the only other place a review appears. One bool removes an N+1 before it exists.
    /// </summary>
    bool HasReview);