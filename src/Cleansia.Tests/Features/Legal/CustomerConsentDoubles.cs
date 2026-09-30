using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Legal;

/// <summary>
/// A signed-in customer who holds both legal consents, for the booking validators whose tests are about
/// something else. With no text in force (an unarranged resolver answers null) there is nothing to be
/// behind, so the terms tick is not asked for.
/// </summary>
internal static class CustomerConsentDoubles
{
    public static IUserConsentRepository Consented()
    {
        var consents = new Mock<IUserConsentRepository>();
        consents
            .Setup(r => r.GetByUserIdNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) =>
            [
                UserConsent.Grant(userId, ConsentType.TermsOfService, "203.0.113.9", "Chrome", null),
                UserConsent.Grant(userId, ConsentType.PrivacyPolicy, "203.0.113.9", "Chrome", null),
            ]);
        return consents.Object;
    }
}
