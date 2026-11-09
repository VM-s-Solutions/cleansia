using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// The admin pay-period settlement handler. The domain guards the legal transition
/// (<see cref="PayPeriod.MarkAsPaid"/> requires Closed). These tests pin that the handler drives the domain
/// transition on the happy path and surfaces a BusinessResult error (no throw, no mutation) on the guarded case.
/// Written red → green per knowledge/testing.md. The validator lets a period be marked paid only once all its pay is
/// invoiced and every invoice in it is paid or cancelled.
/// </summary>
public class PayPeriodSettlementHandlerTests
{
    private const string PayPeriodId = "period-1";
    private const string AdminEmail = "admin@cleansia.cz";

    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<ICompanySettlementReader> _settlement = new();

    private static PayPeriod OpenPeriod()
    {
        var period = PayPeriod.CreateBiWeekly(new DateOnly(2026, 1, 1));
        period.Id = PayPeriodId;
        return period;
    }

    private static PayPeriod ClosedPeriod()
    {
        var period = OpenPeriod();
        period.Close(AdminEmail);
        return period;
    }

    private void Arrange(PayPeriod period) =>
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);

    // ── MarkPayPeriodPaid (AC3) ──────────────────────────────────────

    [Fact]
    public async Task MarkPayPeriodPaid_Moves_Closed_To_Paid_And_Sets_PaidAt()
    {
        var period = ClosedPeriod();
        Arrange(period);
        var handler = new MarkPayPeriodPaid.Handler(_payPeriodRepository.Object);

        var result = await handler.Handle(
            new MarkPayPeriodPaid.Command(PayPeriodId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PayPeriodStatus.Paid, period.Status);
        Assert.NotNull(period.PaidAt);
    }

    [Fact]
    public async Task MarkPayPeriodPaid_On_Open_Period_Returns_Error_And_Does_Not_Mutate()
    {
        var period = OpenPeriod();
        Arrange(period);
        var handler = new MarkPayPeriodPaid.Handler(_payPeriodRepository.Object);

        var result = await handler.Handle(
            new MarkPayPeriodPaid.Command(PayPeriodId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PayPeriodNotClosed, result.Error!.Message);
        Assert.Equal(PayPeriodStatus.Open, period.Status);
        Assert.Null(period.PaidAt);
    }

    // ── MarkPayPeriodPaid validator ──────────────────────────────────

    [Fact]
    public async Task MarkPayPeriodPaid_Is_Refused_While_Pay_In_The_Period_Is_Not_Invoiced()
    {
        Arrange(ClosedPeriod());
        ArrangeBooks(unpaidInvoices: 0, uninvoicedPayRows: 2);

        var result = await Validator().ValidateAsync(new MarkPayPeriodPaid.Command(PayPeriodId));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.PayPeriodHasUninvoicedPay, error.ErrorMessage);
    }

    [Fact]
    public async Task MarkPayPeriodPaid_Is_Refused_While_An_Invoice_In_The_Period_Is_Neither_Paid_Nor_Cancelled()
    {
        Arrange(ClosedPeriod());
        ArrangeBooks(unpaidInvoices: 1, uninvoicedPayRows: 0);

        var result = await Validator().ValidateAsync(new MarkPayPeriodPaid.Command(PayPeriodId));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.PayPeriodHasUnpaidInvoices, error.ErrorMessage);
    }

    [Fact]
    public async Task MarkPayPeriodPaid_Passes_Once_Everything_In_The_Period_Is_Invoiced_And_Paid_Or_Cancelled()
    {
        Arrange(ClosedPeriod());
        ArrangeBooks(unpaidInvoices: 0, uninvoicedPayRows: 0);

        var result = await Validator().ValidateAsync(new MarkPayPeriodPaid.Command(PayPeriodId));

        Assert.True(result.IsValid);
        _settlement.Verify(r => r.ReadPayPeriodAsync(PayPeriodId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private MarkPayPeriodPaid.Validator Validator()
    {
        _payPeriodRepository.Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new MarkPayPeriodPaid.Validator(_payPeriodRepository.Object, _settlement.Object);
    }

    private void ArrangeBooks(int unpaidInvoices, int uninvoicedPayRows) =>
        _settlement
            .Setup(r => r.ReadPayPeriodAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((unpaidInvoices, uninvoicedPayRows));
}
