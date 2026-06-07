namespace Cleansia.Core.Clients.Abstractions;

/// <summary>
/// The closed failure-classification taxonomy every outbound integration (Stripe, SendGrid, FCM,
/// Mapbox) maps its failures to at the adapter boundary (ADR-0005). Consumers branch on this class,
/// never on a raw <c>StripeException</c>/<c>HttpRequestException</c>/status code. The set is closed so
/// callers can branch it exhaustively; adding a class is a superseding-ADR decision.
/// </summary>
public enum IntegrationFailureClass
{
    /// <summary>
    /// Retry may succeed: HTTP 408/429/5xx, socket reset, circuit-open. Retried within the
    /// resilience budget (handler) or thrown to trigger a queue retry (consumer, ADR-0002 D3.3).
    /// </summary>
    Transient = 0,

    /// <summary>
    /// Caller error — retry will never succeed: HTTP 4xx except 401/403/408/429 (e.g. 400/404/409/422).
    /// Maps to a deterministic business error (handler) / ack without throwing (consumer).
    /// </summary>
    Permanent = 1,

    /// <summary>
    /// Our credentials/config are wrong: HTTP 401/403, missing/invalid API key, misconfigured
    /// endpoint. NEVER retried; it is an ops incident (Critical-logged + alert), not a caller error.
    /// This is the "Configuration" class in runtime-readiness.md terms.
    /// </summary>
    AuthConfig = 2,

    /// <summary>
    /// No response within the per-attempt budget (Polly timeout / <see cref="System.Threading.Tasks.TaskCanceledException"/>).
    /// Retried like <see cref="Transient"/>, but distinctly metered (a timeout storm is the
    /// circuit-breaker's signal).
    /// </summary>
    Timeout = 3,
}

/// <summary>Helpers for branching on <see cref="IntegrationFailureClass"/>.</summary>
public static class IntegrationFailureClassExtensions
{
    /// <summary>
    /// True when an automatic retry may help: only <see cref="IntegrationFailureClass.Transient"/>
    /// and <see cref="IntegrationFailureClass.Timeout"/>. A <see cref="IntegrationFailureClass.Permanent"/>
    /// or <see cref="IntegrationFailureClass.AuthConfig"/> failure is never retried.
    /// </summary>
    public static bool IsRetryable(this IntegrationFailureClass @class) =>
        @class is IntegrationFailureClass.Transient or IntegrationFailureClass.Timeout;
}
