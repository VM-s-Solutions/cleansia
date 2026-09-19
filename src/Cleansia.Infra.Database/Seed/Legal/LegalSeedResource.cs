using System.Globalization;
using System.Reflection;
using System.Text;
using Cleansia.Core.Domain.Legal;

namespace Cleansia.Infra.Database.Seed.Legal;

/// <summary>
/// One embedded seed file, <c>Seed/Legal/{audience}/{type}/{country-or-any}/{yyyy-MM-dd}/{lang}.md</c>,
/// with a <c>title:</c> front-matter line. The path is the document's identity and the file is the
/// text, so a new version is a new folder and nothing else.
/// </summary>
public sealed record LegalSeedResource(
    LegalDocumentAudience Audience,
    LegalDocumentType Type,
    string? CountryIsoCode,
    DateOnly EffectiveFrom,
    string Language,
    string Title,
    string ContentMarkdown)
{
    public const string Prefix = "Seed/Legal/";
    public const string AnyCountry = "any";

    private static readonly Dictionary<string, LegalDocumentAudience> Audiences = new(StringComparer.OrdinalIgnoreCase)
    {
        ["customer"] = LegalDocumentAudience.Customer,
        ["employee"] = LegalDocumentAudience.Employee,
    };

    private static readonly Dictionary<string, LegalDocumentType> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["terms-of-service"] = LegalDocumentType.TermsOfService,
        ["privacy-policy"] = LegalDocumentType.PrivacyPolicy,
    };

    public static IReadOnlyList<LegalSeedResource> ReadAll(Assembly? assembly = null)
    {
        assembly ??= typeof(LegalSeedResource).Assembly;
        var resources = new List<LegalSeedResource>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var logicalName = name.Replace('\\', '/');
            if (!logicalName.StartsWith(Prefix, StringComparison.Ordinal) || !logicalName.EndsWith(".md", StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            resources.Add(Parse(logicalName, reader.ReadToEnd()));
        }

        return resources;
    }

    public static LegalSeedResource Parse(string logicalName, string content)
    {
        var segments = logicalName.Replace('\\', '/')[Prefix.Length..].Split('/');
        if (segments.Length != 5
            || !Audiences.TryGetValue(segments[0], out var audience)
            || !Types.TryGetValue(segments[1], out var type)
            || !DateOnly.TryParseExact(segments[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var effectiveFrom)
            || !segments[4].EndsWith(".md", StringComparison.Ordinal)
            || segments[4].Length != 5)
        {
            throw new InvalidOperationException(
                $"Legal seed file '{logicalName}' is not at Seed/Legal/{{customer|employee}}/{{terms-of-service|privacy-policy}}/{{ISO3|any}}/{{yyyy-MM-dd}}/{{xx}}.md.");
        }

        var country = segments[2].Equals(AnyCountry, StringComparison.OrdinalIgnoreCase) ? null : segments[2].ToUpperInvariant();
        var language = segments[4][..2].ToLowerInvariant();
        var (title, body) = SplitFrontMatter(logicalName, content);

        return new LegalSeedResource(audience, type, country, effectiveFrom, language, title, body);
    }

    // Line endings are normalised so the stored text and its hash do not depend on which OS checked
    // the file out: the repository stores LF, a Windows checkout reads CRLF.
    private static (string Title, string Body) SplitFrontMatter(string logicalName, string content)
    {
        var normalised = content.Replace("\r\n", "\n").TrimStart('\uFEFF');
        const string fence = "---\n";
        if (!normalised.StartsWith(fence, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Legal seed file '{logicalName}' has no front-matter block.");
        }

        var close = normalised.IndexOf("\n---\n", fence.Length, StringComparison.Ordinal);
        if (close < 0)
        {
            throw new InvalidOperationException($"Legal seed file '{logicalName}' has an unterminated front-matter block.");
        }

        var frontMatter = normalised[fence.Length..close];
        var title = frontMatter.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["title:".Length..].Trim())
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException($"Legal seed file '{logicalName}' has no 'title:' in its front-matter.");
        }

        var body = normalised[(close + "\n---\n".Length)..].Trim();
        if (body.Length == 0)
        {
            throw new InvalidOperationException($"Legal seed file '{logicalName}' has no text after its front-matter.");
        }

        return (title, body);
    }
}
