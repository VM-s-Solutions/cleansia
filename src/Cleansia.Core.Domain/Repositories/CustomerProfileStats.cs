namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// The customer profile hero stats, computed from the user's orders in one
/// repository call (T-0392): total bookings placed, and the total money saved
/// (tier + promo + membership discounts). Savings are the REALIZED discounts —
/// only orders that are not Cancelled and not (fully or partially) Refunded —
/// summed in a single currency (<see cref="SavingsCurrencyCode"/>, taken from
/// the user's most recent realized order); orders in a different currency are
/// excluded so the scalar never mixes units. <see cref="SavingsCurrencyCode"/>
/// is null (and savings 0) when the user has no realized orders.
/// </summary>
public sealed record CustomerProfileStats(int TotalBookings, decimal TotalSavings, string? SavingsCurrencyCode)
{
    public static readonly CustomerProfileStats Empty = new(0, 0m, null);
}
