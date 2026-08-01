using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Azure.Storage.Blobs;

namespace Cleansia.Tests.Logging;

/// <summary>
/// S6 — a signed URL in a log is a credential in a log. Every host's RequestLoggingMiddleware writes a
/// slice of the request and response bodies at Information.
///
/// Two independent defects are pinned here, and they fail for DIFFERENT reasons — keep both:
/// <list type="number">
/// <item><b>Ordering.</b> The middleware used to truncate BEFORE redacting. The redaction regex needs the
/// value's closing quote to match, so any secret straddling the cut was logged raw as a prefix. Fixed by
/// redact-then-truncate.</item>
/// <item><b>Regex membership.</b> <c>blobUrl</c> was absent from the redaction list, so order-photo and
/// dispute-evidence responses — where the value sits early enough to fit the window whole — wrote
/// COMPLETE signed URLs, signature included, to Information-level logs.</item>
/// </list>
///
/// Every fixture is a REAL DTO serialized with the hosts' own JSON configuration
/// (the hosts' own converters, via RequestLoggingHarness) carrying a REAL SAS from the real blob
/// client. A hand-trimmed body drifts from the wire shape, which is how the first version of this test
/// passed for the wrong reason.
/// </summary>
public class RequestLogSignedUrlRedactionTests
{
    private const string Redacted = "***REDACTED***";
    private const string BlobName = "0f6c9f2e-8f3a-4a2b-9a1f-2c3d4e5f6a7b";

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    /// <summary>
    /// Defect 1 — the profile response. The signed URL straddles the response cut, so truncate-first
    /// left <c>"blobUrl":"http…</c> in the log as a raw prefix.
    ///
    /// The assertion that CARRIES this test is the raw-prefix one. Asserting only "the signature is
    /// absent" would pass under BOTH orderings here, because at this limit the cut lands before
    /// <c>sig=</c> — that would be truncation doing the work, not the control under test.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task ProfileResponse_SignedUrl_IsRedacted_NotMerelyTruncatedAway(Type middlewareType)
    {
        var (json, signature) = RealProfileResponse();
        var responseLimit = RequestLoggingHarness.LimitOf(middlewareType, "ResponseBodyLimit");

        // Non-vacuity: this body really is longer than the window, and the signed value really does
        // straddle it — otherwise the two orderings are indistinguishable and the test proves nothing.
        var keyIndex = json.IndexOf("\"blobUrl\"", StringComparison.Ordinal);
        var valueEndIndex = json.IndexOf('"', keyIndex + "\"blobUrl\":\"".Length);
        Assert.True(json.Length > responseLimit, $"fixture must exceed the {responseLimit}-byte window");
        Assert.InRange(keyIndex, 0, responseLimit - 1);
        Assert.True(valueEndIndex > responseLimit, "the signed value must straddle the truncation cut");

        var logged = await RequestLoggingHarness.RunAsync(middlewareType, "/api/User/GetCurrent", responseJson: json);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain("\"blobUrl\":\"http", message));
        Assert.All(logged, message => Assert.DoesNotContain(signature, message));
        Assert.Contains(logged, message => message.Contains($"\"blobUrl\":\"{Redacted}\""));
    }

    /// <summary>
    /// Defect 2 — a response where the signed URL fits the window whole. This is the pre-existing leak
    /// the change closes: before <c>blobUrl</c> joined the redaction list a COMPLETE signed URL,
    /// signature included, was written at Information on every call. Ordering is irrelevant here;
    /// regex membership is what this pins.
    ///
    /// Driven through <c>POST /api/Dispute/UploadEvidence</c> — a REAL route that still carries a SAS
    /// and is deliberately NOT in <c>IsSensitivePath</c> (its response has no free text at all, so the
    /// redaction IS the whole control). The order-photo routes this used to exercise are now suppressed
    /// wholesale, which would have left it asserting redaction against a path no host serves.
    ///
    /// <c>DisputeDetails</c> was tried first and rejected on evidence: its <c>Evidence</c> array is 13th
    /// of 15 fields, so a realistic body puts the signature at byte 643 — outside the window, where
    /// truncation and not redaction would be doing the work. Shrinking the fixture to force it inside
    /// would be exactly the hand-trimmed-payload defect this suite exists to prevent.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task UnsuppressedResponse_CompleteSignedUrl_IncludingSignature_NeverReachesTheLog(Type middlewareType)
    {
        var (json, signature) = RealDisputeEvidenceResponse();
        var responseLimit = RequestLoggingHarness.LimitOf(middlewareType, "ResponseBodyLimit");

        // Non-vacuity: the signature genuinely sits inside the window, so only redaction can remove it.
        Assert.InRange(json.IndexOf(signature, StringComparison.Ordinal), 0, responseLimit - 1);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Dispute/UploadEvidence", responseJson: json, method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(signature, message));
        Assert.All(logged, message => Assert.DoesNotContain("\"blobUrl\":\"http", message));
        Assert.Contains(logged, message => message.Contains($"\"blobUrl\":\"{Redacted}\""));
    }

    /// <summary>
    /// Defect 1 on the REQUEST path — an avatar upload's base64 payload runs far past the request cut,
    /// so truncate-first logged a raw prefix of the image bytes.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task UploadRequest_Base64Payload_IsRedacted_NotMerelyTruncatedAway(Type middlewareType)
    {
        var json = RealAvatarUploadRequest(out var payloadHead);
        var requestLimit = RequestLoggingHarness.LimitOf(middlewareType, "RequestBodyLimit");

        Assert.True(json.Length > requestLimit, $"fixture must exceed the {requestLimit}-byte window");
        Assert.InRange(json.IndexOf(payloadHead, StringComparison.Ordinal), 0, requestLimit - 1);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType,
            "/api/User/UpdateCurrentUser",
            responseJson: "{}",
            requestJson: json,
            method: HttpMethods.Put);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(payloadHead, message));
        Assert.Contains(logged, message => message.Contains($"\"base64Content\":\"{Redacted}\""));
    }

    /// <summary>
    /// A Stripe **ephemeral key** (<c>ek_…</c>) is a short-lived credential granting a mobile client
    /// scoped access to a Stripe customer. It rode in the payment-intent response one field behind the
    /// already-redacted <c>clientSecret</c> and was written to Information-level logs verbatim.
    ///
    /// Found by <c>RedactionUnmaskedFreeTextGuardTests</c>, not by a human — the hand sweeps missed it
    /// three times because it does not read like free text, which is the argument for the guard.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task PaymentIntentResponse_EphemeralKey_IsRedacted(Type middlewareType)
    {
        const string ephemeralKey = "ek_test_YWNjdF90aGlzX2lzX2FfY3JlZGVudGlhbA";
        var json = $$"""{"clientSecret":"pi_3Abc_secret_xyz","ephemeralKey":"{{ephemeralKey}}","stripeCustomerId":"cus_Abc123"}""";

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Payment/CreatePaymentIntent", responseJson: json, method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(ephemeralKey, message));
        Assert.Contains(logged, message => message.Contains($"\"ephemeralKey\":\"{Redacted}\""));
    }

    /// <summary>
    /// The sibling credential in the same DTO. <c>CreateMembershipSubscription.Response</c> carries a
    /// <c>SetupIntentClientSecret</c> (<c>seti_…_secret_…</c>) — what the mobile PaymentSheet uses to
    /// confirm a card setup against a customer. The alternation is quote-anchored, so the
    /// <c>clientSecret</c> token never matched <c>"setupIntentClientSecret"</c> and the value was
    /// logged raw beside an already-redacted <c>ephemeralKey</c>.
    ///
    /// This is the class the guard CANNOT see: not a field unmasked by a redaction, but a secret whose
    /// name was simply never in the token list. Nothing detects that today — see the class note on
    /// <c>RedactionUnmaskedFreeTextGuardTests</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task MembershipSubscriptionResponse_SetupIntentClientSecret_IsRedacted(Type middlewareType)
    {
        const string setupIntentSecret = "seti_1PxyzLiveValue_secret_LIVEVALUE123456789";
        var json = $$"""{"membershipId":"m-1","setupIntentClientSecret":"{{setupIntentSecret}}","stripeCustomerId":"cus_Abc123","ephemeralKey":"ek_test_abc"}""";

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Membership/CreateSubscription", responseJson: json, method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(setupIntentSecret, message));
        Assert.Contains(logged, message => message.Contains($"\"setupIntentClientSecret\":\"{Redacted}\""));
    }

    // ── fixtures: real DTOs, real SAS, the hosts' own JSON configuration ─────────────────────────────

    private static Uri RealSasUri() =>
        new BlobContainerClient("UseDevelopmentStorage=true", Constants.BlobContainers.UserFiles)
            .GenerateSasUri(BlobName, TimeSpan.FromHours(1));

    private static string SignatureOf(Uri sas) =>
        sas.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts[0] == "sig")
            .Select(parts => parts[1])
            .Single();

    private static (string Json, string Signature) RealProfileResponse()
    {
        var sas = RealSasUri();

        var user = User.CreateWithPassword("jaroslava.novotna@example.cz", "Password1", "Jaroslava", "Novotna");
        user.Update("Jaroslava", "Novotna", "+420777123456", new DateOnly(1988, 4, 17));
        user.UpdateLanguagePreference("cs");
        user.UpdateProfilePhotoName(BlobName);

        var dto = user.MapToMyProfileDto(new CustomerProfileStats(12, 1250.50m, "CZK"), sas.ToString())!;

        return (JsonSerializer.Serialize(dto, RequestLoggingHarness.WireOptions), SignatureOf(sas));
    }

    private static (string Json, string Signature) RealDisputeEvidenceResponse()
    {
        var sas = RealSasUri();

        var dto = new UploadDisputeEvidence.Response(
            EvidenceId: "evidence-1",
            FileName: "receipt.jpg",
            BlobUrl: sas.ToString(),
            UploadedOn: new DateTimeOffset(2026, 7, 30, 9, 15, 0, TimeSpan.Zero));

        return (JsonSerializer.Serialize(dto, RequestLoggingHarness.WireOptions), SignatureOf(sas));
    }

    private static string RealAvatarUploadRequest(out string payloadHead)
    {
        // A real avatar arrives data-URI prefixed and runs far past the request window.
        var base64 = "data:image/png;base64," + new string('A', 4000);
        payloadHead = base64[..64];

        var command = new UpdateCurrentUser.Command(
            Id: string.Empty,
            FirstName: "Jaroslava",
            LastName: "Novotna",
            PhoneNumber: "+420777123456",
            BirthDate: new DateOnly(1988, 4, 17),
            Photo: new BlobFileDto("avatar.png", base64, "image/png"),
            LanguageCode: "cs");

        return JsonSerializer.Serialize(command, RequestLoggingHarness.WireOptions);
    }
}
