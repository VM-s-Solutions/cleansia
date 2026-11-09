using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// The admin pay-period settlement handler. The domain guards the legal transition
/// (<see cref="PayPeriod.MarkAsPaid"/> requires Closed). These tests pin that the handler drives the domain
/// transition on the happy path and surfaces a BusinessResult error (no throw, no mutation) on the guarded case.
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
}
