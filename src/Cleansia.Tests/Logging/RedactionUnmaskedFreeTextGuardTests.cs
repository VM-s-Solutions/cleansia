using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

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
/// Sibling guards in this idiom: <c>RateLimitCoverageGuardTests</c>,
/// <c>AnonymousAllowListExhaustivenessTests</c>, <c>FrozenPermissionMapTests</c>.
/// </summary>
public class RedactionUnmaskedFreeTextGuardTests
{
    /// <summary>
    /// The field names the hosts' <c>SensitiveFieldRegex</c> collapses. Read from the live regex rather
    /// than restated, so adding a token to the middleware automatically widens this guard.
    /// </summary>
    private static readonly IReadOnlyList<string> RedactionTokens = ReadRedactionTokens();

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
        "PaymentIntentId",    // Stripe machine id, e.g. "pi_3Abc…"
        // Opaque Stripe id, not narrative text — so it is out of scope for THIS guard. That these
        // responses hand a StripeCustomerId to the client at all is a separate S4 question about the
        // shipped DTO contract, not about redaction unmasking it.
        "StripeCustomerId",
    };

    /// <summary>
    /// Genuine free text behind a redaction token that this ticket did NOT expose and does not own.
    /// Each entry is a measured claim that the text was ALREADY inside the window before the
    /// redact-before-truncate change — so suppressing the route here would be silently absorbing
    /// someone else's finding. They belong to T-0457.
    ///
    /// This list is for **pre-existing** exposure only. A NEWLY unmasked field must be fixed, not
    /// added here — which is the whole point of the guard.
    /// </summary>
    private static readonly HashSet<string> AcceptedPreExisting = new(StringComparer.Ordinal)
    {
        // Behind `password`, whose value is SHORT — redacting it lengthens the body rather than
        // freeing window, so these were always visible. Pre-existing S6, T-0457.
        "CreateAdminUser.Command.FirstName",

        // Measured OLD=True by the security review: visible before the ordering change. T-0457.
        "GetMyDocuments.MyDocumentDto.Description",
        "GetMyDocuments.Response.Description",
    };

    [Fact]
    public void EveryDtoWhoseRedactedFieldUnmasksFreeText_HasItsRoutesSuppressed()
    {
        var failures = new List<string>();

        foreach (var (route, dtoTypes) in RoutesWithTheirWireTypes())
        {
            foreach (var dto in dtoTypes)
            {
                var unmasked = UnmaskedFreeTextMember(dto);
                if (unmasked is null)
                {
                    continue;
                }

                var key = $"{dto.DeclaringType?.Name}.{dto.Name}.{unmasked}";
                if (AcceptedPreExisting.Contains(key))
                {
                    continue;
                }

                if (!IsSensitivePathOnEveryHost(route))
                {
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
        var routes = RoutesWithTheirWireTypes();

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
        Assert.True(IsSensitivePathOnEveryHost(route), $"{route} must be in IsSensitivePath on all five hosts");

    // ── detection ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first free-text member that sits AFTER a redaction token in JSON order, or null.
    /// Nested DTOs are flattened in place, which is how the serializer emits them.
    /// </summary>
    private static string? UnmaskedFreeTextMember(Type dto)
    {
        var seenToken = false;

        foreach (var (name, type) in FlattenedMembers(dto, depth: 0))
        {
            if (RedactionTokens.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                seenToken = true;
                continue;
            }

            if (seenToken && type == typeof(string) && !StructuralMembers.Contains(name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// DTOs that are machine projections, never narrative. <c>Code(Type, Name, Value)</c> is the enum
    /// wire shape — flattening it would surface generic members like <c>Type</c> and <c>Name</c>, and
    /// allowlisting THOSE names globally would blind the guard to a real person's <c>Name</c>.
    /// </summary>
    private static readonly HashSet<Type> OpaqueDtos = [typeof(Cleansia.Core.AppServices.Shared.DTOs.Enums.Code)];

    private static IEnumerable<(string Name, Type Type)> FlattenedMembers(Type type, int depth)
    {
        if (depth > 4 || OpaqueDtos.Contains(type))
        {
            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // [JsonIgnore] members never reach the wire, so they can neither be redacted nor leaked.
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            var memberType = UnwrapCollection(property.PropertyType);

            if (IsAppServicesDto(memberType))
            {
                foreach (var nested in FlattenedMembers(memberType, depth + 1))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return (property.Name, Nullable.GetUnderlyingType(memberType) ?? memberType);
            }
        }
    }

    private static Type UnwrapCollection(Type type)
    {
        if (type == typeof(string))
        {
            return type;
        }

        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        // Take the element type off the IEnumerable<> INTERFACE, not off the type's own generic
        // arguments — for Dictionary<string, T> the latter yields the KEY type and mis-reports the
        // member as a string.
        var enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable is null ? type : enumerable.GetGenericArguments()[0];
    }

    private static bool IsAppServicesDto(Type type) =>
        type.Assembly == typeof(Cleansia.Core.AppServices.Common.Constants).Assembly &&
        !type.IsEnum &&
        type is { IsClass: true, IsAbstract: false } or { IsValueType: true, IsPrimitive: false, IsEnum: false };

    // ── the wire surface ────────────────────────────────────────────────────────────────────────────

    private sealed record RouteWireTypes(string Route, IReadOnlyList<Type> WireTypes);

    private static List<RouteWireTypes> RoutesWithTheirWireTypes()
    {
        var results = new List<RouteWireTypes>();

        foreach (var host in HostAssemblies())
        {
            foreach (var controller in host.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t)))
            {
                // A controller may carry several [Route] attributes (versioned surfaces do), so take
                // them all rather than GetCustomAttribute, which throws on the ambiguity.
                var routeAttributes = controller.GetCustomAttributes<RouteAttribute>().ToList();
                if (routeAttributes.Count == 0)
                {
                    continue;
                }

                var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal)
                    ? controller.Name[..^"Controller".Length]
                    : controller.Name;

                foreach (var routeAttribute in routeAttributes)
                {
                    var basePath = routeAttribute.Template.Replace("[controller]", controllerName);

                    foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        var verb = action.GetCustomAttributes()
                            .OfType<HttpMethodAttribute>()
                            .FirstOrDefault();
                        if (verb is null)
                        {
                            continue;
                        }

                        var route = "/" + basePath + (string.IsNullOrEmpty(verb.Template) ? "" : "/" + verb.Template);
                        route = route.Replace("//", "/");

                        var wireTypes = action.GetParameters().Select(p => p.ParameterType)
                            .Concat(action.GetCustomAttributes<ProducesResponseTypeAttribute>().Select(a => a.Type))
                            .Where(t => t is not null)
                            .SelectMany(t => Expand(UnwrapCollection(t!), 0))
                            .Where(IsAppServicesDto)
                            .Distinct()
                            .ToList();

                        results.Add(new RouteWireTypes(route, wireTypes));
                    }
                }
            }
        }

        return results;
    }

    /// <summary>The type plus every DTO reachable from it — an offending shape is usually nested.</summary>
    private static IEnumerable<Type> Expand(Type type, int depth)
    {
        if (depth > 4 || !IsAppServicesDto(type))
        {
            yield break;
        }

        yield return type;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var nested in Expand(UnwrapCollection(property.PropertyType), depth + 1))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<Assembly> HostAssemblies() =>
        RequestLoggingHarness.AllHostMiddleware.Select(t => t.Assembly).Distinct();

    private static bool IsSensitivePathOnEveryHost(string route) =>
        RequestLoggingHarness.AllHostMiddleware
            .All(middleware => (bool)middleware
                .GetMethod("IsSensitivePath", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [new Microsoft.AspNetCore.Http.PathString(route)])!);

    private static IReadOnlyList<string> ReadRedactionTokens()
    {
        var middleware = RequestLoggingHarness.AllHostMiddleware[0];
        var regex = (Regex)middleware
            .GetMethod("SensitiveFieldRegex", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        var alternation = Regex.Match(regex.ToString(), @"\""\(([^)]*)\)\""");
        Assert.True(alternation.Success, "could not read the token alternation out of SensitiveFieldRegex");

        return alternation.Groups[1].Value.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }
}
