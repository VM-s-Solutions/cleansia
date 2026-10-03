namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// The customer cancellation schedule an order is booked under, frozen on it by <see cref="Order.Create"/>:
/// free until <see cref="FreeHours"/> before the start, <see cref="PartialFeeRate"/> until
/// <see cref="PartialHours"/>, <see cref="LastMinuteFeeRate"/> after that, and free until
/// <see cref="PlusFreeHours"/> for an entitled Plus member. A booking stays under the terms version it
/// was made under. → /product/business-rules#cancellation
/// </summary>
public sealed record CancellationTerms(
    int FreeHours,
    int PartialHours,
    decimal PartialFeeRate,
    decimal LastMinuteFeeRate,
    int PlusFreeHours);
