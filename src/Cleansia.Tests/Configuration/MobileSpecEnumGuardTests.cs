using System.Text.Json;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// T-0370 AC1 spec-enum guard — the structural complement to the <c>[SwaggerEnumAsInt]</c> discipline.
/// The backend's <c>TolerantEnumConverterFactory</c> ALWAYS writes enums as integers on the wire, but
/// the mobile hosts' <c>EnumSchemaFilter</c> emits a STRING schema unless the enum carries the
/// attribute — a mismatch is a contract lie that fails the ENTIRE response decode on both generated
/// clients (the MembershipStatus bug). This guard pins the committed specs both platforms generate
/// from: every enum-carrying schema must be an integer enum. It catches BOTH a future mobile-DTO enum
/// missing the attribute (after the owner spec re-dump) and a bad re-dump regression.
/// Repo-file resolution mirrors <see cref="Cleansia.Tests.RateLimiting.PipelineOrderTests"/>.
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
            $"the {app} spec (scripts/refresh-mobile-spec.sh). String-typed enum schemas fail the whole " +
            "response decode in both generated clients. Offenders:\n  " + string.Join("\n  ", offenders));
    }
}
