using Cleansia.Core.AppServices.Features.Orders.DTOs;
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
    /// Detail-shape projection of an Order. The two caller-context
    /// parameters (<paramref name="estimatedCleanerPay"/> and
    /// <paramref name="isAssignedToCurrentUser"/>) are passed in rather
    /// than computed here because they require services the mapper
    /// shouldn't reach for (pay-config repository, current-user
    /// resolver). Handlers compute them and hand the values in.
    /// </summary>
    public static OrderItem MapToDetail(
        this Order order,
        decimal? estimatedCleanerPay = null,
        bool isAssignedToCurrentUser = false,
        bool hasAfterPhotos = false)
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
            AssignedEmployees: order.AssignedEmployees.Select(ae => ae.MapToAssignedEmployeeDto()),
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

    public static AssignedEmployeeDto MapToAssignedEmployeeDto(this OrderEmployee orderEmployee)
    {
        var employee = orderEmployee.Employee;
        var user = employee?.User;

        return new AssignedEmployeeDto(
            Id: orderEmployee.Id,
            EmployeeId: orderEmployee.EmployeeId,
            FullName: user != null
                ? $"{user.FirstName} {user.LastName}".Trim()
                : string.Empty,
            PhoneNumber: user?.PhoneNumber
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
    private static (AppliedDiscountSource Source, decimal Amount) ResolveAppliedDiscount(Order order)
    {
        // Promo always wins exclusively when present — the resolver zeroes
        // Plus + Tier in the promo branch, so we'd never see promo alongside
        // either of them.
        if (order.PromoDiscountAmount is > 0m)
        {
            return (AppliedDiscountSource.Promo, order.PromoDiscountAmount.Value);
        }

        var membership = order.MembershipDiscountAmount ?? 0m;
        var tier = order.TierDiscountAmount ?? 0m;

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
    private static string BuildApproximateAddress(Address? address)
    {
        if (address == null) return string.Empty;
        var city = address.City?.Trim();
        if (string.IsNullOrEmpty(city)) return string.Empty;

        var zipPrefix = address.ZipCode?.Trim();
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