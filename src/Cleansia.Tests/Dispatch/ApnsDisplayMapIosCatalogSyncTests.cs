using System.Text.Json;
using System.Text.RegularExpressions;
using Cleansia.Infra.Clients.Fcm;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// <see cref="FcmMessageFactory.ApnsDisplayMap"/> is a server-side declaration that client push copy
/// EXISTS: a registered key makes the factory emit <c>push.&lt;key&gt;.title|body</c> as an APNs
/// loc-key, and iOS renders whatever the main bundle resolves — or, resolving nothing, the raw key
/// itself on a lock screen. This pins the map against both iOS app catalogs so the mistake fails on
/// the PR that makes it: the Swift-side <c>PushLocKeyCatalogTests</c> assert the same property, but a
/// backend author adding a map key never runs the iOS test targets.
///
/// <para>Both catalogs are required for every key, including the audience-specific ones: a push is
/// addressed to a USER's device tokens, not to an app, so a cleaner who also has the customer app
/// installed receives <c>payroll.invoice_paid</c> on both bundles.</para>
///
/// <para>Deliberately NOT extended to Android. Android's templates are named
/// <c>notification_preferred_offer_title</c>, not the event key, so asserting them from here would
/// make the server own a key-name transform it has no business owning — the map would stop being a
/// declaration and start being a naming convention for a client. Android's own template test covers
/// that side.</para>
///
/// <para>Cross-tree read mirrors <c>StartupSeedScriptSyncTests</c>: walk up to the <c>.sln</c>, then
/// name the resolved path in every failure so an iOS folder rename reads as a rename.</para>
/// </summary>
public class ApnsDisplayMapIosCatalogSyncTests
{
    private static readonly string[] PlatformLanguages = ["en", "cs", "sk", "uk", "ru"];

    private static readonly string[] IosAppCatalogs =
    [
        Path.Combine("cleansia_ios", "CleansiaPartner", "Resources", "Localizable.xcstrings"),
        Path.Combine("cleansia_ios", "CleansiaCustomer", "Resources", "Localizable.xcstrings"),
    ];

    private static readonly Regex PositionalSlot = new(@"%(\d+)\$", RegexOptions.Compiled);

    [Fact]
    public void Every_Registered_Event_Has_Translated_Title_And_Body_In_Both_iOS_Catalogs()
    {
        var violations = new List<string>();

        foreach (var (catalogPath, strings) in LoadIosCatalogs())
        {
            foreach (var eventKey in FcmMessageFactory.ApnsDisplayMap.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                foreach (var locKey in new[] { $"push.{eventKey}.title", $"push.{eventKey}.body" })
                {
                    if (!strings.TryGetValue(locKey, out var entry))
                    {
                        violations.Add($"{catalogPath}: '{locKey}' is missing — a lock screen would show that raw key.");
                        continue;
                    }

                    foreach (var language in PlatformLanguages)
                    {
                        var unit = ReadStringUnit(entry, language);
                        if (unit is null)
                        {
                            violations.Add($"{catalogPath}: '{locKey}' has no {language} stringUnit.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(unit.Value.Value))
                            violations.Add($"{catalogPath}: '{locKey}' is empty in {language}.");

                        if (unit.Value.State != "translated")
                            violations.Add($"{catalogPath}: '{locKey}' is '{unit.Value.State}' in {language}, not 'translated'.");
                    }
                }
            }
        }

        AssertNoViolations(
            violations,
            "FcmMessageFactory.ApnsDisplayMap registers events whose iOS push copy is missing, empty or untranslated. " +
            "Ship the loc-keys in both app catalogs BEFORE registering the event (ADR-0025 D2 client-first rule).");
    }

    [Fact]
    public void Every_Registered_Event_Body_Has_Exactly_The_Mapped_Positional_Slots()
    {
        var violations = new List<string>();

        foreach (var (catalogPath, strings) in LoadIosCatalogs())
        {
            foreach (var (eventKey, argNames) in FcmMessageFactory.ApnsDisplayMap.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                var locKey = $"push.{eventKey}.body";
                if (!strings.TryGetValue(locKey, out var entry))
                    continue;

                foreach (var language in PlatformLanguages)
                {
                    var value = ReadStringUnit(entry, language)?.Value;
                    if (string.IsNullOrEmpty(value))
                        continue;

                    var specifierCount = CountFormatSpecifiers(value);
                    var slots = PositionalSlot.Matches(value).Select(m => int.Parse(m.Groups[1].Value)).ToList();
                    var highestSlot = slots.Count == 0 ? 0 : slots.Max();

                    if (specifierCount != slots.Count)
                    {
                        violations.Add(
                            $"{catalogPath}: '{locKey}' [{language}] mixes non-positional format specifiers into \"{value}\" — " +
                            "APNs loc-args are ordered, so every slot must be positional (%1$@).");
                        continue;
                    }

                    if (highestSlot != argNames.Count)
                    {
                        violations.Add(
                            $"{catalogPath}: '{locKey}' [{language}] uses {highestSlot} positional slot(s) but the map sends " +
                            $"{argNames.Count} loc-arg(s) ({string.Join(", ", argNames)}): \"{value}\".");
                    }
                }
            }
        }

        AssertNoViolations(
            violations,
            "An iOS push body's positional slots must match the loc-args FcmMessageFactory.ApnsDisplayMap sends. " +
            "A surplus slot renders literally; a missing one silently drops the argument.");
    }

    /// <summary>
    /// The factory sets <c>LocArgs</c> but never <c>title-loc-args</c>, so any format specifier in a
    /// title reaches the lock screen verbatim.
    /// </summary>
    [Fact]
    public void No_Registered_Event_Title_Carries_A_Format_Specifier()
    {
        var violations = new List<string>();

        foreach (var (catalogPath, strings) in LoadIosCatalogs())
        {
            foreach (var eventKey in FcmMessageFactory.ApnsDisplayMap.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var locKey = $"push.{eventKey}.title";
                if (!strings.TryGetValue(locKey, out var entry))
                    continue;

                foreach (var language in PlatformLanguages)
                {
                    var value = ReadStringUnit(entry, language)?.Value;
                    if (!string.IsNullOrEmpty(value) && CountFormatSpecifiers(value) > 0)
                    {
                        violations.Add($"{catalogPath}: '{locKey}' [{language}] carries a format specifier: \"{value}\".");
                    }
                }
            }
        }

        AssertNoViolations(
            violations,
            "FcmMessageFactory sends no title-loc-args, so a specifier in a push title renders as itself.");
    }

    private static IEnumerable<(string CatalogPath, Dictionary<string, JsonElement> Strings)> LoadIosCatalogs()
    {
        var solutionDirectory = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDirectory is null, "Could not locate the solution directory from the test base directory.");

        foreach (var relativePath in IosAppCatalogs)
        {
            var catalogPath = Path.Combine(solutionDirectory!, relativePath);
            Assert.True(
                File.Exists(catalogPath),
                $"iOS string catalog not found: {catalogPath}. FcmMessageFactory.ApnsDisplayMap is pinned against " +
                "this file; if the iOS tree moved, update IosAppCatalogs in ApnsDisplayMapIosCatalogSyncTests.");

            using var document = JsonDocument.Parse(File.ReadAllBytes(catalogPath));
            var strings = document.RootElement.GetProperty("strings")
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone());

            yield return (Path.GetRelativePath(solutionDirectory!, catalogPath), strings);
        }
    }

    private static (string? State, string? Value)? ReadStringUnit(JsonElement entry, string language)
    {
        if (!entry.TryGetProperty("localizations", out var localizations)
            || !localizations.TryGetProperty(language, out var localization)
            || !localization.TryGetProperty("stringUnit", out var stringUnit))
        {
            return null;
        }

        return (
            stringUnit.TryGetProperty("state", out var state) ? state.GetString() : null,
            stringUnit.TryGetProperty("value", out var value) ? value.GetString() : null);
    }

    private static int CountFormatSpecifiers(string value)
    {
        var count = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%')
                continue;

            if (i + 1 < value.Length && value[i + 1] == '%')
            {
                i++;
                continue;
            }

            count++;
        }

        return count;
    }

    private static void AssertNoViolations(List<string> violations, string headline)
    {
        Assert.True(
            violations.Count == 0,
            headline + "\n  " + string.Join("\n  ", violations));
    }

    // Mirrors DatabaseMigrationExtensions.FindSolutionDirectory — walk up until a *.sln is found.
    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }
}
