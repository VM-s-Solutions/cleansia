namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Result of a fiscal registration attempt. Use the factory methods —
/// the positional constructor is for serialization only.
/// </summary>
public record FiscalResult(
    bool IsRegistered,
    bool IsRequired,
    string? FiscalCode,
    string? RegisteredAt,
    FiscalErrorKind ErrorKind,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>
    /// Returned by <c>NoOpFiscalService</c> for countries without a fiscal system.
    /// </summary>
    public static FiscalResult NotRequired() =>
        new(false, false, null, null, FiscalErrorKind.None, null, null);

    /// <summary>
    /// Successful fiscal registration.
    /// </summary>
    public static FiscalResult Success(string code, string registeredAt) =>
        new(true, true, code, registeredAt, FiscalErrorKind.None, null, null);

    /// <summary>
    /// Transient failure — network timeout, 5xx, rate limit.
    /// The retry job will reprocess this with exponential backoff.
    /// </summary>
    public static FiscalResult TransientError(string errorCode, string errorMessage) =>
        new(false, true, null, null, FiscalErrorKind.Transient, errorCode, errorMessage);

    /// <summary>
    /// Permanent failure — the request itself is broken (bad payload, forbidden, not found).
    /// Do not retry. Alert admin for manual correction.
    /// </summary>
    public static FiscalResult PermanentError(string errorCode, string errorMessage) =>
        new(false, true, null, null, FiscalErrorKind.Permanent, errorCode, errorMessage);

    /// <summary>
    /// Configuration failure — API key missing, cert expired, endpoint wrong.
    /// Do not retry. Alert ops immediately.
    /// </summary>
    public static FiscalResult ConfigurationError(string errorCode, string errorMessage) =>
        new(false, true, null, null, FiscalErrorKind.Configuration, errorCode, errorMessage);

    /// <summary>
    /// Unknown failure — response was not classifiable. The retry job will try a few
    /// times before giving up and escalating.
    /// </summary>
    public static FiscalResult UnknownError(string errorCode, string errorMessage) =>
        new(false, true, null, null, FiscalErrorKind.Unknown, errorCode, errorMessage);

    /// <summary>
    /// Backward-compatible factory alias for <see cref="TransientError"/> — kept
    /// so existing call sites compile without changes while they migrate.
    /// </summary>
    public static FiscalResult Error(string errorCode, string errorMessage) =>
        TransientError(errorCode, errorMessage);
}
