using System.Net;
using Cleansia.Core.Clients.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// T-0144 / ADR-0005 D2 — the closed failure-classification taxonomy
/// (<see cref="IntegrationFailureClass"/>) that every outbound integration codes against.
/// This ticket seeds the single ADR-frozen classifier in
/// <c>Cleansia.Core.Clients.Abstractions</c> (D2.1) and wires the Stripe + SendGrid boundaries
/// to it; T-0145/BLIND-6 generalises the per-provider mappers on top of THIS taxonomy (it does
/// not introduce a second one).
///
/// AC5 boundary test, written test-first (RED until the classifier exists): a simulated transient
/// failure classifies <c>Transient</c>; a 401/403 classifies <c>AuthConfig</c> (the "Configuration"
/// class in runtime-readiness.md terms) and — per D2 — is never retried.
/// </summary>
public class IntegrationFailureClassifierTests
{
    // ── Transient: retry may succeed (HTTP 408/429/5xx, timeout, socket reset) ──────────

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)] // 500
    [InlineData(HttpStatusCode.BadGateway)]          // 502
    [InlineData(HttpStatusCode.ServiceUnavailable)]  // 503
    [InlineData(HttpStatusCode.GatewayTimeout)]      // 504
    [InlineData(HttpStatusCode.RequestTimeout)]      // 408
    [InlineData((HttpStatusCode)429)]                // Too Many Requests
    public void Transient_Statuses_Classify_Transient(HttpStatusCode status)
    {
        Assert.Equal(IntegrationFailureClass.Transient, IntegrationFailureClassifier.FromHttpStatus((int)status));
    }

    [Fact]
    public void Simulated_Transient_Network_Failure_Classifies_Transient()
    {
        var failure = IntegrationFailureClassifier.FromException(new HttpRequestException("connection reset"));

        Assert.Equal(IntegrationFailureClass.Transient, failure);
    }

    [Fact]
    public void Timeout_Classifies_Timeout_Which_Is_Retryable_Like_Transient()
    {
        var failure = IntegrationFailureClassifier.FromException(new TaskCanceledException("per-attempt timeout"));

        // D2: Timeout is a distinct class but is retried like Transient.
        Assert.Equal(IntegrationFailureClass.Timeout, failure);
        Assert.True(failure.IsRetryable());
    }

    // ── AuthConfig: our credentials/config are wrong (401/403) — NEVER retried ───────────

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)] // 401
    [InlineData(HttpStatusCode.Forbidden)]    // 403
    public void Auth_Statuses_Classify_AuthConfig_And_Are_Not_Retried(HttpStatusCode status)
    {
        var failure = IntegrationFailureClassifier.FromHttpStatus((int)status);

        Assert.Equal(IntegrationFailureClass.AuthConfig, failure);
        Assert.False(failure.IsRetryable());
    }

    // ── Permanent: caller error, retry never succeeds (4xx except 401/403/408/429) ───────

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]          // 400
    [InlineData(HttpStatusCode.NotFound)]            // 404
    [InlineData(HttpStatusCode.Conflict)]            // 409
    [InlineData(HttpStatusCode.UnprocessableEntity)] // 422
    public void Permanent_Statuses_Classify_Permanent_And_Are_Not_Retried(HttpStatusCode status)
    {
        var failure = IntegrationFailureClassifier.FromHttpStatus((int)status);

        Assert.Equal(IntegrationFailureClass.Permanent, failure);
        Assert.False(failure.IsRetryable());
    }

    [Fact]
    public void Only_Transient_And_Timeout_Are_Retryable()
    {
        Assert.True(IntegrationFailureClass.Transient.IsRetryable());
        Assert.True(IntegrationFailureClass.Timeout.IsRetryable());
        Assert.False(IntegrationFailureClass.Permanent.IsRetryable());
        Assert.False(IntegrationFailureClass.AuthConfig.IsRetryable());
    }
}
