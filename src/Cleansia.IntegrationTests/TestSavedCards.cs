using Cleansia.Core.Domain.Users;

namespace Cleansia.IntegrationTests;

/// <summary>
/// A card the customer saved as the guarantee for cash bookings, for the arrange steps that book cash
/// through the real pipeline: without a usable one in the booking's currency a signed-in customer's cash
/// booking is refused (owner ruling 2026-09-28).
/// </summary>
public static class TestSavedCards
{
    public static SavedCard Usable(string userId, string currencyId) =>
        Expiring(userId, currencyId, DateTime.UtcNow.AddYears(3));

    public static SavedCard Expiring(string userId, string currencyId, DateTime expiry)
    {
        var card = SavedCard.Start(userId, currencyId, $"cus_{userId}", null, null);
        card.Capture($"pm_{userId}_{currencyId}", "visa", "4242", expiry.Month, expiry.Year);
        return card;
    }
}
