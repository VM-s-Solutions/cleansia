namespace Cleansia.Core.Domain.Users;

/// <summary>
/// Records that an erasure attempt failed. The erasure is one commit, so on a throw or a refusal after
/// the walk started the request row staged in the action's own context is rolled back with everything
/// else — the record of the failure must be written in its OWN independently committed scope, the way
/// the audit failure row is. Best-effort: the caller swallows a sink failure and still propagates the
/// erasure's own error.
/// </summary>
public interface IGdprDeletionFailureSink
{
    /// <param name="requestId">
    /// The id of the request row this attempt was working on. Reused when the row is not durable yet
    /// (a first attempt whose commit rolled back), appended to when it is (a retry).
    /// </param>
    Task RecordFailureAsync(
        string subjectUserId,
        string requestId,
        string processedBy,
        string note,
        CancellationToken cancellationToken);
}
