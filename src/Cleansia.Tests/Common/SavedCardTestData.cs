using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Common;

internal static class SavedCardTestData
{
    public static SavedCard Captured(string userId, string currencyId, int expMonth = 12, int expYear = 2035)
    {
        var card = SavedCard.Start(userId, currencyId, "cus_test", null, null);
        card.Capture("pm_test", "visa", "4242", expMonth, expYear);
        return card;
    }

    public static ISavedCardRepository Repository(string listedCurrencyId = "czk")
    {
        var repository = new Mock<ISavedCardRepository>();
        repository.Setup(r => r.GetCapturedForUserInCurrencyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string currencyId, CancellationToken _) => [Captured(userId, currencyId)]);
        repository.Setup(r => r.GetCapturedForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => [Captured(userId, listedCurrencyId)]);
        return repository.Object;
    }
}
