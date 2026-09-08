using Microsoft.Extensions.Configuration;
using Cleansia.Infra.Common.Configuration;
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
        new PayPeriodClosingConfig(new ConfigurationBuilder().Build()),
        NullLogger<PayPeriodTimerHandler>.Instance);

    /// <summary>
    /// T-0689 follow-up — the nightly pay-period job GENERATES AND EMAILS AN INVOICE PER EMPLOYEE and,
    /// until this switch, had no way to stop it: a hardcoded 02:00 timer and no gate of any kind. That is
    /// the same shape as the retention defect (T-0685), on a money-producing job.
    ///
    /// <para>Gated at the handler rather than inside the service on purpose:
    /// <c>EnsureOpenPeriodAsync</c> is called inline by pay calculation and must keep working, or pay-calc
    /// starts failing with "NoActivePeriod". Only the scheduled sweep is switchable.</para>
    /// </summary>
    [Fact]
    public async Task HandleAsync_Skips_Entirely_When_Disabled()
    {
        var handler = new PayPeriodTimerHandler(
            _payPeriodService.Object,
            new PayPeriodClosingConfig(new ConfigurationBuilder()
                .AddInMemoryCollection([
                    new KeyValuePair<string, string?>("PayPeriodClosing:Enabled", "false")])
                .Build()),
            NullLogger<PayPeriodTimerHandler>.Instance);

        await handler.HandleAsync(CancellationToken.None);

        _payPeriodService.Verify(
            s => s.CloseExpiredPeriodsAndOpenNewAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An absent section means the job RUNS — "off" has to be typed.</summary>
    [Fact]
    public void An_Absent_Section_Binds_Enabled_True()
    {
        Assert.True(new PayPeriodClosingConfig(new ConfigurationBuilder().Build()).Enabled);
    }
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
