using System.Text.Json;
using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// ADR-0002 D3 (F3) — the durable <see cref="DeadLetter"/> repository. Auto-registered by the
/// assembly-scan in <c>RepositoryExtensions</c> (it implements <see cref="IRepository{TEntity,TKey}"/>).
/// </summary>
public class DeadLetterRepository(CleansiaDbContext context)
    : BaseRepository<DeadLetter>(context), IDeadLetterRepository
{
    private static readonly EmailType[] EmailTypes = Enum.GetValues<EmailType>();

    public async Task RemoveForSubjectAsync(string userId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters + the explicit subject predicate: the poison consumer records the body
        // verbatim and derives no tenant from it, so the row carries whatever tenant was ambient on the
        // Functions host — routinely not the erasing request's. A tenant-scoped read would silently leave
        // behind every row it cannot see, and an erasure gets no second pass. A user id belongs to one
        // person in any tenant, so the predicate scopes rather than widens.
        //
        // Contains() is the index-assisted NARROWING only; NamesSubject is the decision.
        var candidates = await GetQueryableIgnoringTenant()
            .Where(d => d.SourceQueue == QueueNames.SendEmail && d.RawBody.Contains(userId))
            .ToListAsync(cancellationToken);

        // Loaded-and-removed rather than ExecuteDelete: the erasure runs inside the caller's unit of work,
        // and an ExecuteDelete would commit on its own connection.
        GetDbSet().RemoveRange(candidates.Where(d => NamesSubject(d.RawBody, userId)));
    }

    private static bool NamesSubject(string rawBody, string userId)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ContainsSubjectKey(rawBody, userId);
            }

            return KeyNamesSubject(ReadString(root, nameof(QueueEnvelope<SendEmailMessage>.MessageKey)), userId)
                || PayloadNamesSubject(root, userId);
        }
        catch (JsonException)
        {
            // A body that poisoned a queue is the body most likely to be TRUNCATED, and the envelope's key
            // leads the wire, so it routinely survives the cut that destroyed the JSON. Falling back to the
            // subject's own key TOKEN — never to the bare user id — keeps this precise: a body carrying
            // that token is carrying this subject's message. Only a body with no token left is out of
            // reach, and nothing here guesses at one.
            return ContainsSubjectKey(rawBody, userId);
        }
    }

    /// <summary>
    /// Derived from the frozen <see cref="MessageKeys.Email"/> formula rather than restating it: the key
    /// with an empty code segment IS the per-subject prefix, so a change to the formula moves this with it.
    /// </summary>
    private static bool KeyNamesSubject(string? messageKey, string userId) =>
        messageKey is not null
        && SubjectKeyPrefixes(userId).Any(prefix => messageKey.StartsWith(prefix, StringComparison.Ordinal));

    private static bool ContainsSubjectKey(string rawBody, string userId) =>
        SubjectKeyPrefixes(userId).Any(prefix => rawBody.Contains(prefix, StringComparison.Ordinal));

    private static IEnumerable<string> SubjectKeyPrefixes(string userId) =>
        EmailTypes.Select(type => MessageKeys.Email(type, userId, string.Empty));

    private static bool PayloadNamesSubject(JsonElement root, string userId)
    {
        if (SubjectOf(root, userId))
        {
            return true;
        }

        // The ADR-0002 D2.1a dual-read shape has no envelope, so the payload's own field is the only handle.
        var payload = ReadObject(root, nameof(QueueEnvelope<SendEmailMessage>.Payload));
        return payload is not null && SubjectOf(payload.Value, userId);
    }

    private static bool SubjectOf(JsonElement element, string userId) =>
        string.Equals(
            ReadString(element, nameof(SendEmailMessage.UserId)), userId, StringComparison.Ordinal);

    private static string? ReadString(JsonElement element, string name) =>
        Find(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static JsonElement? ReadObject(JsonElement element, string name) =>
        Find(element, name) is { ValueKind: JsonValueKind.Object } value ? value : null;

    /// <summary>
    /// One top-level property by name, case-insensitively — the wire is camelCase (see
    /// <c>AzureStorageQueueClient</c>) but a hand-replayed body may not be.
    /// </summary>
    private static JsonElement? Find(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }
}
