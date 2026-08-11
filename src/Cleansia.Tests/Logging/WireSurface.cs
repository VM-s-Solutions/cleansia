using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Cleansia.Tests.Logging;

/// <summary>
/// The route → wire-DTO walk the logging guards share: every DTO reachable from a controller action on
/// the five hosts, flattened into JSON member order, plus the live redaction token list and the
/// five-host <c>IsSensitivePath</c> check.
///
/// <para>Extracted from <see cref="RedactionUnmaskedFreeTextGuardTests"/> when the second guard
/// (<see cref="RequestLogPiiSurfaceGuardTests"/>) needed the same corpus. A guard carrying its own copy
/// of the walk drifts from its sibling silently, and the two would then disagree about what "the wire
/// surface" is — which is the whole thing these guards exist to be authoritative about.</para>
///
/// <para><b>Two bounded limits on its reach, both verified rather than assumed.</b> Response types come
/// from <c>[ProducesResponseType]</c>, so an action declaring none is invisible here; and
/// <c>DeclaredOnly</c> means an action moved onto a shared controller base class would be missed
/// (refuted as live — the four base controllers declare zero actions — but latent). Member order is
/// reflection order, which equals wire order here because <c>[JsonPropertyOrder]</c> appears nowhere in
/// the repository; if that ever changes, the flattening must change with it.</para>
/// </summary>
internal static class WireSurface
{
    internal sealed record RouteWireTypes(string Route, IReadOnlyList<Type> WireTypes);

    /// <summary>
    /// DTOs that are machine projections, never narrative. <c>Code(Type, Name, Value)</c> is the enum
    /// wire shape — flattening it would surface generic members like <c>Type</c> and <c>Name</c>, and
    /// allowlisting THOSE names globally would blind the guards to a real person's <c>Name</c>.
    /// </summary>
    private static readonly HashSet<Type> OpaqueDtos = [typeof(Cleansia.Core.AppServices.Shared.DTOs.Enums.Code)];

    public static List<RouteWireTypes> RoutesWithTheirWireTypes()
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
    public static IEnumerable<Type> Expand(Type type, int depth)
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

    /// <summary>Members in JSON order, with nested DTOs flattened in place — how the serializer emits them.</summary>
    public static IEnumerable<(string Name, Type Type)> FlattenedMembers(Type type, int depth)
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

    public static Type UnwrapCollection(Type type)
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

    public static bool IsAppServicesDto(Type type) =>
        type.Assembly == typeof(Cleansia.Core.AppServices.Common.Constants).Assembly &&
        !type.IsEnum &&
        type is { IsClass: true, IsAbstract: false } or { IsValueType: true, IsPrimitive: false, IsEnum: false };

    public static IEnumerable<Assembly> HostAssemblies() =>
        RequestLoggingHarness.AllHostMiddleware.Select(t => t.Assembly).Distinct();

    public static bool IsSensitivePathOnEveryHost(string route) =>
        RequestLoggingHarness.AllHostMiddleware
            .All(middleware => (bool)middleware
                .GetMethod("IsSensitivePath", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [new PathString(route)])!);

    /// <summary>
    /// The credential/payload tokens — the family whose values are UNBOUNDED (a base64 image, a signed
    /// URL, a JWT), so collapsing one to the sentinel frees window and can unmask what follows it.
    /// Parsed out of the LIVE compiled regex rather than restated, so a token added to the middleware
    /// automatically widens every guard that reads this and no guard can hold a stale copy of the list.
    /// </summary>
    public static IReadOnlyList<string> ReadRedactionTokens() => ReadTokens("SensitiveFieldRegex");

    /// <summary>
    /// The contact-identity tokens — bounded values (an email, a phone, a name, a date), matched by
    /// shape. Deliberately a SEPARATE regex from <see cref="ReadRedactionTokens"/>: they redact the same
    /// way but they do not free window the same way, and merging them would make every string member of
    /// every DTO read as "unmasked" to the sibling guard.
    /// </summary>
    public static IReadOnlyList<string> ReadContactIdentityTokens() => ReadTokens("ContactIdentityFieldRegex");

    /// <summary>
    /// Whether the middleware would redact a member of this name, in EITHER family.
    ///
    /// <para>A token is a regex FRAGMENT, not a literal — contact identity is shaped
    /// (<c>[A-Za-z]*email</c>) — and a guard comparing names by equality would report those members as
    /// unprotected while the middleware was in fact redacting them. The anchors here reproduce the
    /// production regex's own quote anchors, so the guard's answer is the middleware's answer; a literal
    /// token like <c>password</c> is a regex that matches itself and behaves exactly as before.</para>
    /// </summary>
    public static bool IsRedacted(string memberName) =>
        ReadRedactionTokens().Concat(ReadContactIdentityTokens())
            .Any(token => Regex.IsMatch(memberName, $"^(?:{token})$", RegexOptions.IgnoreCase));

    private static IReadOnlyList<string> ReadTokens(string regexMember)
    {
        var middleware = RequestLoggingHarness.AllHostMiddleware[0];
        var regex = (Regex)middleware
            .GetMethod(regexMember, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

        var alternation = Regex.Match(regex.ToString(), @"\""\((.*)\)\""\\s\*:");
        Assert.True(alternation.Success, $"could not read the token alternation out of {regexMember}");

        return alternation.Groups[1].Value.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }
}
