using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>A market resolver for membership handler tests: every country (or none) resolves to one currency, CZK by default.</summary>
internal static class MarketResolution
{
    public static Mock<ICurrencyResolutionService> Resolving(Currency? currency = null)
    {
        var mock = new Mock<ICurrencyResolutionService>();
        mock.Setup(s => s.ResolveCurrencyForCountryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency ?? MembershipPricingMockFactory.Czk());
        return mock;
    }
}
