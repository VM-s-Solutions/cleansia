using System.Reflection;
using System.Text.Json;
using Cleansia.Infra.Common.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// T-0370 AC1 spec-enum guard — the structural complement to the <c>[SwaggerEnumAsInt]</c> discipline.
/// The backend's <c>TolerantEnumConverterFactory</c> ALWAYS writes enums as integers on the wire, but
/// every host's <c>EnumSchemaFilter</c> emits a STRING schema unless the enum carries the
/// attribute — a mismatch is a contract lie that fails the ENTIRE response decode on the mobile
/// generated clients (the MembershipStatus bug) and silently poisons the NSwag TypeScript enums
/// (the FiscalErrorKind / BillingInterval admin crashes). Two layers:
/// <list type="number">
///   <item>The committed mobile specs both mobile platforms generate from: every enum-carrying
///   schema must be an integer enum. Catches a bad re-dump regression.</item>
///   <item>A live reflection sweep of every host's controller wire surface (params +
///   [ProducesResponseType] shapes, walked transitively): every reachable Cleansia enum must carry
///   <c>[SwaggerEnumAsInt]</c>, so the lie is caught at build time, before any spec dump.</item>
/// </list>
/// Repo-file resolution mirrors <see cref="Cleansia.Tests.RateLimiting.PipelineOrderTests"/>;
/// host-assembly enumeration mirrors <see cref="Cleansia.Tests.Authentication.AnonymousAllowListExhaustivenessTests"/>.
/// </summary>
public class MobileSpecEnumGuardTests
{
    private static string SpecPath(string app)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cleansia.Api.sln")))
            dir = dir.Parent;
        Assert.True(dir is not null, "Could not locate src/ root.");
        var file = Path.Combine(dir!.FullName, "cleansia_android", "openapi", $"{app}-mobile-api.json");
        Assert.True(File.Exists(file), $"Committed mobile spec not found at {file}");
        return file;
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("partner")]
    public void Every_Enum_Schema_In_The_Committed_Mobile_Spec_Is_Integer_Typed(string app)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SpecPath(app)));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var offenders = new List<string>();
        var enumCount = 0;
        foreach (var schema in schemas.EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("enum", out _))
                continue;

            enumCount++;
            var type = schema.Value.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            if (type != "integer")
                offenders.Add($"{schema.Name} -> \"{type}\"");
        }

        Assert.True(enumCount > 0, $"No enum schemas found in the {app} spec — the guard would be vacuous.");
        Assert.True(offenders.Count == 0,
            $"T-0370 AC1: every enum on a mobile DTO is an int on the wire (TolerantEnumConverterFactory), " +
            $"so its schema must be an integer enum — add [SwaggerEnumAsInt] to the Domain enum and re-dump " +
            $"the {app} spec (src/cleansia_ios/scripts/refresh-mobile-spec.sh). String-typed enum schemas fail " +
            "the whole response decode in both generated clients. Offenders:\n  " + string.Join("\n  ", offenders));
    }

    public static TheoryData<Type> HostMarkers => new()
    {
        typeof(Cleansia.Web.Admin.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Partner.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Customer.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Mobile.Partner.Attributes.PermissionAttribute),
        typeof(Cleansia.Web.Mobile.Customer.Attributes.PermissionAttribute),
    };

    [Theory]
    [MemberData(nameof(HostMarkers))]
    public void Every_Enum_On_The_Host_Wire_Surface_Carries_SwaggerEnumAsInt(Type hostMarker)
    {
        var visited = new HashSet<Type>();
        var reachableEnums = new HashSet<Type>();
        foreach (var root in WireSurfaceRootsOf(hostMarker.Assembly))
            CollectReachableEnums(root, visited, reachableEnums);

        Assert.True(reachableEnums.Count > 0,
            $"No enums reachable from {hostMarker.Assembly.GetName().Name}'s controllers — the guard would be vacuous.");

        var offenders = reachableEnums
            .Where(e => e.GetCustomAttribute<SwaggerEnumAsIntAttribute>() is null)
            .Select(e => e.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Every enum is an int on the wire (TolerantEnumConverterFactory), but " +
            $"{hostMarker.Assembly.GetName().Name}'s EnumSchemaFilter emits a STRING schema unless the enum " +
            "carries [SwaggerEnumAsInt] — the generated clients then lie about the runtime shape " +
            "(admin FiscalErrorKind/BillingInterval crashes). Add [SwaggerEnumAsInt] to:\n  " +
            string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The types a host's OpenAPI spec is built from: action parameters (bodies, query models) and
    /// the [ProducesResponseType] response shapes. Actions return Task&lt;IActionResult&gt; in this
    /// codebase, so the response DTOs are only discoverable via the attribute.
    /// </summary>
    private static IEnumerable<Type> WireSurfaceRootsOf(Assembly hostAssembly)
    {
        var controllers = hostAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controller in controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var parameter in action.GetParameters())
            {
                if (parameter.ParameterType != typeof(CancellationToken))
                    yield return parameter.ParameterType;
            }

            foreach (var produces in action.GetCustomAttributes<ProducesResponseTypeAttribute>())
            {
                if (produces.Type != typeof(void))
                    yield return produces.Type;
            }
        }
    }

    private static void CollectReachableEnums(Type? type, HashSet<Type> visited, HashSet<Type> reachableEnums)
    {
        if (type is null)
            return;

        if (type.IsByRef || type.IsPointer)
            type = type.GetElementType()!;

        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type.IsArray)
        {
            CollectReachableEnums(type.GetElementType(), visited, reachableEnums);
            return;
        }

        if (!visited.Add(type))
            return;

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                CollectReachableEnums(argument, visited, reachableEnums);
        }

        if (type.Namespace?.StartsWith("Cleansia", StringComparison.Ordinal) != true)
            return;

        if (type.IsEnum)
        {
            reachableEnums.Add(type);
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            CollectReachableEnums(property.PropertyType, visited, reachableEnums);
    }
}
