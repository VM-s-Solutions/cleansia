using System.Reflection;
using Cleansia.Core.AppServices.Features.Reports;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.Reports;

/// <summary>
/// The payroll report's money is what the company owes on its invoices. A Rejected invoice, which the removed
/// Reject wrote, is owed nothing, just like a cancelled one.
/// </summary>
public class GetPayrollReportHandlerTests
{
    private const string CzkId = "currency-czk";

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    /// <summary>
    /// 500 + 80 - 30 = 550 is owed. The Rejected 300 + 20 - 10 = 310 and the cancelled 200 are not, so neither is in
    /// the totals, the bonuses, the deductions, the average, the employee rows or the months. All three invoices
    /// still show in the counts and in their status rows.
    /// </summary>
    [Fact]
    public async Task A_Rejected_Invoice_Is_Left_Out_Of_The_Money_Like_A_Cancelled_One()
    {
        var owed = PayrollMockFactory.Invoice(subTotal: 500m, bonusAmount: 80m, deductionAmount: 30m, employeeId: "emp-owed", currencyId: CzkId);
        var rejected = PayrollMockFactory.Invoice(subTotal: 300m, bonusAmount: 20m, deductionAmount: 10m, employeeId: "emp-rejected", currencyId: CzkId);
        typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.Status))!.SetValue(rejected, EmployeeInvoiceStatus.Rejected);
        var cancelled = PayrollMockFactory.Invoice(subTotal: 200m, employeeId: "emp-cancelled", currencyId: CzkId);
        cancelled.Cancel("Wrong address", "admin@cleansia.cz");
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CzkId;
        _currencyRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(czk);
        _invoiceRepository
            .Setup(r => r.GetAllByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), CzkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owed, rejected, cancelled]);

        var report = await ReportAsync();

        Assert.Equal((550m, 80m, 30m), (report.TotalPayroll, report.TotalBonuses, report.TotalDeductions));
        Assert.Equal(550m, report.AveragePayPerEmployee);
        Assert.Equal("emp-owed", Assert.Single(report.EmployeeSummaries).EmployeeId);
        Assert.Equal(550m, report.MonthlyPayroll.Sum(m => m.TotalAmount));
        Assert.Equal((3, 1), (report.TotalInvoices, report.CancelledInvoices));
        Assert.Equal(310m, report.PayrollByStatus.Single(s => s.StatusCode == nameof(EmployeeInvoiceStatus.Rejected)).TotalAmount);
    }

    // The handler is internal, like GetRevenueReport's.
    private async Task<PayrollReportDto> ReportAsync()
    {
        var handler = Activator.CreateInstance(
            typeof(GetPayrollReport).GetNestedType("Handler", BindingFlags.NonPublic)!,
            _invoiceRepository.Object,
            _currencyRepository.Object)!;
        var filter = new ReportFilter(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc));
        var result = await (Task<BusinessResult<PayrollReportDto>>)handler.GetType()
            .GetMethod("Handle")!
            .Invoke(handler, [new GetPayrollReport.Query(filter), CancellationToken.None])!;
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }
}
