using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// S6 — the caller's own contact identity must not reach an Information-level log.
///
/// <para><c>GET /api/User/GetCurrent</c> is the most-called authenticated route on the platform (every
/// app calls it on launch, on resume and after every profile save) and its response is a
/// <c>MyProfileDto</c> whose <c>email</c>/<c>firstName</c>/<c>lastName</c>/<c>phoneNumber</c>/
/// <c>birthDate</c> block closes well inside the 500-byte response window. The five hosts each wrote
/// that block verbatim, on every request.</para>
///
/// <para><b>The endpoint is not the defect — the sink is.</b> The leak is
/// <c>RequestLoggingMiddleware.SafeBody</c>, which is generic over every route, so a per-route
/// suppression entry would have fixed one URL and left the class open (exactly how <c>/adminauth/</c>
/// and <c>/admingdpr/</c> were each missed in turn). The fix is therefore field-name redaction, which
/// applies to every DTO on every host that uses these member names, and
/// <see cref="RequestLogPiiSurfaceGuardTests"/> is what keeps that name list from going stale.</para>
///
/// <para>Fixtures are REAL <c>MyProfileDto</c>s built through the production mapper and serialized with
/// the hosts' own JSON configuration, at three representative name lengths. A fixture short enough for
/// truncation to hide the PII is the failure mode that produced T-0446 AC9, so each case asserts
/// non-vacuity first: the body exceeds the window AND the last PII value closes inside it.</para>
/// </summary>
public class RequestLogProfilePiiRedactionTests
{
    private const string Redacted = "***REDACTED***";
    private const string BlobName = "0f6c9f2e-8f3a-4a2b-9a1f-2c3d4e5f6a7b";
    private const string CallerUserId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    /// <summary>
    /// AC2/AC3 — the response body of the profile read, on each of the five hosts, at three name
    /// lengths. Every one of the five values must be absent from every emitted message.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task ProfileResponse_ContactIdentity_NeverReachesTheLog(Type middlewareType)
    {
        var responseLimit = RequestLoggingHarness.LimitOf(middlewareType, "ResponseBodyLimit");

        foreach (var profile in RepresentativeProfiles())
        {
            var json = profile.Json;

            // Non-vacuity, per case: this really is a realistic body that overflows the window, and the
            // PII block really does close inside it — so truncation cannot be what removes the values.
            // The band is loose because the fixture carries a real SAS, whose signature is base64 and
            // therefore URL-encodes to a slightly different length on every mint.
            Assert.InRange(json.Length, 700, 1000);
            Assert.True(json.Length > responseLimit, $"{profile.Label}: fixture must exceed the {responseLimit}-byte window");
            Assert.InRange(LastPiiCloseIndex(json, profile), 0, responseLimit - 1);

            var logged = await RequestLoggingHarness.RunAsync(
                middlewareType,
                "/api/User/GetCurrent",
                responseJson: json,
                authenticatedUserId: CallerUserId);

            Assert.NotEmpty(logged);
            foreach (var value in profile.SensitiveValues)
            {
                Assert.All(logged, message =>
                    Assert.DoesNotContain(value, message, StringComparison.Ordinal));
            }
        }
    }

    /// <summary>
    /// AC4 — de-identify the log, do not blind it. <c>userId</c> is explicitly permitted by S6 and is the
    /// only handle an operator has once the values are gone.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task ProfileResponse_StillCarriesTheCallerUserId(Type middlewareType)
    {
        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType,
            "/api/User/GetCurrent",
            responseJson: RepresentativeProfiles()[1].Json,
            authenticatedUserId: CallerUserId);

        Assert.Contains(logged, message => message.Contains($"User: {CallerUserId}", StringComparison.Ordinal));
    }

    /// <summary>
    /// The sibling profile routes. The partner and admin APIs serve the same DTO from their own
    /// controllers, so a fix that held only for the customer host would be four-fifths of a fix.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task ProfileUpdate_RequestBody_ContactIdentity_NeverReachesTheLog(Type middlewareType)
    {
        var (json, values) = RealProfileUpdateRequest();

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType,
            "/api/User/UpdateCurrentUser",
            responseJson: "{}",
            requestJson: json,
            method: HttpMethods.Put,
            authenticatedUserId: CallerUserId);

        Assert.NotEmpty(logged);
        foreach (var value in values)
        {
            Assert.All(logged, message => Assert.DoesNotContain(value, message, StringComparison.Ordinal));
        }

        Assert.Contains(logged, message => message.Contains($"\"firstName\":\"{Redacted}\"", StringComparison.Ordinal));
    }

    // ── fixtures: real DTOs through the real mapper, the hosts' own JSON configuration ───────────────

    private sealed record ProfileFixture(string Label, string Json, IReadOnlyList<string> SensitiveValues);

    /// <summary>
    /// The three shapes the PM measured the exposure across: a short email and name, a typical Czech
    /// name, and a long Ukrainian one. All three put the PII block inside the window.
    /// </summary>
    private static IReadOnlyList<ProfileFixture> RepresentativeProfiles() =>
    [
        BuildProfile("short", "jk@x.cz", "Jana", "Krcm", "+420601002003", new DateOnly(1994, 2, 3)),
        BuildProfile("typical", "jaroslava.novotna@example.cz", "Jaroslava", "Novotna", "+420777123456", new DateOnly(1988, 4, 17)),
        BuildProfile("long", "oleksandra.kharchenko@example.ua", "Oleksandra", "Kharchenko-Vasylenko", "+380671234567", new DateOnly(1979, 11, 30)),
    ];

    private static ProfileFixture BuildProfile(
        string label, string email, string firstName, string lastName, string phone, DateOnly birthDate)
    {
        var sas = new BlobContainerClient("UseDevelopmentStorage=true", Constants.BlobContainers.UserFiles)
            .GenerateSasUri(BlobName, TimeSpan.FromHours(1));

        var user = User.CreateWithPassword(email, "Password1", firstName, lastName);
        user.Update(firstName, lastName, phone, birthDate);
        user.UpdateLanguagePreference("cs");
        user.UpdateProfilePhotoName(BlobName);

        var dto = user.MapToMyProfileDto(new CustomerProfileStats(12, 1250.50m, "CZK"), sas.ToString())!;

        return new ProfileFixture(
            label,
            JsonSerializer.Serialize(dto, RequestLoggingHarness.WireOptions),
            [email, firstName, lastName, phone, birthDate.ToString("yyyy-MM-dd")]);
    }

    private static (string Json, IReadOnlyList<string> Values) RealProfileUpdateRequest()
    {
        const string firstName = "Jaroslava";
        const string lastName = "Novotna";
        const string phone = "+420777123456";
        var birthDate = new DateOnly(1988, 4, 17);

        var command = new UpdateCurrentUser.Command(
            Id: string.Empty,
            FirstName: firstName,
            LastName: lastName,
            PhoneNumber: phone,
            BirthDate: birthDate,
            Photo: new BlobFileDto("avatar.png", null, "image/png"),
            LanguageCode: "cs");

        return (
            JsonSerializer.Serialize(command, RequestLoggingHarness.WireOptions),
            [firstName, lastName, phone, birthDate.ToString("yyyy-MM-dd")]);
    }

    /// <summary>Index of the closing quote of the last of the five PII values in wire order.</summary>
    private static int LastPiiCloseIndex(string json, ProfileFixture profile)
    {
        var last = profile.SensitiveValues
            .Select(value => json.IndexOf(value, StringComparison.Ordinal))
            .Max();

        Assert.True(last >= 0, $"{profile.Label}: every PII value must be present in the fixture");
        return last;
    }
}
