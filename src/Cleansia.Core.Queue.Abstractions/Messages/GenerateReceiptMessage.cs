namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// <paramref name="Reissue"/> distinguishes the two things this queue does for an order.
///
/// <para><c>false</c> — the normal enqueue: issue the receipt (allocate the number, register, render,
/// e-mail). A redelivery is deduped by the committed receipt row.</para>
///
/// <para><c>true</c> — the order already has a receipt and a fact printed on it has changed, which
/// today means a cash sale whose money was collected after the document was issued. The stored PDF is
/// re-rendered over the same number and the same blob. It never allocates a number, never registers
/// with an authority and never sends an e-mail, so a redelivery only restates the same document
/// again.</para>
///
/// <para><paramref name="LanguageCode"/> is a fallback only: the consumer writes the document in the
/// language the order was booked in, or the account's preference, and reaches for this when the order
/// records neither.</para>
///
/// <para>Default <c>false</c>, so a bare message already on the wire from a previous deploy still
/// deserializes to the issue path it was written for.</para>
/// </summary>
public record GenerateReceiptMessage(string OrderId, string LanguageCode, bool Reissue = false);
