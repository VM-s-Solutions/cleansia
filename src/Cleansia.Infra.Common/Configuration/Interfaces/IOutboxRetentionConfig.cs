namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// Operator-tunable retention-prune settings (section <c>OutboxRetention</c>). The prune is table-growth
/// hygiene only — it deletes rows that are already terminal (Dispatched outbox / old processed-inbox) and
/// never changes dispatch or idempotency behavior. The windows and the on/off switch are tunables, not
/// business decisions, so they are read from configuration rather than hardcoded.
/// </summary>
public interface IOutboxRetentionConfig
{
    /// <summary>Master switch. When false the prune timer is a no-op, so it can be disabled per environment.</summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Age (in days, measured from <c>DispatchedOn</c>) past which a terminal <c>Dispatched</c> outbox row is
    /// eligible for deletion. Only Dispatched rows are ever considered — never a Pending or Failed row.
    /// </summary>
    int DispatchedRetentionDays { get; set; }

    /// <summary>
    /// Age (in days, measured from <c>ProcessedAt</c>) past which a processed-inbox idempotency row is eligible
    /// for deletion. The claim only needs to survive while a redelivery is plausible; rows older than this
    /// window can be pruned without weakening duplicate-suppression for in-flight work.
    /// </summary>
    int ProcessedRetentionDays { get; set; }

    /// <summary>Max rows deleted per batch. Keeps each prune run bounded — no single unbounded <c>DELETE</c>.</summary>
    int BatchSize { get; set; }
}
