using System.Net.Sockets;
using FirebaseAdmin.Messaging;
using SendGrid;
using Stripe;

namespace Cleansia.Core.Clients.Abstractions;

/// <summary>
/// The single mapper from an outbound failure (HTTP status / transport exception / provider error
/// type) to the closed <see cref="IntegrationFailureClass"/>. Pure, unit-testable, no I/O.
///
/// Exists so every integration boundary (Stripe, SendGrid, FCM, Mapbox) classifies + logs at the
/// boundary against ONE taxonomy. The provider-specific mappers (SendGrid <see cref="Response"/>,
/// <see cref="StripeException"/>, FCM <see cref="MessagingErrorCode"/>) reduce to the
/// <see cref="FromHttpStatus"/> / <see cref="FromException"/> primitives at the floor — they do not
/// introduce a second classification.
/// </summary>
public static class IntegrationFailureClassifier
{
    /// <summary>
    /// Classify an HTTP status code:
    /// 408/429/5xx → Transient; 401/403 → AuthConfig; other 4xx → Permanent; anything else → Transient
    /// (a non-success outside the known families is treated as retryable rather than silently dropped).
    /// </summary>
    public static IntegrationFailureClass FromHttpStatus(int statusCode)
    {
        return statusCode switch
        {
            401 or 403 => IntegrationFailureClass.AuthConfig,
            408 or 429 => IntegrationFailureClass.Transient,
            >= 500 and <= 599 => IntegrationFailureClass.Transient,
            >= 400 and <= 499 => IntegrationFailureClass.Permanent,
            _ => IntegrationFailureClass.Transient,
        };
    }

    /// <summary>
    /// Classify a transport-level exception (no HTTP response): a timeout/cancellation →
    /// <see cref="IntegrationFailureClass.Timeout"/>; a socket/connection fault
    /// (<see cref="HttpRequestException"/>/<see cref="SocketException"/>/<see cref="IOException"/>) →
    /// <see cref="IntegrationFailureClass.Transient"/>; anything else → Transient (conservative —
    /// an unknown transport fault is more likely a blip than a permanent caller error).
    /// </summary>
    public static IntegrationFailureClass FromException(Exception exception)
    {
        return exception switch
        {
            TaskCanceledException or TimeoutException or OperationCanceledException
                => IntegrationFailureClass.Timeout,
            HttpRequestException or SocketException or IOException
                => IntegrationFailureClass.Transient,
            _ => IntegrationFailureClass.Transient,
        };
    }

    /// <summary>Classify a failed SendGrid <see cref="Response"/> by its HTTP status.</summary>
    public static IntegrationFailureClass FromSendGridResponse(Response response) =>
        FromHttpStatus((int)response.StatusCode);

    /// <summary>
    /// Classify a <see cref="StripeException"/> by the HTTP status Stripe surfaces on it; when the
    /// status is absent (a pure transport fault), fall back to <see cref="FromException"/>.
    /// </summary>
    public static IntegrationFailureClass FromStripeException(StripeException exception)
    {
        var status = (int)exception.HttpStatusCode;
        return status > 0 ? FromHttpStatus(status) : FromException(exception);
    }

    /// <summary>
    /// Classify an FCM per-token <see cref="MessagingErrorCode"/>. The dead-token codes
    /// (<see cref="MessagingErrorCode.Unregistered"/>/<see cref="MessagingErrorCode.InvalidArgument"/>/
    /// <see cref="MessagingErrorCode.SenderIdMismatch"/>) are <see cref="IntegrationFailureClass.Permanent"/>;
    /// <see cref="MessagingErrorCode.ThirdPartyAuthError"/> is <see cref="IntegrationFailureClass.AuthConfig"/>;
    /// the rest (Unavailable/Internal/QuotaExceeded, or an absent code) are retryable.
    /// </summary>
    public static IntegrationFailureClass FromFcmErrorCode(MessagingErrorCode? code) =>
        code switch
        {
            MessagingErrorCode.Unregistered
                or MessagingErrorCode.InvalidArgument
                or MessagingErrorCode.SenderIdMismatch => IntegrationFailureClass.Permanent,
            MessagingErrorCode.ThirdPartyAuthError => IntegrationFailureClass.AuthConfig,
            _ => IntegrationFailureClass.Transient,
        };

    /// <summary>
    /// True when an FCM error means the token is permanently dead and its device row should be pruned:
    /// exactly the per-token <see cref="IntegrationFailureClass.Permanent"/> codes. A transient code or
    /// an absent code leaves the token in place for the next dispatch.
    /// </summary>
    public static bool IsDeadFcmToken(MessagingErrorCode? code) =>
        FromFcmErrorCode(code) == IntegrationFailureClass.Permanent;
}
