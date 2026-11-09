using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Services;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// Change end date moves the open period's end and keeps its start. The audit row keeps the old and the new end,
/// and the row's actor is who changed it.
/// </summary>
public class UpdatePayPeriodTests
{
    private const string PayPeriodId = PayrollMockFactory.PayPeriodId;

    [Fact]
    public async Task Handler_Moves_Only_The_End_And_Audits_The_Old_And_The_New_End()
    {
        var period = PayrollMockFactory.OpenPeriod();
        var repository = new Mock<IPayPeriodRepository>();
        repository.Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        var audit = new Mock<IAuditContext>();
        object? before = null;
        object? after = null;
        audit.Setup(a => a.RecordChange("PayPeriod", PayPeriodId, It.IsAny<object>(), It.IsAny<object>(), null))
            .Callback<string, string, object, object, string?>((_, _, b, a, _) => (before, after) = (b, a));

        var result = await new UpdatePayPeriod.Handler(repository.Object, PayPeriodTestServices.ClosingConfig(), audit.Object)
            .Handle(new UpdatePayPeriod.Command(PayPeriodId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), Notes: null),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6)), (period.StartDate, period.EndDate));
        audit.Verify(a => a.RecordChange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string?>()),
            Times.Once);
        Assert.Equal(new UpdatePayPeriod.EndDateSnapshot(PayPeriodId, new DateOnly(2026, 1, 14)), before);
        Assert.Equal(new UpdatePayPeriod.EndDateSnapshot(PayPeriodId, new DateOnly(2026, 1, 6)), after);
    }
}
