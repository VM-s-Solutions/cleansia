using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.Devices;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// The guard for the class the other two wire-surface guards say out loud they cannot see:
/// <b>a secret whose field name was never in the redaction token list.</b>
///
/// <para><b>Why the list cannot guard itself.</b> <c>SensitiveFieldRegex</c> is a list of literal names,
/// so the only thing between a new secret-bearing DTO member and an Information-level body log is that
/// somebody thought to add its name. Both live Stripe credentials found in sprint 14 were in that class,
/// and <c>ephemeralKey</c> was found <i>by accident</i> — it happened to sit behind an already-redacted
/// field, so the unmasking guard surfaced it while looking for something else. This guard asks the
/// question deliberately: for every wire DTO on the five hosts, does any member <i>look like</i> a
/// credential, and is it covered?</para>
///
/// <para><b>The rule.</b> A string member whose name contains the standalone word <c>secret</c>,
/// <c>token</c>, <c>key</c> or <c>password</c> must be redacted by the live middleware regexes, or on a
/// route suppressed by <c>IsSensitivePath</c> on all five hosts, or on <see cref="StructurallyNotASecret"/>
/// with a written reason. Otherwise CI is red, naming the DTO, the member and the routes that emit it.</para>
///
/// <para><b>It was measured before it was written.</b> The first run saw <b>48</b> credential-shaped
/// members, of which <b>20</b> (six distinct names) were neither redacted nor suppressed. Nineteen were
/// false positives of five kinds, all reasoned below. The twentieth was real:
/// <c>RegisterDevice.Command.DeviceToken</c> — the raw FCM/APNs push token, logged verbatim on every
/// device registration because <c>token</c> is quote-anchored in the regex and so never matched
/// <c>deviceToken</c>. The same value is redacted when it is called <c>Token</c>
/// (<c>RegisterLiveActivityToken.Command</c>) and suppressed when it is called
/// <c>TrustedDeviceToken</c> (it rides <c>/Auth/Login</c>) — which is the arbitrariness of a name list,
/// stated as a measurement. It is fixed in the same change: <c>deviceToken</c> is now a token on all
/// five hosts.</para>
///
/// <para><b>What this guard cannot see, stated plainly.</b> It is a static walk over types, so it reads
/// <i>names and types</i> — never runtime values. A member holding a Stripe secret under a name with no
/// credential word in it (<c>Payload</c>, <c>Blob</c>, <c>Handle</c>) is invisible here, and no
/// name-shaped heuristic can reach it. The value-shaped leg the ticket proposed (flag a member whose
/// example/default is prefixed <c>sk_</c>/<c>ek_</c>/<c>seti_</c>) was <b>dropped after measurement, not
/// skipped</b>: the AppServices assembly carries no <c>[DefaultValue]</c>, no example attribute, no
/// generated XML documentation file and no credential-prefix literal, so there is nothing statically
/// discoverable for it to read and the leg would have asserted nothing. (<c>pi_</c> would not have
/// belonged in it regardless — a payment-intent id is a Stripe object id, not a secret, which is why
/// <c>PaymentIntentId</c> sits on the sibling guard's structural list.) Type filter is
/// <see cref="string"/> alone: <c>RefreshTokenExpiresAt</c> is credential-shaped by name and holds a
/// timestamp.</para>
///
/// <para>The route → DTO walk lives in <see cref="WireSurface"/>, shared with
/// <see cref="RedactionUnmaskedFreeTextGuardTests"/> (unmasking) and
/// <see cref="RequestLogPiiSurfaceGuardTests"/> (contact identity). Coverage is asked of
/// <c>WireSurface.IsRedacted</c>, not of the raw token list, because the PII family is matched by shape
/// and an equality comparison would report a shaped-covered member as unprotected.</para>
/// </summary>
public class RequestLogCredentialShapeGuardTests
{
    /// <summary>
    /// A member name is credential-shaped when one of its PascalCase words IS a credential word — not
    /// when it merely contains the letters. Substring matching would fire on <c>Keyword</c>,
    /// <c>Tokenizer</c> and <c>Monkey</c>, and a guard whose first run is mostly noise gets switched off,
    /// which is the failure mode that matters more here than a narrow miss.
    /// </summary>
    private static readonly Regex CredentialWord =
        new("^(secrets?|tokens?|keys?|passwords?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// PascalCase/camelCase word split. Digits are their own word so <c>Base64Content</c> splits to
    /// Base·64·Content rather than swallowing the run, and the uppercase-run alternative keeps
    /// <c>APIKey</c> as API·Key instead of one opaque word.
    /// </summary>
    private static readonly Regex Word =
        new("[A-Z]+(?![a-z])|[A-Z][a-z]*|[a-z]+|[0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Members whose name is credential-shaped but which structurally cannot hold a credential. Every
    /// entry is a claim a human can argue with, and each was checked against its producer rather than
    /// assumed from the name.
    ///
    /// <para><b>A newly-added member may NEVER be silenced by adding it here</b> — that is the whole
    /// point of the guard. The one real credential this guard found was fixed in the middleware, not
    /// excepted, which is why there is no second "accepted pre-existing" list to put it on.</para>
    /// </summary>
    private static readonly HashSet<string> StructurallyNotASecret = new(StringComparer.OrdinalIgnoreCase)
    {
        // The notification's loc-key ("order.completed"), from NotificationEventCatalog. Clients render
        // the text and derive the deep link from it, so it is the row's public type discriminator.
        "EventKey",

        // Which fiscal provider stamped the receipt. It is PRINTED on the customer's receipt PDF
        // (DefaultReceiptLayoutBuilder:104), which settles the question.
        "FiscalProviderKey",

        // A client-supplied replay-dedup nonce, hashed into the Stripe attempt id by
        // DeriveStripeAttemptId. Knowing it grants nothing without the caller's own authentication, and
        // it is the correlation handle a support engineer needs when a subscribe attempt is disputed.
        "IdempotencyToken",

        // The placeholder NAME in an email template's key/value pair — EmailTemplateKeyValueDto(Key,
        // Value). The variable, not the content.
        "Key",

        // An i18n translation key for a loyalty tier/perk label.
        "LabelKey",
    };

    [Fact]
    public void EveryCredentialShapedWireMember_IsRedactedOrItsRouteIsSuppressed()
    {
        var findings = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (route, dtoTypes) in WireSurface.RoutesWithTheirWireTypes())
        {
            if (WireSurface.IsSensitivePathOnEveryHost(route))
            {
                continue;
            }

            foreach (var dto in dtoTypes)
            {
                foreach (var member in UnprotectedCredentialMembers(dto))
                {
                    var key = $"{dto.DeclaringType?.Name}.{dto.Name}.{member}";
                    if (!findings.TryGetValue(key, out var routes))
                    {
                        findings[key] = routes = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    routes.Add(route);
                }
            }
        }

        Assert.True(findings.Count == 0,
            $"{findings.Count} wire member(s) whose name is credential-shaped reach an Information-level " +
            "log raw. Fix by adding the name to SensitiveFieldRegex on all five hosts, or by suppressing " +
            "the route in IsSensitivePath on all five hosts. Adding to StructurallyNotASecret is only for " +
            "a member that provably does not hold what its name says.\n  " +
            string.Join("\n  ", findings.Select(f => $"{f.Key}  ←  {string.Join(" , ", f.Value)}")));
    }

    /// <summary>
    /// Anti-vacuity. A reflection-driven guard that discovers nothing passes silently, so a guard that
    /// inspects nothing is a non-run rather than a pass. The floors are a route count, a credential-shaped
    /// member count, and — stronger than either — the demand that the walk still reaches the three Stripe
    /// DTOs whose members are the reason this guard exists.
    /// </summary>
    [Fact]
    public void Guard_ActuallySeesTheCredentialBearingDtos()
    {
        var routes = WireSurface.RoutesWithTheirWireTypes();
        var credentialShaped = routes
            .SelectMany(r => r.WireTypes)
            .Distinct()
            .SelectMany(t => WireSurface.FlattenedMembers(t, depth: 0))
            .Where(m => m.Type == typeof(string) && IsCredentialShaped(m.Name))
            .Select(m => m.Name)
            .ToList();

        Assert.InRange(routes.Count, 400, 1000);
        Assert.InRange(credentialShaped.Count, 20, 500);
        Assert.Contains("EphemeralKey", credentialShaped);
        Assert.Contains("SetupIntentClientSecret", credentialShaped);
        Assert.Contains("DeviceToken", credentialShaped);
    }

    /// <summary>
    /// The shape claim, and the false-positive boundary that is the real risk here. The left column is
    /// names that exist or plausibly will; the right column is the near-misses a substring heuristic
    /// would have fired on. A future edit that widens the word set back to a substring reddens here.
    /// </summary>
    [Theory]
    [InlineData("Password", true)]
    [InlineData("CurrentPassword", true)]
    [InlineData("Token", true)]
    [InlineData("DeviceToken", true)]
    [InlineData("RefreshToken", true)]
    [InlineData("ClientSecret", true)]
    [InlineData("SetupIntentClientSecret", true)]
    [InlineData("EphemeralKey", true)]
    [InlineData("ApiKey", true)]
    [InlineData("APIKey", true)]
    [InlineData("SASToken", true)]
    [InlineData("Keys", true)]
    // The near-misses. Every one of these is a word that merely CONTAINS a credential word.
    [InlineData("Keyword", false)]
    [InlineData("Tokenizer", false)]
    [InlineData("Monkey", false)]
    [InlineData("Turnkey", false)]
    [InlineData("PasswordlessLogin", false)]
    [InlineData("Base64Content", false)]
    [InlineData("Keychain", false)]
    public void TheCredentialShape_CoversTheseNamesAndStopsAtThose(string memberName, bool expected) =>
        Assert.Equal(expected, IsCredentialShaped(memberName));

    /// <summary>
    /// Five-host consistency. <c>WireSurface.ReadTokens</c> reads the regex off <b>one</b> host, so every
    /// guard built on it silently inherits that host's list — four-of-five redaction would read as full
    /// coverage. Options are compared too: dropping <c>IgnoreCase</c> on one host is a divergence the
    /// pattern text alone would not show.
    /// </summary>
    [Theory]
    [InlineData("SensitiveFieldRegex")]
    [InlineData("ContactIdentityFieldRegex")]
    public void TheRedactionRegex_IsIdenticalOnAllFiveHosts(string regexMember)
    {
        var reference = RegexOf(RequestLoggingHarness.AllHostMiddleware[0], regexMember);

        foreach (var middleware in RequestLoggingHarness.AllHostMiddleware.Skip(1))
        {
            var candidate = RegexOf(middleware, regexMember);

            Assert.True(reference == candidate,
                $"{regexMember} diverges on {middleware.FullName}. Every guard reading the token list " +
                $"reads it from {RequestLoggingHarness.AllHostMiddleware[0].FullName}, so a host that " +
                $"redacts less than the others would be invisible.\n  reference: {reference}\n  this host: {candidate}");
        }
    }

    /// <summary>
    /// The finding, pinned as behaviour on every host. The guard above proves the NAME is on the list;
    /// this proves the VALUE does not reach the log — and the gap between those two is exactly what
    /// T-0446 AC9 found, where every listed name was redacted 0% of the time because the middleware
    /// truncated before it redacted.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostMiddlewareTypes))]
    public async Task DeviceRegistration_PushToken_NeverReachesTheLog(Type middlewareType)
    {
        const string pushToken =
            "fMEQ4t2vSGa1nQ9pXKzYbT:APA91bH7xQ2mJ4vZc8LpR0dNwUeIyTgF3sKaObX9CmVhJlPqR2tZ" +
            "yW6nE1dGuHkA5sBcXfMoLpQrTvYzN8jD4wSgKeIbUmHtRxCyFvPnZaQlOjWdEkGhMsBt";

        var json = JsonSerializer.Serialize(
            new RegisterDevice.Command("D5F1A0C4-3B27-4E96-9A18-7C0E2B6F4D83", pushToken, "ios"),
            RequestLoggingHarness.WireOptions);

        // Non-vacuity: the token must close INSIDE the window, or truncation rather than redaction would
        // be what removes it and this test would pass against an unredacted middleware.
        var tokenCloseIndex = json.IndexOf(pushToken, StringComparison.Ordinal) + pushToken.Length;
        Assert.InRange(tokenCloseIndex, 0, RequestLoggingHarness.LimitOf(middlewareType, "RequestBodyLimit") - 1);

        var logged = await RequestLoggingHarness.RunAsync(
            middlewareType,
            "/api/Device/Register",
            responseJson: "{}",
            requestJson: json,
            method: HttpMethods.Post,
            authenticatedUserId: "01HZX9N6M7Q8R9S0T1V2W3X4Y5");

        Assert.NotEmpty(logged);
        Assert.All(logged, message => Assert.DoesNotContain(pushToken, message, StringComparison.Ordinal));
        Assert.Contains(logged, message => message.Contains("\"deviceToken\":\"***REDACTED***\"", StringComparison.Ordinal));
    }

    public static TheoryData<Type> HostMiddlewareTypes() => RequestLoggingHarness.HostMiddlewareTypes();

    private static string RegexOf(Type middleware, string regexMember)
    {
        var regex = (Regex)middleware
            .GetMethod(regexMember, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        return $"{regex.Options}|{regex}";
    }

    private static bool IsCredentialShaped(string memberName) =>
        Word.Matches(memberName).Any(w => CredentialWord.IsMatch(w.Value));

    private static IEnumerable<string> UnprotectedCredentialMembers(Type dto) =>
        WireSurface.FlattenedMembers(dto, depth: 0)
            .Where(m => m.Type == typeof(string)
                        && IsCredentialShaped(m.Name)
                        && !StructurallyNotASecret.Contains(m.Name)
                        && !WireSurface.IsRedacted(m.Name))
            .Select(m => m.Name);
}
