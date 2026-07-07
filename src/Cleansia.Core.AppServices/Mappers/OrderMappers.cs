using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Mappers;

public static class OrderMappers
{
    public static OrderStatus GetCurrentOrderStatus(this Order order)
    {
        return order.CurrentStatus!.Value;
    }

    /// <summary>
    /// Server-side projection for the order LIST queries — fetches exactly what
    /// <see cref="MapToDto(OrderListRow)"/> needs instead of materializing the full entity
    /// graph. Must stay value-identical to the entity path
    /// (<see cref="MapToDto(Order)"/> over the handlers' old Include set) — pinned by
    /// OrderListProjectionEquivalenceTests.
    /// </summary>
    public static IQueryable<OrderListRow> SelectOrderListRows(this IQueryable<Order> orders)
    {
        return orders.Select(o => new OrderListRow(
            o.Id,
            o.CustomerName,
            o.CustomerEmail,
            o.CustomerPhone,
            o.CustomerAddress == null
                ? null
                : new OrderListAddressRow(
                    o.CustomerAddress.Id,
                    o.CustomerAddress.Street,
                    o.CustomerAddress.City,
                    o.CustomerAddress.ZipCode,
                    o.CustomerAddress.Latitude,
                    o.CustomerAddress.Longitude),
            o.DisplayOrderNumber,
            o.Rooms,
            o.Bathrooms,
            o.Extras,
            o.CleaningDateTime,
            o.PaymentType,
            o.PaymentStatus,
            o.TotalPrice,
            o.TierDiscountAmount,
            o.MembershipDiscountAmount,
            o.PromoDiscountAmount,
            o.EstimatedTime,
            // Persisted current status; a pre-backfill NULL column falls back to the
            // authoritative latest-history subquery (same rule: CreatedOn desc, Sequence desc).
            o.CurrentStatus ?? o.OrderStatusHistory
                .OrderByDescending(s => s.CreatedOn)
                .ThenByDescending(s => s.Sequence)
                .Select(s => (OrderStatus?)s.Status)
                .FirstOrDefault(),
            o.ConfirmationCode,
            o.CurrencyId,
            new OrderListCurrencyRow(
                o.Currency.Id,
                o.Currency.Code,
                o.Currency.Symbol,
                o.Currency.Name,
                o.Currency.ExchangeRate,
                o.Currency.IsDefault),
            o.SelectedPackages.Select(op => new OrderListPackageRow(
                op.Package!.Id,
                op.Package!.Name,
                op.Package!.Description,
                op.Package!.Price,
                op.Package!.Translations)).ToList(),
            o.SelectedServices.Select(os => new OrderListServiceRow(
                os.Service!.Id,
                os.Service!.Name,
                os.Service!.Description,
                os.Service!.BasePrice,
                os.Service!.PerRoomPrice,
                os.Service!.Translations,
                new OrderListCategoryRow(
                    os.Service!.Category!.Id,
                    os.Service!.Category!.Slug,
                    os.Service!.Category!.Name,
                    os.Service!.Category!.Description,
                    os.Service!.Category!.DisplayOrder,
                    os.Service!.Category!.Translations))).ToList(),
            o.AssignedEmployees.Select(ae => new OrderListEmployeeRow(ae.Id, ae.EmployeeId)).ToList(),
            o.RequiredEmployees,
            o.MaxEmployees,
            o.TravelDistance));
    }

    public static OrderListItem MapToDto(this OrderListRow row)
    {
        var (source, applied) = ResolveAppliedDiscount(
            row.PromoDiscountAmount, row.MembershipDiscountAmount, row.TierDiscountAmount);
        var assignedCount = row.AssignedEmployees.Count;
        var availableSpots = row.MaxEmployees - assignedCount;
        return new OrderListItem(
            Id: row.Id,
            CustomerName: row.CustomerName,
            CustomerEmail: row.CustomerEmail,
            CustomerPhone: row.CustomerPhone,
            CustomerAddress: row.Address != null
                ? $"{row.Address.Street}, {row.Address.City}, {row.Address.ZipCode}"
                : "",
            CustomerAddressApproximate: BuildApproximateAddress(row.Address?.City, row.Address?.ZipCode),
            DisplayOrderNumber: row.DisplayOrderNumber,
            Rooms: row.Rooms,
            Bathrooms: row.Bathrooms,
            Extras: row.Extras.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CleaningDateTime: row.CleaningDateTime,
            PaymentType: row.PaymentType.MapToCode(),
            PaymentStatus: row.PaymentStatus.MapToCode(),
            TotalPrice: row.TotalPrice,
            OriginalSubtotal: row.TotalPrice + applied,
            AppliedDiscountSource: source,
            TierDiscountAmount: row.TierDiscountAmount,
            MembershipDiscountAmount: row.MembershipDiscountAmount,
            PromoDiscountAmount: row.PromoDiscountAmount,
            EstimatedTime: row.EstimatedTime,
            OrderStatus: row.OrderStatus!.Value.MapToCode(),
            ConfirmationCode: row.ConfirmationCode,
            SelectedPackages: row.SelectedPackages.Select(p => new PackageListItem(
                Id: p.Id,
                Name: p.Name,
                Description: p.Description,
                Price: p.Price,
                Translations: p.Translations.ToDictionary(),
                // The list queries never load Package.IncludedServices, so the entity path
                // always emitted an empty collection here — preserved for wire parity.
                IncludedServices: Enumerable.Empty<PackageServiceSummary>())),
            CurrencyId: row.CurrencyId,
            Currency: new CurrencyListItem(
                Id: row.Currency.Id,
                Code: row.Currency.Code,
                Symbol: row.Currency.Symbol,
                Name: row.Currency.Name,
                ExchangeRate: row.Currency.ExchangeRate,
                IsDefault: row.Currency.IsDefault),
            AssignedEmployees: row.AssignedEmployees.Select(e => e.Id),
            SelectedServices: row.SelectedServices.Select(s => new ServiceListItem(
                Id: s.Id,
                Name: s.Name,
                Description: s.Description,
                Category: new CategoryDto(
                    Id: s.Category.Id,
                    Slug: s.Category.Slug,
                    Name: s.Category.Name,
                    Description: s.Category.Description,
                    DisplayOrder: s.Category.DisplayOrder,
                    Translations: s.Category.Translations.ToDictionary()),
                BasePrice: s.BasePrice,
                PerRoomPrice: s.PerRoomPrice,
                Translations: s.Translations.ToDictionary())),
            RequiredEmployees: row.RequiredEmployees,
            MaxEmployees: row.MaxEmployees,
            AvailableSpots: availableSpots,
            AssignedEmployeesCount: assignedCount,
            HasAvailableSpots: availableSpots > 0,
            EstimatedCleanerPay: null,
            CustomerAddressLatitude: row.Address?.Latitude,
            CustomerAddressLongitude: row.Address?.Longitude);
    }

    public static OrderListItem MapToDto(this Order order)
    {
        var (source, applied) = ResolveAppliedDiscount(order);
        return new OrderListItem(
            Id: order.Id,
            CustomerName: order.CustomerName,
            CustomerEmail: order.CustomerEmail,
            CustomerPhone: order.CustomerPhone,
            CustomerAddress: order.CustomerAddress != null
                ? $"{order.CustomerAddress.Street}, {order.CustomerAddress.City}, {order.CustomerAddress.ZipCode}"
                : "",
            CustomerAddressApproximate: BuildApproximateAddress(order.CustomerAddress),
            DisplayOrderNumber: order.DisplayOrderNumber,
            Rooms: order.Rooms,
            Bathrooms: order.Bathrooms,
            Extras: order.Extras.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CleaningDateTime: order.CleaningDateTime,
            PaymentType: order.PaymentType.MapToCode(),
            PaymentStatus: order.PaymentStatus.MapToCode(),
            TotalPrice: order.TotalPrice,
            OriginalSubtotal: order.TotalPrice + applied,
            AppliedDiscountSource: source,
            TierDiscountAmount: order.TierDiscountAmount,
            MembershipDiscountAmount: order.MembershipDiscountAmount,
            PromoDiscountAmount: order.PromoDiscountAmount,
            EstimatedTime: order.EstimatedTime,
            OrderStatus: order.GetCurrentOrderStatus().MapToCode(),
            ConfirmationCode: order.ConfirmationCode,
            SelectedPackages: order.SelectedPackages.Select(op => op.Package.MapToDto()),
            CurrencyId: order.CurrencyId,
            Currency: order.Currency.MapToDto(),
            AssignedEmployees: order.AssignedEmployees.Select(e => e.Id),
            SelectedServices: order.SelectedServices.Select(os => os.Service.MapToDto()),
            RequiredEmployees: order.RequiredEmployees,
            MaxEmployees: order.MaxEmployees,
            AvailableSpots: order.AvailableSpots,
            AssignedEmployeesCount: order.AssignedEmployees.Count,
            HasAvailableSpots: order.HasAvailableSpots,
            EstimatedCleanerPay: null,
            CustomerAddressLatitude: order.CustomerAddress?.Latitude,
            CustomerAddressLongitude: order.CustomerAddress?.Longitude
        );
    }

    /// <summary>
    /// Detail-shape projection of an Order. The caller-context
    /// parameters (<paramref name="estimatedCleanerPay"/>,
    /// <paramref name="isAssignedToCurrentUser"/> and
    /// <paramref name="isCustomerCaller"/>) are passed in rather
    /// than computed here because they require services the mapper
    /// shouldn't reach for (pay-config repository, current-user
    /// resolver). Handlers compute them and hand the values in.
    /// </summary>
    public static OrderItem MapToDetail(
        this Order order,
        decimal? estimatedCleanerPay = null,
        bool isAssignedToCurrentUser = false,
        bool hasAfterPhotos = false,
        bool isCustomerCaller = false)
    {
        var (source, applied) = ResolveAppliedDiscount(order);
        return new OrderItem(
            Id: order.Id,
            DisplayOrderNumber: order.DisplayOrderNumber,
            CustomerName: order.CustomerName,
            CustomerEmail: order.CustomerEmail,
            CustomerPhone: order.CustomerPhone,
            Address: order.CustomerAddress.MapToOrderAddress(),
            Rooms: order.Rooms,
            Bathrooms: order.Bathrooms,
            Extras: order.Extras.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CleaningDateTime: order.CleaningDateTime,
            PaymentType: order.PaymentType.MapToCode(),
            PaymentStatus: order.PaymentStatus.MapToCode(),
            TotalPrice: order.TotalPrice,
            OriginalSubtotal: order.TotalPrice + applied,
            AppliedDiscountSource: source,
            TierDiscountAmount: order.TierDiscountAmount,
            MembershipDiscountAmount: order.MembershipDiscountAmount,
            PromoDiscountAmount: order.PromoDiscountAmount,
            EstimatedTime: order.EstimatedTime,
            ActualCompletionTime: order.ActualCompletionTime,
            CompletedAt: order.CompletedAt,
            CompletionNotes: order.CompletionNotes,
            OrderStatus: order.GetCurrentOrderStatus().MapToCode(),
            ConfirmationCode: order.ConfirmationCode,
            Notes: order.Notes,
            SpecialInstructions: order.SpecialInstructions,
            AccessInstructions: order.AccessInstructions,
            RecurringTemplateId: order.RecurringTemplateId,
            SelectedPackages: order.SelectedPackages.Select(op => op.Package.MapToDetails(order.Currency.Code)),
            Currency: order.Currency.MapToDetailDto(),
            SelectedServices: order.SelectedServices.Select(os => os.Service.MapToDetails(order.Currency.Code)),
            StatusHistory: order.OrderStatusHistory.Select(sh => sh.MapToDto()) ?? [],
            CreatedOn: order.CreatedOn,
            UpdatedOn: order.UpdatedOn,
            AssignedEmployees: order.AssignedEmployees.Select(ae => ae.MapToAssignedEmployeeDto(isCustomerCaller)),
            ReceiptNumber: order.Receipt?.ReceiptNumber,
            OrderNotes: order.OrderNotes.Select(n => n.MapToDto()),
            OrderIssues: order.OrderIssues.Select(i => i.MapToDto()),
            Review: order.Reviews.FirstOrDefault()?.MapToDto(),
            EstimatedCleanerPay: estimatedCleanerPay,
            IsAssignedToCurrentUser: isAssignedToCurrentUser,
            HasAfterPhotos: hasAfterPhotos
        );
    }

    public static OrderNoteDto MapToDto(this OrderNote note)
    {
        return new OrderNoteDto(
            Id: note.Id,
            EmployeeId: note.EmployeeId,
            Content: note.Content,
            CreatedOn: note.CreatedOn
        );
    }

    public static OrderIssueDto MapToDto(this OrderIssue issue)
    {
        return new OrderIssueDto(
            Id: issue.Id,
            ReportedByEmployeeId: issue.ReportedByEmployeeId,
            Description: issue.Description,
            IsResolved: issue.IsResolved,
            ResolvedAt: issue.ResolvedAt,
            CreatedOn: issue.CreatedOn
        );
    }

    public static OrderReviewDto MapToDto(this OrderReview review)
    {
        return new OrderReviewDto(
            Id: review.Id,
            OrderId: review.OrderId,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedOn: review.CreatedOn,
            UpdatedOn: review.UpdatedOn
        );
    }

    // Customer callers get the cleaner's first name only and no phone —
    // same masking GetOrderPhotos applies to CapturedByEmployeeName.
    public static AssignedEmployeeDto MapToAssignedEmployeeDto(this OrderEmployee orderEmployee, bool isCustomerCaller = false)
    {
        var employee = orderEmployee.Employee;
        var user = employee?.User;

        return new AssignedEmployeeDto(
            Id: orderEmployee.Id,
            EmployeeId: orderEmployee.EmployeeId,
            FullName: isCustomerCaller
                ? user?.FirstName ?? string.Empty
                : user != null
                    ? $"{user.FirstName} {user.LastName}".Trim()
                    : string.Empty,
            PhoneNumber: isCustomerCaller ? null : user?.PhoneNumber
        );
    }

    /// <summary>
    /// Picks which of the three discount sources applied to this order. The
    /// fields can ALL be non-null when LOY-003's additive Plus + Tier model
    /// kicks in (membership and tier both apply, optionally with promo
    /// replacing them — though the resolver zeroes Plus+Tier when promo wins,
    /// so promo is always exclusive with the additive pair).
    ///
    /// Returns the source enum + total CZK amount applied so callers can
    /// derive the original subtotal as `TotalPrice + applied`.
    /// </summary>
    private static (AppliedDiscountSource Source, decimal Amount) ResolveAppliedDiscount(Order order) =>
        ResolveAppliedDiscount(order.PromoDiscountAmount, order.MembershipDiscountAmount, order.TierDiscountAmount);

    private static (AppliedDiscountSource Source, decimal Amount) ResolveAppliedDiscount(
        decimal? promoDiscountAmount,
        decimal? membershipDiscountAmount,
        decimal? tierDiscountAmount)
    {
        // Promo always wins exclusively when present — the resolver zeroes
        // Plus + Tier in the promo branch, so we'd never see promo alongside
        // either of them.
        if (promoDiscountAmount is > 0m)
        {
            return (AppliedDiscountSource.Promo, promoDiscountAmount.Value);
        }

        var membership = membershipDiscountAmount ?? 0m;
        var tier = tierDiscountAmount ?? 0m;

        return (membership, tier) switch
        {
            ( > 0m, > 0m) => (AppliedDiscountSource.Combined, membership + tier),
            ( > 0m, _) => (AppliedDiscountSource.Membership, membership),
            (_, > 0m) => (AppliedDiscountSource.Tier, tier),
            _ => (AppliedDiscountSource.None, 0m),
        };
    }

    /// <summary>
    /// Coarse location string safe to show to cleaners *before* they accept
    /// the order — no street name, no house number. Mirrors how Wolt/Bolt
    /// show pickup areas: enough for the cleaner to evaluate distance/zone,
    /// none of the PII that would let them dox the customer.
    ///
    /// Output examples:
    ///   - "Praha 4 · 14000"   (Czech city districts encoded in the ZIP prefix)
    ///   - "Brno · 60200"
    ///   - "Praha"             (no ZIP → fall back to city only)
    ///   - ""                  (no address at all)
    ///
    /// We deliberately use ZIP prefix not full ZIP because PSČ "14000"
    /// already identifies the broad district (whole Praha 4); leaving the
    /// last two digits would narrow it to a specific street group, which
    /// defeats the privacy intent.
    /// </summary>
    private static string BuildApproximateAddress(Address? address) =>
        address == null ? string.Empty : BuildApproximateAddress(address.City, address.ZipCode);

    private static string BuildApproximateAddress(string? rawCity, string? rawZipCode)
    {
        var city = rawCity?.Trim();
        if (string.IsNullOrEmpty(city)) return string.Empty;

        var zipPrefix = rawZipCode?.Trim();
        if (!string.IsNullOrEmpty(zipPrefix) && zipPrefix.Length >= 3)
        {
            // For Czech PSČ (5 digits, no spaces) keep the first 3 — that's
            // city + district granularity. International ZIPs vary; the
            // length>=3 check just prevents emitting "1" as a "prefix".
            var truncated = zipPrefix.Replace(" ", "")
                .Substring(0, Math.Min(3, zipPrefix.Length));
            return $"{city} · {truncated}";
        }

        return city;
    }
}