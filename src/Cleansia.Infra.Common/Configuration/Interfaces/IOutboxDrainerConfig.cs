namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// Operator-tunable drainer settings (section <c>OutboxDrainer</c>). Batch size, the retry ceiling, and
/// the backoff base are tunables, not business decisions, so they are read from configuration rather
/// than hardcoded.
/// </summary>
public interface IOutboxDrainerConfig
{
    /// <summary>Max rows claimed and attempted per tick. Keeps each run bounded.</summary>
    int BatchSize { get; set; }

    /// <summary>
    /// After this many failed sends a row stops being claimable and is dead-lettered instead of
    /// looping forever.
    /// </summary>
    int MaxAttempts { get; set; }

    /// <summary>Base for the exponential backoff between failed send attempts.</summary>
    int BaseBackoffSeconds { get; set; }

    /// <summary>
    /// How long a claim is held before the row becomes re-claimable. Bounds how long a message waits if
    /// the drainer crashes after claiming but before sending; it must comfortably exceed one drain tick.
    /// </summary>
    int LeaseSeconds { get; set; }
}
