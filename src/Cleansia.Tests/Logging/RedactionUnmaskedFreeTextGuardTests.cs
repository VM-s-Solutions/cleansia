using System.Text.RegularExpressions;

namespace Cleansia.Tests.Logging;

/// <summary>
/// The mechanical guard for the "redaction unmasks the field behind it" class.
///
/// Redacting before truncating (T-0446 AC9) collapses a large payload to a 15-character sentinel, which
/// frees window and pulls whatever follows it into the logged slice. Four instances of that were found
/// by hand inside one ticket — <c>SaveMyDocuments</c>, <c>SaveOrderPhotos</c>, <c>UploadOrderPhoto</c>,
/// then <c>GetOrderPhotos</c> on the response path — and the catalog note written after the first three
/// still missed the fourth, because prose framed it as an upload concern. So this is a test, not a note.
///
/// It walks every wire DTO reachable from a controller action on the five hosts, flattens it into JSON
/// member order, and flags any type where a <b>redaction-regex token</b> is followed by a
/// <b>free-text</b> member. Every flagged type's routes must be in <c>IsSensitivePath</c> — because
/// once the token collapses, that free text is what lands in the log.
///
/// Failure message names the type, the member and the route, so the fix is obvious: add the route to
/// <c>IsSensitivePath</c>, or add the member to <see cref="StructuralMembers"/> if it is not free text.
///
/// <b>WHAT THIS GUARD DOES NOT COVER — read before trusting it.</b>
/// It detects <i>unmasking</i>: a field pulled into the window because the token in front of it
/// collapsed. It does NOT detect a secret whose field name is simply <i>absent from the token list</i>
/// — nothing does. <c>SetupIntentClientSecret</c> is the proof: the alternation is quote-anchored, so
/// the <c>clientSecret</c> token never matched it and the value logged raw. <c>ephemeralKey</c> was in
/// the same category and was found only by luck, because it happened to sit behind an already-redacted
/// field. A sibling guard (a name/shape heuristic over this same wire walk — <c>*Secret*</c>,
/// <c>*Token*</c>, <c>*Key*</c>, <c>*Password*</c>, and values shaped <c>sk_</c>/<c>ek_</c>/
/// <c>seti_</c>/<c>pi_</c>) is the follow-up that would close it.
///
/// Two bounded limits on its reach, both verified rather than assumed:
/// <list type="bullet">
/// <item>Response types are sourced from <c>[ProducesResponseType]</c>. <b>14 of 449 actions (3.1%)
/// declare none</b> and are therefore invisible to the walk — all GDPR/feature-flag deletes, none
/// returning a token-carrying DTO today.</item>
/// <item><c>DeclaredOnly</c> means an action moved onto a shared controller base class would be
/// missed. Refuted as live — the four base controllers declare zero actions — but latent.</item>
/// </list>
/// Member order is reflection order, which equals wire order here because <c>[JsonPropertyOrder]</c>
/// appears nowhere in the repository; if that ever changes, this flattening must change with it.
///
/// The route→DTO walk itself lives in <see cref="WireSurface"/>, shared with
/// <see cref="RequestLogPiiSurfaceGuardTests"/>.
///
/// Sibling guards in this idiom: <c>RateLimitCoverageGuardTests</c>,
/// <c>AnonymousAllowListExhaustivenessTests</c>, <c>FrozenPermissionMapTests</c>.
/// </summary>
public class RedactionUnmaskedFreeTextGuardTests
{
    /// <summary>
    /// The field names the hosts' <c>SensitiveFieldRegex</c> collapses. Read from the live regex rather
    /// than restated, so adding a token to the middleware automatically widens this guard.
    ///
    /// <para><b>Only the credential/payload family counts as "window-freeing".</b> Contact identity is
    /// redacted by a second regex and is deliberately NOT read here: those values are bounded at tens of
    /// bytes and the sentinel is 15, so collapsing one shifts the window by tens of bytes rather than
    /// hundreds — and for a short name it lengthens the body outright. Treating both families as
    /// window-freeing made every string member of every DTO report as unmasked, which is a guard that has
    /// stopped saying anything.</para>
    /// </summary>
    private static readonly IReadOnlyList<string> RedactionTokens = WireSurface.ReadRedactionTokens();

    /// <summary>
    /// String members that are NOT operator/user free text — file plumbing, ids, MIME types, URLs.
    /// A member here is judged incapable of carrying narrative PII. Keep this list short and reasoned:
    /// every entry is a claim that a human cannot type a customer's details into it.
    /// </summary>
    private static readonly HashSet<string> StructuralMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ContentType",        // MIME type, machine-set
        "FileName",           // client filename
        "OriginalFileName",   // client filename
        "FilePath",           // server-generated blob path
        "BlobUrl",            // itself a redaction token
        "Id",
        "PhotoId",
        "DocumentId",
        "EvidenceId",
        "OrderId",
        "UserId",
        "UploadedBy",
        "CapturedByEmployeeId",
        "CapturedByEmployeeName", // a cleaner's own first name, not customer narrative
        "DisplayOrderNumber",
        "SavingsCurrencyCode",
        "PreferredLanguageCode",
        "PreferredLanguageName",
        "LanguageCode",       // ISO code, e.g. "cs"
        // RegisterDevice.Validator admits only "android" or "ios"; nothing else reaches the handler.
        "Platform",
        "PaymentIntentId",    // Stripe machine id, e.g. "pi_3Abc…"
        // Opaque Stripe id, not narrative text — so it is out of scope for THIS guard. That these
        // responses hand a StripeCustomerId to the client at all is a separate S4 question about the
        // shipped DTO contract, not about redaction unmasking it.
        "StripeCustomerId",
    };

    /// <summary>
    /// Genuine free text behind a redaction token that the finding ticket did NOT expose and does not
    /// own. Each entry is a measured claim that the text was ALREADY inside the window before the
    /// redact-before-truncate change — so suppressing the route here would be silently absorbing someone
    /// else's finding.
    ///
    /// <para>This list is for <b>pre-existing</b> exposure only. A NEWLY unmasked field must be fixed,
    /// not added here — which is the whole point of the guard.</para>
    ///
    /// <para><b>Empty, and that is the desired state.</b> The seven entries it carried were the S6 debt
    /// T-0446 handed to T-0457, and both halves are now closed rather than accepted: the three
    /// <c>CreateAdminUser.Command</c> names are redacted by the contact-identity pass, and
    /// <c>GetMyDocuments</c>' description/review notes are suppressed wholesale by route. An entry
    /// nobody owns is a claim nobody will ever check, so keep this at zero.</para>
    /// </summary>
    private static readonly HashSet<string> AcceptedPreExisting = new(StringComparer.Ordinal);

    [Fact]
    public void EveryDtoWhoseRedactedFieldUnmasksFreeText_HasItsRoutesSuppressed()
    {
        var failures = new List<string>();

        foreach (var (route, dtoTypes) in WireSurface.RoutesWithTheirWireTypes())
        {
            foreach (var dto in dtoTypes)
            {
                if (WireSurface.IsSensitivePathOnEveryHost(route))
                {
                    continue;
                }

                foreach (var unmasked in UnmaskedFreeTextMembers(dto))
                {
                    // Accepting is PER MEMBER. Accepting the first offender used to absorb every later
                    // one in the same DTO silently — so a genuinely new field added behind an accepted
                    // one kept the guard green, which is the exact failure it exists to prevent.
                    if (AcceptedPreExisting.Contains($"{dto.DeclaringType?.Name}.{dto.Name}.{unmasked}"))
                    {
                        continue;
                    }

                    failures.Add(
                        $"{dto.DeclaringType?.Name}.{dto.Name}: a redaction token is followed by free-text " +
                        $"member '{unmasked}', and route '{route}' is NOT in IsSensitivePath. " +
                        "Redacting the token frees window and pulls that text into the log.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Distinct().OrderBy(f => f)));
    }

    /// <summary>Sanity: the guard is looking at a real corpus, not an empty one.</summary>
    [Fact]
    public void Guard_ActuallyInspectsTheWireSurface()
    {
        var routes = WireSurface.RoutesWithTheirWireTypes();

        Assert.InRange(routes.Count, 400, 1000);
        Assert.NotEmpty(RedactionTokens);
        Assert.Contains("base64Content", RedactionTokens);
        Assert.Contains("blobUrl", RedactionTokens);
        Assert.Contains(routes.SelectMany(r => r.WireTypes), t => t.Name.Contains("OrderPhotoDto"));
    }

    /// <summary>
    /// The four instances T-0446 found, pinned by name. If someone removes a route from
    /// <c>IsSensitivePath</c> the main guard catches it; this makes the regression legible.
    /// </summary>
    [Theory]
    [InlineData("/api/Employee/SaveMyDocuments")]
    [InlineData("/api/Order/SavePhotos")]
    [InlineData("/api/Order/UploadPhoto")]
    [InlineData("/api/Order/GetPhotos")]
    [InlineData("/api/AdminOrder/photos/{orderId}")]
    // Found by this guard, not by hand: "/auth/" never matched "/api/AdminAuth/...", so the admin
    // refresh/logout responses leaked the admin's Email once the leading JWT was redacted.
    [InlineData("/api/AdminAuth/RefreshToken")]
    [InlineData("/api/AdminAuth/Logout")]
    public void TheKnownUnmaskingRoutes_AreSuppressed(string route) =>
        Assert.True(WireSurface.IsSensitivePathOnEveryHost(route), $"{route} must be in IsSensitivePath on all five hosts");

    // ── detection ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns EVERY free-text member sitting after a redaction token in JSON order — not just the
    /// first. Returning only the first meant accepting one member silently accepted all the ones
    /// behind it, so the list stopped being the per-member measured claim its doc-comment promises.
    /// Nested DTOs are flattened in place, which is how the serializer emits them.
    /// </summary>
    private static IEnumerable<string> UnmaskedFreeTextMembers(Type dto)
    {
        var seenToken = false;

        foreach (var (name, type) in WireSurface.FlattenedMembers(dto, depth: 0))
        {
            if (RedactionTokens.Any(token => Regex.IsMatch(name, $"^(?:{token})$", RegexOptions.IgnoreCase)))
            {
                seenToken = true;
                continue;
            }

            // Redacted by the contact-identity pass, so it is not exposed free text — but it does not
            // free meaningful window either, so it must not arm `seenToken` for what follows it.
            if (WireSurface.IsRedacted(name))
            {
                continue;
            }

            if (seenToken && type == typeof(string) && !StructuralMembers.Contains(name))
            {
                yield return name;
            }
        }
    }
}
