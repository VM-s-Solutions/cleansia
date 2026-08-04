using Cleansia.Core.AppServices.Services.Interfaces;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A resolver that declines every hold, for the factory suites that are about something else. The
/// hold's own behaviour is covered by <see cref="PreferredCleanerHoldResolverTests"/> and
/// <see cref="OrderFactoryPreferredHoldTests"/>.
/// </summary>
internal static class NoPreferredCleanerHold
{
    public static IPreferredCleanerHoldResolver Resolver => Grants(null);

    public static IPreferredCleanerHoldResolver Grants(DateTime? holdUntilUtc)
    {
        var resolver = new Mock<IPreferredCleanerHoldResolver>();
        resolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(holdUntilUtc is null
                ? PreferredCleanerOutcome.Declined(HoldDeclineReason.NoPreference)
                : PreferredCleanerOutcome.Granted(holdUntilUtc.Value));
        return resolver.Object;
    }
}
