using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// The admin's period page reads three counts with the period: every invoice issued in it, whatever its status,
/// which is what its View invoices link lists; the invoices neither paid nor cancelled; and the pay rows no invoice
/// holds. Mark paid waits on the last two. The cleaners' period list carries none of them.
/// </summary>
public class GetPayPeriodByIdHandlerTests
{
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IPayPeriodClosingConfig> _closingConfig = new();
    private readonly Mock<ICompanySettlementReader> _settlement = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();

    [Fact]
    public async Task The_Period_Comes_With_Its_Invoice_Unpaid_And_Uninvoiced_Counts()
    {
        var period = PayrollMockFactory.ClosedPeriod();
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _closingConfig.Setup(c => c.LengthInDays).Returns(PayPeriod.LengthInDays);
        _settlement
            .Setup(s => s.ReadPayPeriodAsync(period.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 2));
        var cancelled = PayrollMockFactory.Invoice();
        cancelled.Cancel("Wrong address", "admin@cleansia.cz");
        EmployeeInvoice[] invoices =
        [
            PayrollMockFactory.Invoice().Approve("admin@cleansia.cz").MarkAsPaid(),
            PayrollMockFactory.Invoice(),
            cancelled,
            PayrollMockFactory.Invoice(payPeriodId: "period-2"),
        ];
        _invoiceRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<EmployeeInvoice, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<EmployeeInvoice, bool>> filter, CancellationToken _) =>
                invoices.Count(filter.Compile()));

        var dto = await HandleAsync(period.Id);

        Assert.Equal((3, 1, 2), (dto.InvoiceCount, dto.UnpaidInvoiceCount, dto.UninvoicedPayCount));
        Assert.Equal(period.Id, dto.Id);
    }

    [Fact]
    public void The_Period_List_Mapping_Carries_No_Counts()
    {
        var dto = PayrollMockFactory.OpenPeriod().MapToDto(PayPeriod.LengthInDays);

        Assert.Equal((null, null, null), (dto.InvoiceCount, dto.UnpaidInvoiceCount, dto.UninvoicedPayCount));
    }

    // The handler is internal, like GetRevenueReport's.
    private async Task<PayPeriodDto> HandleAsync(string payPeriodId)
    {
        var handler = Activator.CreateInstance(
            typeof(GetPayPeriodById).GetNestedType("Handler", BindingFlags.NonPublic)!,
            _payPeriodRepository.Object,
            _closingConfig.Object,
            _settlement.Object,
            _invoiceRepository.Object)!;
        var result = await (Task<BusinessResult<PayPeriodDto>>)handler.GetType()
            .GetMethod("Handle")!
            .Invoke(handler, [new GetPayPeriodById.Query(payPeriodId), CancellationToken.None])!;
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }
}
