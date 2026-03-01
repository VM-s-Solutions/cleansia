using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Features.PayConfig.DTOs;
using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.AppServices.Mappers;

public static class EmployeePayrollMappers
{
    public static OrderEmployeePayDto MapToDto(this OrderEmployeePay orderPay) =>
        new(
            orderPay.Id,
            orderPay.OrderId,
            orderPay.Order?.DisplayOrderNumber ?? orderPay.OrderId,
            orderPay.EmployeeId,
            orderPay.Employee != null ? $"{orderPay.Employee.User?.FirstName} {orderPay.Employee.User?.LastName}".Trim() : "Unknown",
            orderPay.PayPeriodId,
            orderPay.PayPeriod?.GetPeriodLabel() ?? "",
            orderPay.BasePay,
            orderPay.ExtrasPay,
            orderPay.ExpensesPay,
            orderPay.BonusPay,
            orderPay.DeductionPay,
            orderPay.TotalPay,
            orderPay.PayBreakdown,
            orderPay.IsApproved,
            orderPay.CreatedOn.DateTime
        );

    public static EmployeeInvoiceDto MapToDto(this EmployeeInvoice invoice) =>
        new(
            invoice.Id,
            invoice.EmployeeId,
            invoice.Employee != null ? $"{invoice.Employee.User?.FirstName} {invoice.Employee.User?.LastName}".Trim() : "Unknown",
            invoice.PayPeriodId,
            invoice.PayPeriod?.GetPeriodLabel() ?? "",
            invoice.InvoiceNumber,
            invoice.VariableSymbol,
            invoice.PaymentReference,
            invoice.TotalOrders,
            invoice.SubTotal,
            invoice.BonusAmount,
            invoice.DeductionAmount,
            invoice.TotalAmount,
            invoice.Currency?.Code ?? "",
            invoice.Status,
            invoice.PdfBlobUrl,
            invoice.GeneratedAt,
            invoice.ApprovedAt,
            invoice.ApprovedBy,
            invoice.PaidAt,
            invoice.AdminNotes,
            invoice.BankTransferNote
        );

    public static PayPeriodDto MapToDto(this PayPeriod period) =>
        new(
            period.Id,
            period.StartDate,
            period.EndDate,
            period.Status.ToString(),
            period.GetPeriodLabel(),
            period.GetPeriodDays(),
            period.ClosedAt,
            period.ClosedBy,
            period.PaidAt,
            period.Notes
        );

    public static EmployeePayConfigDto MapToDto(this EmployeePayConfig config) =>
        new(
            config.Id,
            config.ServiceId,
            config.Service?.Name,
            config.PackageId,
            config.Package?.Name,
            config.BasePay,
            config.ExtraPerRoom,
            config.ExtraPerBathroom,
            config.DistanceRatePerKm,
            config.MinimumPay,
            config.MaximumPay,
            config.CurrencyId,
            config.Currency?.Code ?? "",
            config.Description,
            config.CreatedOn.DateTime
        );

    public static EmployeeInvoiceDetailDto MapToDetailDto(this EmployeeInvoice invoice) =>
        new(
            invoice.Id,
            invoice.EmployeeId,
            invoice.Employee != null ? $"{invoice.Employee.User?.FirstName} {invoice.Employee.User?.LastName}".Trim() : "Unknown",
            invoice.PayPeriodId,
            invoice.PayPeriod?.GetPeriodLabel() ?? "",
            invoice.InvoiceNumber,
            invoice.VariableSymbol,
            invoice.SpecificSymbol,
            invoice.PaymentReference,
            invoice.TotalOrders,
            invoice.SubTotal,
            invoice.BonusAmount,
            invoice.DeductionAmount,
            invoice.TotalAmount,
            invoice.Currency?.Code ?? "",
            invoice.Status,
            invoice.PdfBlobUrl,
            invoice.GeneratedAt,
            invoice.ApprovedAt,
            invoice.ApprovedBy,
            invoice.PaidAt,
            invoice.AdminNotes,
            invoice.BankTransferNote,
            invoice.OrderPays.Select(op => op.MapToDto()).ToList()
        );
}
