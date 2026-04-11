namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Classifies a fiscal authority failure so the retry job can decide
/// whether to retry the request or escalate it to operations.
/// </summary>
public enum FiscalErrorKind
{
    /// <summary>No error (success or not required).</summary>
    None = 0,

    /// <summary>
    /// Network timeout, 5xx, rate limit, circuit breaker open.
    /// Retry with exponential backoff.
    /// </summary>
    Transient = 1,

    /// <summary>
    /// 400/422 bad payload, 403 forbidden, 404 endpoint wrong.
    /// Do not retry — our request is broken. Alert admin.
    /// </summary>
    Permanent = 2,

    /// <summary>
    /// API key missing, certificate expired, wrong endpoint URL.
    /// Do not retry — ops must fix the configuration. Alert immediately.
    /// </summary>
    Configuration = 3,

    /// <summary>
    /// Unclassified response. Retry a few times, then escalate.
    /// </summary>
    Unknown = 4,
}
