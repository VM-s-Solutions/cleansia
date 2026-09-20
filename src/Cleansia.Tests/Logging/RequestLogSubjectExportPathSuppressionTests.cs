using System.Text.Json;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Logging;

/// <summary>
/// ADR-0062 D5-export / S6 — the subject export now carries the customer's own audit trail
/// (<c>customerActions</c>: the payload, the IP address, the device label) beside the profile block that
/// was already there, and the consent section carries the user agent each grant was made from. None of
/// those names is in either redaction list and the device label and user agent are client-controlled
/// text no denylist reaches, so the two export routes stay suppressed wholesale — the
/// <c>gdpr/</c> rule, on every host — and this pins that the rule still holds for the POST verb the
/// routes moved to when the export became a Command. The body is a real <see cref="GdprExportDto"/>
/// through the hosts' own serializer options, so a renamed member cannot make this pass by accident.
/// </summary>
public class RequestLogSubjectExportPathSuppressionTests
{
    private const string Suppressed = "[suppressed: sensitive endpoint]";
    private const string Ip = "203.0.113.9";
    private const string Device = "iPhone 15 / iOS 17.4 <img src=x onerror=alert(1)>";
    private const string Payload = "{\"feeRate\":0.5,\"hasBeenAccepted\":true}";
    private const string Email = "jane.doe@example.test";
    // Digits only: the web encoder writes "+" as \u002B, so a literal "+420…" would not be found raw.
    private const string Phone = "420123456789";

    [Theory]
    [InlineData("/api/v1/AdminGdpr/export/user-1")]
    [InlineData("/api/v1/Gdpr/export")]
    public async Task ThePostedExport_ItsTrailAndItsProfile_NeverReachTheLogOnAnyHost(string route)
    {
        var json = JsonSerializer.Serialize(Export(), RequestLoggingHarness.WireOptions);
        AssertWouldOtherwiseBeVisible(json);

        foreach (var middlewareType in RequestLoggingHarness.AllHostMiddleware)
        {
            var logged = await RequestLoggingHarness.RunAsync(
                middlewareType, route, responseJson: json, method: "POST", authenticatedUserId: "admin-1");

            Assert.NotEmpty(logged);
            Assert.All(logged, message =>
            {
                Assert.DoesNotContain("payloadJson", message);
                Assert.DoesNotContain("ipAddress", message);
                Assert.DoesNotContain("deviceLabel", message);
                Assert.DoesNotContain("email", message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("phone", message, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(Ip, message);
                Assert.DoesNotContain(Device, message);
                Assert.DoesNotContain(Email, message);
                Assert.DoesNotContain(Phone, message);
            });
            Assert.Contains(logged, message => message.Contains(Suppressed));
        }
    }

    private static GdprExportDto Export() =>
        new(
            new GdprExportProfileDto("user-1", "Jane", "Doe", Email, Phone, null, "en", DateTimeOffset.UtcNow),
            Address: null,
            Employee: null,
            PayoutDetails: null,
            Orders: [],
            Disputes: [],
            Documents: [],
            Invoices: [],
            Consents: [new GdprExportConsentDto("c-1", ConsentType.TermsOfService, true, DateTimeOffset.UtcNow, null, Ip, Device, "2026-09-14", "legal-1")],
            CustomerActions:
            [
                new GdprExportCustomerActionDto("customer.order.cancel", DateTimeOffset.UtcNow, "Order", "order-1",
                    true, null, Payload, Ip, Device),
            ],
            new GdprExportMetadataDto(DateTimeOffset.UtcNow, "admin:admin@cleansia.test", "JSON"), []);

    /// <summary>
    /// Anti-vacuity: the response really does carry the names and values raw, so it is the path rule
    /// that keeps them out and not the field redaction or the body window.
    /// </summary>
    private static void AssertWouldOtherwiseBeVisible(string json)
    {
        Assert.Contains("\"payloadJson\":", json);
        Assert.Contains("\"ipAddress\":\"" + Ip + "\"", json);
        Assert.Contains("\"deviceLabel\":", json);
        Assert.Contains("\"userAgent\":", json);
        Assert.Contains("\"email\":\"" + Email + "\"", json);
        Assert.Contains("\"phoneNumber\":\"" + Phone + "\"", json);
        Assert.False(WireSurface.IsRedacted("payloadJson"));
        Assert.False(WireSurface.IsRedacted("ipAddress"));
        Assert.False(WireSurface.IsRedacted("deviceLabel"));
        Assert.False(WireSurface.IsRedacted("userAgent"));
    }
}
