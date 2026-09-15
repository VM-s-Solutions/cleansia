using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D4 — the scoped per-request buffer backing <see cref="IAuditContext"/>. A sensitive handler
/// records one typed, pre-redacted before/after pair (or, for a customer act, one evidence record);
/// <c>AuditLogBehavior</c> drains it when writing the success row, and both behaviors drain it on the
/// failure arms (subject and resource only). Pure in-memory (no DbContext): the
/// payloads are serialized eagerly to the same camelCase JSON the jsonb columns hold, so the behavior
/// reads back ready-to-store strings and never touches a domain type. The last record in a request wins.
/// Enums go in by name: a row is read years later by a support agent and a lawyer's file, and a name
/// survives a renumbering where an integer silently changes meaning (ADR-0062 D3).
/// </summary>
public sealed class AuditContext : IAuditContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private AuditSnapshot? _snapshot;
    private bool _failureRecorded;

    public bool SuccessRowDeclined { get; private set; }

    public void RecordChange(string resourceType, string resourceId, object before, object after, string? reason = null)
    {
        _snapshot = new AuditSnapshot(
            resourceType,
            resourceId,
            JsonSerializer.Serialize(before, JsonOptions),
            JsonSerializer.Serialize(after, JsonOptions),
            reason);
    }

    public void RecordEvidence(string resourceType, string? resourceId, object? payload, string? actorUserId = null)
    {
        _snapshot = new AuditSnapshot(
            resourceType,
            resourceId,
            BeforeJson: null,
            payload is null ? null : JsonSerializer.Serialize(payload, JsonOptions),
            Reason: null,
            actorUserId);
    }

    public void DeclineSuccessRow()
    {
        SuccessRowDeclined = true;
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
