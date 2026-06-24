using System.Text.Json;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D4 — the scoped per-request buffer backing <see cref="IAuditContext"/>. A sensitive handler
/// records one typed, pre-redacted before/after pair; <c>AuditLogBehavior</c> drains it when writing the
/// success row. Pure in-memory (no DbContext): the payloads are serialized eagerly to the same camelCase
/// JSON the jsonb columns hold, so the behavior reads back ready-to-store strings and never touches a
/// domain type. The last <see cref="RecordChange"/> in a request wins.
/// </summary>
public sealed class AuditContext : IAuditContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private AuditSnapshot? _snapshot;
    private bool _failureRecorded;

    public void RecordChange(string resourceType, string resourceId, object before, object after, string? reason = null)
    {
        _snapshot = new AuditSnapshot(
            resourceType,
            resourceId,
            JsonSerializer.Serialize(before, JsonOptions),
            JsonSerializer.Serialize(after, JsonOptions),
            reason);
    }

    public AuditSnapshot? DrainSnapshot()
    {
        var drained = _snapshot;
        _snapshot = null;
        return drained;
    }

    public bool TryClaimFailureRecording()
    {
        if (_failureRecorded)
        {
            return false;
        }

        _failureRecorded = true;
        return true;
    }
}
