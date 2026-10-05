using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// What a signed-in customer must hold to book cash, beyond the one-cleaner rule of
/// <see cref="BookingPolicy.AllowsCash"/> (owner ruling 2026-09-28): no open receivable, and room under
/// <see cref="BookingPolicy.MaxOpenUnpaidCashBookings"/>. No saved card is asked for (owner ruling
/// 2026-10-04). Asked by the one-off booking, both recurring schedule writes and the confirmation of a
/// recurring cash occurrence, in that order.
/// </summary>
internal static class CustomerCashStanding
{
    /// <summary>Owed to any operating company, like the open-bookings limit.</summary>
    public static async Task<bool> OwesNothingAsync(
        IReceivableRepository receivableRepository, string userId, CancellationToken cancellationToken)
        => !await receivableRepository.HasOpenForUserAsync(userId, cancellationToken);

    /// <summary>
    /// Counted in every operating company the customer booked with. A recurring occurrence the customer
    /// has not confirmed is not a booking yet: it is retracted unless confirmed, so it counts from its
    /// confirmation on, and the occurrence being confirmed is not among them.
    /// </summary>
    public static async Task<bool> HasRoomForAnotherOpenCashBookingAsync(
        IOrderRepository orderRepository, string userId, CancellationToken cancellationToken)
        => await orderRepository.GetCountForOwnerAsync(userId, OpenUnpaidCashBooking, cancellationToken)
           < BookingPolicy.MaxOpenUnpaidCashBookings;

    private static readonly Expression<Func<Order, bool>> OpenUnpaidCashBooking =
        o => o.PaymentType == PaymentType.Cash
             && o.PaymentStatus == PaymentStatus.Pending
             && o.CurrentStatus != OrderStatus.Cancelled
             && o.CurrentStatus != OrderStatus.Completed
             && (o.RecurringTemplateId == null || o.CustomerConfirmedAt != null);
}
