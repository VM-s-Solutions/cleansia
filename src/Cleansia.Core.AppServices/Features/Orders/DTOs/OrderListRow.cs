using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

/// <summary>
/// Server-side projection shape for the order LIST queries — exactly the columns the
/// <see cref="OrderListItem"/> mapper reads plus the sidecar fields the list handlers need
/// (assignee ids for the ownership mask, travel distance for the pay estimator, address id
/// for the geocode backfill). Never leaves the backend; the wire DTO stays
/// <see cref="OrderListItem"/>.
/// </summary>
public sealed record OrderListRow(
    string Id,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    OrderListAddressRow? Address,
    string DisplayOrderNumber,
    int Rooms,
    int Bathrooms,
    IReadOnlyDictionary<string, bool> Extras,
    DateTime CleaningDateTime,
    PaymentType PaymentType,
    PaymentStatus PaymentStatus,
    decimal TotalPrice,
    decimal? TierDiscountAmount,
    decimal? MembershipDiscountAmount,
    decimal? PromoDiscountAmount,
    int EstimatedTime,
    OrderStatus? OrderStatus,
    string ConfirmationCode,
    string CurrencyId,
    OrderListCurrencyRow Currency,
    List<OrderListPackageRow> SelectedPackages,
    List<OrderListServiceRow> SelectedServices,
    List<OrderListEmployeeRow> AssignedEmployees,
    int RequiredEmployees,
    int MaxEmployees,
    decimal? TravelDistance);

public sealed record OrderListAddressRow(
    string Id,
    string Street,
    string City,
    string ZipCode,
    double? Latitude,
    double? Longitude);

public sealed record OrderListCurrencyRow(
    string Id,
    string Code,
    string Symbol,
    string Name,
    decimal ExchangeRate,
    bool IsDefault);

public sealed record OrderListServiceRow(
    string Id,
    string Name,
    string Description,
    decimal BasePrice,
    decimal PerRoomPrice,
    IReadOnlyDictionary<string, Translation> Translations,
    OrderListCategoryRow Category);

public sealed record OrderListCategoryRow(
    string Id,
    string Slug,
    string Name,
    string Description,
    int DisplayOrder,
    IReadOnlyDictionary<string, Translation> Translations);

public sealed record OrderListPackageRow(
    string Id,
    string Name,
    string Description,
    decimal Price,
    IReadOnlyDictionary<string, Translation> Translations);

public sealed record OrderListEmployeeRow(
    string Id,
    string EmployeeId);
