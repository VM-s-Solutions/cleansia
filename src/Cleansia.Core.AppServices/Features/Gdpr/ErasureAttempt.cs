namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// The scoped per-request marker that an erasure walk has begun for a subject. The deletion service sets
/// it once the blocking checks have passed (a refusal before that point is a business answer, not a failed
/// erasure); <c>ErasureFailureCaptureBehavior</c> reads it after the pipeline has produced its final outcome,
/// which is the only vantage point from which the erasure's single commit can be seen to throw.
/// </summary>
public interface IErasureAttempt
{
    bool Started { get; }

    string? SubjectUserId { get; }

    string? RequestId { get; }

    string? ProcessedBy { get; }

    void Begin(string subjectUserId, string requestId, string processedBy);
}

public sealed class ErasureAttempt : IErasureAttempt
{
    public bool Started { get; private set; }

    public string? SubjectUserId { get; private set; }

    public string? RequestId { get; private set; }

    public string? ProcessedBy { get; private set; }

    public void Begin(string subjectUserId, string requestId, string processedBy)
    {
        Started = true;
        SubjectUserId = subjectUserId;
        RequestId = requestId;
        ProcessedBy = processedBy;
    }
}
