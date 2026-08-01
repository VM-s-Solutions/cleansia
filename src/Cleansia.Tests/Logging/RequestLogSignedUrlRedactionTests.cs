using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;
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
    /// Defect 2 — the order-photo response, where the signed URL fits the window whole. This is the
    /// pre-existing leak the change closes: before <c>blobUrl</c> joined the redaction list a COMPLETE
    /// signed URL, signature included, was written at Information on every call. Ordering is irrelevant
    /// here; regex membership is what this pins.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task OrderPhotoResponse_CompleteSignedUrl_IncludingSignature_NeverReachesTheLog(Type middlewareType)
    {
        var (json, signature) = RealOrderPhotoResponse();
        var responseLimit = RequestLoggingHarness.LimitOf(middlewareType, "ResponseBodyLimit");

        // Non-vacuity: the signature genuinely sits inside the window, so only redaction can remove it.
        Assert.InRange(json.IndexOf(signature, StringComparison.Ordinal), 0, responseLimit - 1);

        var logged = await RequestLoggingHarness.RunAsync(middlewareType, "/api/OrderPhoto/GetByOrder", responseJson: json);

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

    private static (string Json, string Signature) RealOrderPhotoResponse()
    {
        var sas = RealSasUri();

        var dto = new GetOrderPhotos.Response(
            Photos:
            [
                new GetOrderPhotos.OrderPhotoDto(
                    Id: "photo-1",
                    PhotoType: PhotoType.Before,
                    BlobUrl: sas.ToString(),
                    FileName: "before-1.jpg",
                    OriginalFileName: "IMG_0042.jpg",
                    FileSizeBytes: 148_221,
                    ContentType: "image/jpeg",
                    CapturedAt: new DateTime(2026, 7, 30, 9, 15, 0, DateTimeKind.Utc),
                    CapturedByEmployeeId: null,
                    CapturedByEmployeeName: "Petr",
                    Width: 1920,
                    Height: 1080,
                    Notes: null)
            ],
            BeforePhotoCount: 1,
            AfterPhotoCount: 0);

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
