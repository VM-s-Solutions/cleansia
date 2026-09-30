namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// Issue the receipt for an order: allocate the number, register, render and e-mail it. A redelivery is
/// deduped by the committed receipt row.
///
/// <para><paramref name="LanguageCode"/> is a fallback only: the consumer writes the document in the
/// language the order was booked in, or the account's preference, and reaches for this when the order
/// records neither.</para>
///
/// <para><paramref name="ReceivableId"/> names a paid receivable on the order, and asks for its fee
/// receipt instead of the order's sale receipt.</para>
/// </summary>
public record GenerateReceiptMessage(string OrderId, string LanguageCode, string? ReceivableId = null);
