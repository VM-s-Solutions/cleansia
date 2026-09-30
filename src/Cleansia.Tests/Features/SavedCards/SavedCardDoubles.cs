using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.SavedCards;

/// <summary>
/// The customer's saved cards as the cash-booking rules read them. Suites about something else take
/// <see cref="Guaranteed"/>: a usable card in whatever currency is asked for, so cash is judged on the
/// suite's own terms alone.
/// </summary>
internal static class SavedCardDoubles
{
    public static ISavedCardRepository Guaranteed() =>
        Holding((userId, currencyId) => [Usable(userId, currencyId)]);

    public static ISavedCardRepository Holding(Func<string, string, IReadOnlyList<SavedCard>> cardsByUserAndCurrency)
    {
        var repository = new Mock<ISavedCardRepository>();
        repository
            .Setup(r => r.GetCapturedForUserInCurrencyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, string currencyId, CancellationToken _) => cardsByUserAndCurrency(userId, currencyId));
        return repository.Object;
    }

    public static SavedCard Usable(string userId, string currencyId) =>
        CapturedExpiring(userId, currencyId, DateTime.UtcNow.AddYears(3));

    public static SavedCard CapturedExpiring(string userId, string currencyId, DateTime expiry)
    {
        var card = SavedCard.Start(userId, currencyId, $"cus_{userId}", null, null);
        card.Capture($"pm_{userId}", "visa", "4242", expiry.Month, expiry.Year);
        return card;
    }
}
