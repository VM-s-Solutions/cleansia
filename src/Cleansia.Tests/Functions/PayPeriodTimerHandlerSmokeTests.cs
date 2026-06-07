using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class PayPeriodTimerHandlerSmokeTests
{
    private readonly Mock<IPayPeriodBackgroundService> _payPeriodService = new();

    private PayPeriodTimerHandler CreateHandler() => new(
        _payPeriodService.Object,
        NullLogger<PayPeriodTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Closes_Periods_Once()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _payPeriodService.Verify(
            s => s.CloseExpiredPeriodsAndOpenNewAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
