namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// The four completed-order counts the partner dashboard renders, computed in one grouped query
/// (conditional counts over <c>Order.CompletedAt</c>) instead of four sequential COUNT round trips.
/// Every window is half-open <c>[From, To)</c>, matching the dashboard caller's day/week/month math.
/// </summary>
public sealed record CompletedOrderWindowCounts(int ThisMonth, int LastMonth, int Today, int Week)
{
    public static readonly CompletedOrderWindowCounts Empty = new(0, 0, 0, 0);
}
