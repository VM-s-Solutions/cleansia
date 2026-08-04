using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Moq;

namespace Cleansia.Tests.Common;

/// <summary>
/// Default express-waiver collaborators for suites that are not about the waiver. Both default to
/// "nothing is waived, nothing is reserved, nothing is released", which is the behaviour every test
/// written before ADR-0035 assumed.
/// </summary>
public static class ExpressWaiverMocks
{
    public static Mock<IExpressWaiverResolver> NoWaiver()
    {
        var mock = new Mock<IExpressWaiverResolver>();
        mock.Setup(r => r.ResolveForUserAsync(
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpressWaiver.None("C:2026-08"));
        return mock;
    }

    public static Mock<IExpressWaiverConsumer> NoConsumer()
    {
        var mock = new Mock<IExpressWaiverConsumer>();
        mock.Setup(c => c.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpressWaiver.None("C:2026-08"));
        mock.Setup(c => c.TryReserveAsync(
                It.IsAny<ExpressWaiver>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipBenefitUsage?)null);
        mock.Setup(c => c.ReleaseForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        mock.Setup(c => c.WouldForfeitOnCustomerCancelAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock;
    }
}
