using System.Text;
using System.Text.Json;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 — what a <c>&lt;queue&gt;-poison</c> consumer is allowed to put in its ALERT.
///
/// <para>D3 assigns the two steps different jobs in its own words: the durable <c>DeadLetter</c> row is
/// "the recovery source", and the <c>LogError</c> is the alert. Those are different audiences with
/// different retention. The row lives in our Postgres behind admin auth; the alert leaves the process
/// into the Functions host's retained log stream AND — since <c>AddSentryMonitoring</c> — into Sentry,
/// a separate vendor. So the alert gets the message's IDENTITY and the row keeps its BYTES.</para>
///
/// <para><b>Why identity is enough.</b> <see cref="QueueEnvelope{T}.MessageKey"/> is the deterministic,
/// frozen identity of every message on every queue (<see cref="MessageKeys"/>), and it already encodes
/// each queue's domain subject: <c>receipt:{OrderId}</c>, <c>invoice:{PayPeriodId}:{EmployeeId}</c>,
/// <c>pay:{OrderId}:{EmployeeId}</c>, <c>push:{UserId}:{EventKey}:{OrderId}</c>,
/// <c>liveactivity:{OrderId}:{EventKey}:{Sequence}</c>, <c>promo:{TenantId}:{ContentHash}</c>, and
/// <c>email:{purpose}:{UserId}:{codeHash}</c> — which <see cref="MessageKeys.HashCode"/> already
/// hashes precisely "so the secret never appears in a key or a log line". The key is therefore both the
/// triage handle and the <c>WHERE</c> clause that finds the row
/// (<c>SourceQueue = … AND RawBody LIKE '%{MessageKey}%'</c>).</para>
///
/// <para><b>Fail-closed by construction, not by denylist.</b> Two envelope fields are read BY NAME and
/// nothing else is copied out of the body — so a message type that gains a field tomorrow is withheld
/// by default rather than needing someone to remember it. That is the opposite polarity to
/// <c>RequestLoggingMiddleware.SensitiveFieldRegex</c>, which is a name denylist and does not (and
/// would not) cover <c>SendEmailMessage.Code</c>.</para>
///
/// <para><see cref="PoisonAlertDescriptor.Fingerprint"/> is what keeps the alert actionable when the
/// body is not a parseable envelope: it ties the alert to exactly one <c>DeadLetter.RawBody</c> without
/// reproducing a byte of it.</para>
/// </summary>
public static class PoisonAlert
{
    /// <summary>The body is not JSON, or not a JSON object — no identity could be read from it.</summary>
    public const string Unparseable = "<unparseable>";

    /// <summary>The body parsed, but carried no <c>messageKey</c> (a bare, pre-envelope payload).</summary>
    public const string Absent = "<absent>";

    /// <summary>
    /// Projects a raw poisoned body down to what may be alerted on. Total: any body — malformed,
    /// truncated, empty, or hostile — yields a descriptor, because the alert must fire regardless.
    /// </summary>
    public static PoisonAlertDescriptor Describe(string? body)
    {
        var raw = body ?? string.Empty;
        var (messageKey, tenantId) = ReadIdentity(raw);

        return new PoisonAlertDescriptor(
            MessageKey: messageKey,
            TenantId: tenantId,
            Bytes: Encoding.UTF8.GetByteCount(raw),
            // The same one-way short hash the send-email key uses for its token segment: one hashing
            // convention on this path, and 64 bits is far more than enough to pick one row out of a
            // dead-letter table.
            Fingerprint: MessageKeys.HashCode(raw));
    }

    private static (string MessageKey, string? TenantId) ReadIdentity(string raw)
    {
        if (raw.Length == 0)
        {
            return (Unparseable, null);
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (Unparseable, null);
            }

            return (ReadString(document.RootElement, "messageKey") ?? Absent,
                ReadString(document.RootElement, "tenantId"));
        }
        catch (JsonException)
        {
            // Includes the depth/format guards System.Text.Json applies — a body that poisons a queue is
            // exactly the body most likely to be malformed, so this is the normal path, not the odd one.
            return (Unparseable, null);
        }
    }

    /// <summary>
    /// Reads ONE top-level property by name, case-insensitively (the wire is camelCase — see
    /// <c>AzureStorageQueueClient</c> — but a hand-replayed body may not be), and only when its value is
    /// a JSON string. A nested object or array is never descended into: the payload is where the
    /// credentials and the PII are.
    /// </summary>
    private static string? ReadString(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
        }

        return null;
    }
}

/// <summary>
/// The alert-safe projection of a poisoned message. Every member of this record is intended to reach the
/// Functions host log stream and Sentry; nothing else from the body may. Adding a member is a security
/// change — see <c>PoisonAlertRedactionTests</c>, which asserts this record's shape.
/// </summary>
/// <param name="MessageKey">The envelope's deterministic identity, or a
/// <see cref="PoisonAlert.Unparseable"/>/<see cref="PoisonAlert.Absent"/> sentinel.</param>
/// <param name="TenantId">The envelope's tenant, so an operator scopes the lookup correctly.</param>
/// <param name="Bytes">UTF-8 size of the body that WAS stored — an at-a-glance "is this truncated".</param>
/// <param name="Fingerprint">One-way short hash of the whole verbatim body; the exact-match handle onto
/// <c>DeadLetter.RawBody</c> when the key is a sentinel or repeats.</param>
public sealed record PoisonAlertDescriptor(string MessageKey, string? TenantId, int Bytes, string Fingerprint);
