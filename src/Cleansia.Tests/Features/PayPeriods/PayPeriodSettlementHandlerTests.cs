using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// AC3 (T-0171b) — the admin pay-period settlement handlers. The domain guards the legal
/// transitions (<see cref="PayPeriod.MarkAsPaid"/> requires Closed; <see cref="PayPeriod.Reopen"/>
/// refuses a Paid period). These tests pin that each handler drives the domain transition on the
/// happy path and surfaces a BusinessResult error (no throw, no mutation) on the guarded case.
/// Written red → green per knowledge/testing.md.
/// </summary>
public class PayPeriodSettlementHandlerTests
{
    private const string PayPeriodId = "period-1";
    private const string AdminEmail = "admin@cleansia.cz";

    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();

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

    private static PayPeriod PaidPeriod()
    {
        var period = ClosedPeriod();
        period.MarkAsPaid();
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

    // ── ReopenPayPeriod (AC3) ────────────────────────────────────────

    [Fact]
    public async Task ReopenPayPeriod_Moves_Closed_To_Open_And_Clears_ClosedMetadata()
    {
        var period = ClosedPeriod();
        Arrange(period);
        var handler = new ReopenPayPeriod.Handler(_payPeriodRepository.Object);

        var result = await handler.Handle(
            new ReopenPayPeriod.Command(PayPeriodId, Notes: "reopened for correction"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PayPeriodStatus.Open, period.Status);
        Assert.Null(period.ClosedAt);
        Assert.Null(period.ClosedBy);
    }

    [Fact]
    public async Task ReopenPayPeriod_On_Paid_Period_Returns_Error_And_Does_Not_Mutate()
    {
        var period = PaidPeriod();
        Arrange(period);
        var handler = new ReopenPayPeriod.Handler(_payPeriodRepository.Object);

        var result = await handler.Handle(
            new ReopenPayPeriod.Command(PayPeriodId, Notes: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PayPeriodAlreadyPaid, result.Error!.Message);
        Assert.Equal(PayPeriodStatus.Paid, period.Status);
    }
}
