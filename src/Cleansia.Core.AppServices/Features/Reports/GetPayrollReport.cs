#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Cleansia.Core.AppServices.Features.Reports;

public class GetPayrollReport
{
    public record Query(ReportFilter Filter) : IQuery<PayrollReportDto>;

    internal class Handler(IEmployeeInvoiceRepository employeeInvoiceRepository)
        : IRequestHandler<Query, BusinessResult<PayrollReportDto>>
    {
        public async Task<BusinessResult<PayrollReportDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var invoices = await employeeInvoiceRepository
                .GetAllInvoicesByDateRange(request.Filter.StartDate, request.Filter.EndDate)
                .ToListAsync(cancellationToken);

            var totalPayroll = invoices.Sum(i => i.TotalAmount);
            var totalInvoices = invoices.Count;
            var uniqueEmployees = invoices.Select(i => i.EmployeeId).Distinct().Count();
            var averagePayPerEmployee = uniqueEmployees > 0 ? totalPayroll / uniqueEmployees : 0;

            var pendingInvoices = invoices.Count(i => i.Status == EmployeeInvoiceStatus.Pending);
            var approvedInvoices = invoices.Count(i => i.Status == EmployeeInvoiceStatus.Approved);
            var paidInvoices = invoices.Count(i => i.Status == EmployeeInvoiceStatus.Paid);
            var cancelledInvoices = invoices.Count(i => i.Status == EmployeeInvoiceStatus.Cancelled);

            var totalBonuses = invoices.Sum(i => i.BonusAmount);
            var totalDeductions = invoices.Sum(i => i.DeductionAmount);

            var employeeSummaries = invoices
                .GroupBy(i => new { i.EmployeeId, EmployeeName = GetEmployeeName(i) })
                .Select(g => new EmployeePayrollSummary(
                    EmployeeId: g.Key.EmployeeId,
                    EmployeeName: g.Key.EmployeeName,
                    TotalOrders: g.Sum(i => i.TotalOrders),
                    InvoiceCount: g.Count(),
                    SubTotal: g.Sum(i => i.SubTotal),
                    BonusAmount: g.Sum(i => i.BonusAmount),
                    DeductionAmount: g.Sum(i => i.DeductionAmount),
                    TotalAmount: g.Sum(i => i.TotalAmount)))
                .OrderByDescending(e => e.TotalAmount)
                .ToList();

            var payrollByStatus = invoices
                .GroupBy(i => i.Status)
                .Select(g => new PayrollByStatus(
                    StatusCode: g.Key.ToString(),
                    StatusName: g.Key.MapToCode().Name,
                    InvoiceCount: g.Count(),
                    TotalAmount: g.Sum(i => i.TotalAmount)))
                .ToList();

            var monthlyPayroll = invoices
                .GroupBy(i => new { i.GeneratedAt.Year, i.GeneratedAt.Month })
                .Select(g => new MonthlyPayroll(
                    Year: g.Key.Year,
                    Month: g.Key.Month,
                    MonthName: CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    TotalAmount: g.Sum(i => i.TotalAmount),
                    InvoiceCount: g.Count()))
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            return new PayrollReportDto(
                TotalPayroll: totalPayroll,
                AveragePayPerEmployee: averagePayPerEmployee,
                TotalInvoices: totalInvoices,
                PendingInvoices: pendingInvoices,
                ApprovedInvoices: approvedInvoices,
                PaidInvoices: paidInvoices,
                CancelledInvoices: cancelledInvoices,
                TotalBonuses: totalBonuses,
                TotalDeductions: totalDeductions,
                EmployeeSummaries: employeeSummaries,
                PayrollByStatus: payrollByStatus,
                MonthlyPayroll: monthlyPayroll);
        }

        private static string GetEmployeeName(Core.Domain.EmployeePayroll.EmployeeInvoice invoice)
        {
            var user = invoice.Employee?.User;
            if (user == null)
                return string.Empty;

            return $"{user.FirstName} {user.LastName}".Trim();
        }
    }
}