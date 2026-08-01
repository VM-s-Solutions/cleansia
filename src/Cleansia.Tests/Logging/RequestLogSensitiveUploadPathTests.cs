using System.Text.Json;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// S6 — the two upload endpoints carry operator-written FREE TEXT next to a base64 payload, and that
/// text is unreachable by a field-name denylist:
/// <c>SaveMyDocuments.DocumentToSave.Description</c> accompanies identity documents (ID cards,
/// criminal-record extracts, work permits), and <c>SaveOrderPhotos.PhotoToSave.Notes</c> is what a
/// cleaner writes about a customer's household.
///
/// Both were previously hidden by accident: a large <c>base64Content</c> early in each record consumed
/// the whole request window. Redacting BEFORE truncating (this ticket's AC9 fix) collapses that payload
/// to 17 characters, so everything after it now fits — the ordering fix bought a real leak. The
/// instrument is <c>IsSensitivePath</c>, the wholesale suppression already used for <c>/auth/</c>,
/// <c>/login</c> and <c>password</c>, NOT a wider regex.
/// </summary>
public class RequestLogSensitiveUploadPathTests
{
    private const string Suppressed = "[suppressed: sensitive endpoint]";
    private const string RedactedValue = "***REDACTED***";

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task SaveMyDocumentsRequest_DocumentDescription_IsSuppressed(Type middlewareType)
    {
        const string marker = "ID-card-no-990417-4821";
        var json = RealSaveMyDocumentsRequest(marker);

        AssertWouldOtherwiseBeVisible(middlewareType, json, marker);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Employee/SaveMyDocuments", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(marker, message));
        Assert.Contains(logged, message => message.Contains(Suppressed));
    }

    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task SaveOrderPhotosRequest_EveryPhotoNote_IsSuppressed(Type middlewareType)
    {
        var markers = Enumerable.Range(1, 4).Select(i => $"customer-household-note-{i}").ToArray();
        var json = RealSaveOrderPhotosRequest(markers);

        // Security measured 4/4 newly visible on a four-photo batch — assert all four, not just the first.
        foreach (var marker in markers)
        {
            AssertWouldOtherwiseBeVisible(middlewareType, json, marker);
        }

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/SavePhotos", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        foreach (var marker in markers)
        {
            Assert.All(logged, message => Assert.DoesNotContain(marker, message));
        }

        Assert.Contains(logged, message => message.Contains(Suppressed));
    }

    /// <summary>
    /// The third endpoint, and the one easiest to miss: <c>UploadOrderPhoto</c> posts a SINGLE photo to
    /// <c>/api/Order/UploadPhoto</c>, which the <c>/savephotos</c> entry does not match. Its
    /// <c>Notes</c> sits after a <c>byte[] FileData</c> that the regex collapses, so it has the same
    /// shape and the same exposure as the batch endpoint.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task UploadOrderPhotoRequest_PhotoNote_IsSuppressed(Type middlewareType)
    {
        const string marker = "customer-keeps-key-under-mat-dog-bites";
        var json = RealUploadOrderPhotoRequest(marker);

        AssertWouldOtherwiseBeVisible(middlewareType, json, marker);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType, "/api/Order/UploadPhoto", responseJson: "{}", requestJson: json,
            method: HttpMethods.Post);

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(marker, message));
        Assert.Contains(logged, message => message.Contains(Suppressed));
    }

    /// <summary>
    /// Non-vacuity. Suppression only proves something if the text would otherwise reach the log, so
    /// reproduce what the redaction step leaves behind — the base64 payload collapsed to its sentinel —
    /// and assert the marker then falls INSIDE the request window. Without this the test would pass on a
    /// fixture that truncation happened to hide anyway, which is the trap AC9 was written to close.
    /// </summary>
    private static void AssertWouldOtherwiseBeVisible(Type middlewareType, string json, string marker)
    {
        var requestLimit = RequestLoggingHarness.LimitOf(middlewareType, "RequestBodyLimit");
        var afterRedaction = System.Text.RegularExpressions.Regex.Replace(
            json, "\"(base64Content|fileData)\"\\s*:\\s*\"[^\"]*\"", $"\"payload\":\"{RedactedValue}\"");

        var markerIndex = afterRedaction.IndexOf(marker, StringComparison.Ordinal);
        Assert.InRange(markerIndex, 0, requestLimit - 1);

        // These fixtures must stay under the scan cap, or they would be suppressed for SIZE and the
        // path entry under test would never be exercised.
        Assert.InRange(json.Length, 0, RequestLoggingHarness.LimitOf(middlewareType, "RedactionScanLimit"));
    }

    // ── fixtures: the real command shapes, serialized the way the hosts emit them ────────────────────

    private static string RealSaveMyDocumentsRequest(string description)
    {
        var command = new SaveMyDocuments.Command
        {
            Documents =
            [
                new SaveMyDocuments.DocumentToSave
                {
                    DocumentType = DocumentType.Passport,
                    File = new BlobFileDto("passport.jpg", LargePayload(), "image/jpeg"),
                    Description = description
                }
            ]
        };

        return JsonSerializer.Serialize(command, RequestLoggingHarness.WireOptions);
    }

    private static string RealSaveOrderPhotosRequest(IReadOnlyList<string> notes)
    {
        var command = new SaveOrderPhotos.Command(
            OrderId: "01HZX9N6M7Q8R9S0T1V2W3X4Y5",
            Photos: notes.Select((note, i) => new SaveOrderPhotos.PhotoToSave(
                PhotoType: PhotoType.Before,
                File: new BlobFileDto($"before-{i + 1}.jpg", LargePayload(), "image/jpeg"),
                Notes: note)).ToList());

        return JsonSerializer.Serialize(command, RequestLoggingHarness.WireOptions);
    }

    private static string RealUploadOrderPhotoRequest(string notes)
    {
        var command = new UploadOrderPhoto.Command(
            OrderId: "01HZX9N6M7Q8R9S0T1V2W3X4Y5",
            PhotoType: PhotoType.After,
            FileName: "after-1.jpg",
            ContentType: "image/jpeg",
            FileData: new byte[3000],
            Notes: notes);

        return JsonSerializer.Serialize(command, RequestLoggingHarness.WireOptions);
    }

    private static string LargePayload() => "data:image/jpeg;base64," + new string('A', 4000);
}
