using Cleansia.Core.AppServices.Features.Orders.DTOs;
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
            EstimatedTime: order.EstimatedTime,
            OrderStatus: order.GetCurrentOrderStatus().MapToCode(),
            ConfirmationCode: order.ConfirmationCode,
            StripeSessionId: order.StripeSessionId,
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
            EstimatedTime: order.EstimatedTime,
            ActualCompletionTime: order.ActualCompletionTime,
            CompletionNotes: order.CompletionNotes,
            OrderStatus: order.GetCurrentOrderStatus().MapToCode(),
            ConfirmationCode: order.ConfirmationCode,
            StripeSessionId: order.StripeSessionId,
            Notes: order.Notes,
            SpecialInstructions: order.SpecialInstructions,
            AccessInstructions: order.AccessInstructions,
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
            UserId: review.UserId,
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
            PhoneNumber: user?.PhoneNumber,
            Email: user?.Email
        );
    }
}