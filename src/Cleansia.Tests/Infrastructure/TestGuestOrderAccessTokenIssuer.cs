using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// A real issuer over a repository holding nothing — for the booking, status-e-mail and receipt
/// handlers, whose subject is the act they perform rather than the credential that rides along with
/// it. Minting stages a row and reads nothing; only <c>RevokeAsync</c> reads, which is why the empty
/// answer is configured rather than left to Moq's default (it hands back a null list for
/// <c>Task&lt;IReadOnlyList&lt;T&gt;&gt;</c>, not an empty one).
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
