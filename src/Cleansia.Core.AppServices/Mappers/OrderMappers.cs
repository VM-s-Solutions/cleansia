using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Mappers;

public static class OrderMappers
{
    public static OrderStatus GetCurrentOrderStatus(this Order order)
    {
        return order.OrderStatusHistory.OrderByDescending(x => x.CreatedOn).FirstOrDefault()!.Status;
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
            HasAvailableSpots: order.HasAvailableSpots
        );
    }

    public static OrderItem MapToDetail(this Order order)
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
            Review: order.Reviews.FirstOrDefault()?.MapToDto()
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
}