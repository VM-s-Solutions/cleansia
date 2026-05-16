namespace Cleansia.Core.Domain.Bookings;

/// <summary>
/// How often a <see cref="RecurringBookingTemplate"/> spawns concrete orders.
/// Values are persisted as ints — don't reorder existing entries.
/// </summary>
public enum RecurrenceFrequency
{
    Weekly = 1,
    Biweekly = 2,
    Monthly = 3,
}
