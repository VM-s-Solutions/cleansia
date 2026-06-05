using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// T-0122 (FISCAL-RECON) — the testable Core timer-handler body (the <c>[TimerTrigger]</c> shell stays
/// in the Exe per the T-0121 pattern, sibling to <c>RetryFailedFiscalRegistrations</c>). The handler
/// just drives the reconciliation sweep service once per tick.
/// </summary>
public class FiscalReconciliationTimerHandlerTests
{
    [Fact]
    public async Task HandleAsync_Invokes_The_Reconciliation_Sweep_Once()
    {
        var service = new Mock<IFiscalReconciliationService>();
        service
            .Setup(s => s.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = new FiscalReconciliationTimerHandler(
            service.Object, NullLogger<FiscalReconciliationTimerHandler>.Instance);

        await handler.HandleAsync(CancellationToken.None);

        service.Verify(s => s.ReconcileAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
