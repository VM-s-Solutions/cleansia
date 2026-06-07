namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The single dedicated drainer for the durable outbox. One tick claims a batch of undispatched rows
/// under a lease, sends each row's already-serialized body via the unchanged queue client, and marks a
/// row dispatched ONLY after its send succeeds. A send failure leaves the row claimable for retry; a
/// row that exhausts its retry budget is dead-lettered. Delivery is at-least-once — a crash between the
/// send and the dispatched mark re-sends, and the downstream consumer dedups on the deterministic
/// message key — so a crash between commit and send no longer loses the message.
/// </summary>
public interface IOutboxDrainerService
{
    /// <summary>Runs one bounded drain tick. Returns the number of rows successfully dispatched.</summary>
    Task<int> DrainOnceAsync(CancellationToken cancellationToken);
}
