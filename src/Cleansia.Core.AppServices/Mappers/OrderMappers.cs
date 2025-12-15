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
            Currency: order.Currency.MapToDetails(),
            SelectedServices: order.SelectedServices.Select(os => os.Service.MapToDetails(order.Currency.Code)),
            StatusHistory: order.OrderStatusHistory.Select(sh => sh.MapToDto()) ?? [],
            CreatedOn: order.CreatedOn,
            UpdatedOn: order.UpdatedOn,
            AssignedEmployeeId: order.EmployeeId,
            AssignedEmployeeName: order.Employee != null
                ? $"{order.Employee.User?.FirstName} {order.Employee.User?.LastName}".Trim()
                : null,
            AssignedEmployeePhone: order.Employee?.User?.PhoneNumber,
            ReceiptNumber: order.Receipt?.ReceiptNumber
        );
    }
}