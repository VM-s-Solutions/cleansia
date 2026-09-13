using System.Text.Json;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;

namespace Cleansia.Tests.Logging;

/// <summary>
/// ADR-0062 D6 / S6 — a customer audit entry carries the subject's <c>payloadJson</c>, <c>ipAddress</c>
/// and <c>deviceLabel</c>, and the timeline lists the same rows. None of those names is in either
/// redaction list, and the device label is client-controlled text no denylist reaches, so the three
/// <c>/api/CustomerAudit/…</c> routes are suppressed wholesale — the <c>gdpr/</c> shape, on every host.
/// The response body is built from the real DTOs through the hosts' own serializer options, so a
/// renamed member cannot make this pass by accident.
/// </summary>
public class RequestLogCustomerAuditPathSuppressionTests
{
    private const string Suppressed = "[suppressed: sensitive endpoint]";
    private const string Ip = "203.0.113.9";
    private const string Device = "iPhone 15 / iOS 17.4 <img src=x onerror=alert(1)>";
    private const string Payload = "{\"feeRate\":0.5,\"hasBeenAccepted\":true}";

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task GetById_ThePayloadAndRequestMetadata_NeverReachTheLog(Type middlewareType)
    {
        var json = JsonSerializer.Serialize(new CustomerActionAuditDetailDto(
            Id: "aud-1", UserId: "user-1", ClientAudience: "cleansia.customer", IpAddress: Ip, DeviceLabel: Device,
            DeviceId: "device-1", Action: "customer.order.cancel", ResourceType: "Order", ResourceId: "order-1",
            Success: true, ErrorCode: null, OccurredOn: DateTimeOffset.UtcNow, PayloadJson: Payload,
            CorrelationId: "corr-1"), RequestLoggingHarness.WireOptions);

        AssertWouldOtherwiseBeVisible(middlewareType, json);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/CustomerAudit/get-by-id/aud-1", responseJson: json, authenticatedUserId: "admin-1");

        Assert.NotEmpty(logged);
        Assert.All(logged, message =>
        {
            Assert.DoesNotContain("payloadJson", message);
            Assert.DoesNotContain("ipAddress", message);
            Assert.DoesNotContain("deviceLabel", message);
            Assert.DoesNotContain(Ip, message);
            Assert.DoesNotContain(Device, message);
        });
        Assert.Contains(logged, message => message.Contains(Suppressed));
    }

    [Theory]
    [InlineData("/api/CustomerAudit/get-paged?filter.userId=user-1")]
    [InlineData("/api/CustomerAudit/timeline?userId=user-1")]
    public async Task ThePagedListAndTheTimeline_AreSuppressedOnEveryHost(string route)
    {
        var json = JsonSerializer.Serialize(new PagedData<TimelineEntryDto>(1, 20, 1,
        [
            new TimelineEntryDto(TimelineSource.Customer, "aud-1", DateTimeOffset.UtcNow, "user-1",
                "customer.order.cancel", "Order", "order-1", true, null),
        ]), RequestLoggingHarness.WireOptions);

        foreach (var middlewareType in RequestLoggingHarness.AllHostMiddleware)
        {
            var logged = await RequestLoggingHarness.RunAsync(middlewareType, route, responseJson: json, authenticatedUserId: "admin-1");

            Assert.Contains(logged, message => message.Contains(Suppressed));
            Assert.All(logged, message => Assert.DoesNotContain("customer.order.cancel", message));
        }
    }

    /// <summary>
    /// Anti-vacuity: the response really does carry the three names raw, so it is the path rule that
    /// keeps them out and not the field redaction.
    /// </summary>
    private static void AssertWouldOtherwiseBeVisible(Type middlewareType, string json)
    {
        Assert.Contains("\"payloadJson\":", json);
        Assert.Contains("\"ipAddress\":\"" + Ip + "\"", json);
        Assert.Contains("\"deviceLabel\":", json);
        Assert.False(WireSurface.IsRedacted("payloadJson"), $"payloadJson must not be a redaction token on {middlewareType.Name}");
        Assert.False(WireSurface.IsRedacted("ipAddress"));
        Assert.False(WireSurface.IsRedacted("deviceLabel"));
    }
}
