using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// A real issuer over a repository holding nothing — for the receipt and fiscal-retry consumers,
/// whose subject is the e-mail's at-most-once delivery rather than the credential it carries.
/// </summary>
internal static class TestGuestOrderAccessTokenIssuer
{
    internal static GuestOrderAccessTokenIssuer WithNoLiveTokens()
    {
        var repository = new Mock<IGuestOrderAccessTokenRepository>();
        repository
            .Setup(r => r.GetLiveForOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuestOrderAccessToken>());
        return new GuestOrderAccessTokenIssuer(repository.Object);
    }
}
