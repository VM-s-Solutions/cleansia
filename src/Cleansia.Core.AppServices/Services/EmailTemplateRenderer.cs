using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Cleansia.Core.AppServices.Services.Interfaces;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Renders one of the repository's own e-mail templates.
/// </summary>
/// <remarks>
/// The HTML in <c>email-templates/</c> used to be documentation: nothing in the
/// solution read it, and the copy that actually reached a customer was whatever
/// had been pasted into SendGrid against a template id. The two could drift and
/// nothing caught it.
///
/// The templates are linked into this assembly as embedded resources, so the
/// folder in the repository <b>is</b> the runtime source — there is one copy, it
/// is reviewed in a PR like everything else, and sending no longer depends on a
/// hosted template existing in someone's SendGrid account.
///
/// Substitution is deliberately literal <c>{{Key}}</c> replacement rather than a
/// templating engine. The set uses no conditionals or loops — the per-language
/// copy arrives already resolved from <c>EmailTemplateTranslation</c> — and a
/// dependency that parses untrusted-looking syntax around customer data would be
/// paying for expressiveness nothing uses.
/// </remarks>
public sealed partial class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string ResourcePrefix = "Cleansia.Core.AppServices.EmailTemplates.";

    private static readonly ConcurrentDictionary<string, string> Cache = new();
    private static readonly Assembly OwningAssembly = typeof(EmailTemplateRenderer).Assembly;

    /// <inheritdoc />
    public string Render(string templateName, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(values);

        var template = Cache.GetOrAdd(templateName, Load);
        var builder = new StringBuilder(template);

        foreach (var (key, value) in values)
        {
            builder.Replace("{{" + key + "}}", value ?? string.Empty);
        }

        // Anything still in braces had no value at all — a translation row that is
        // missing for one locale, most likely. Drop it. The alternative is a
        // customer reading "{{Greeting}}", and an empty line is the lesser failure.
        return Placeholder().Replace(builder.ToString(), string.Empty);
    }

    [GeneratedRegex(@"\{\{[A-Za-z0-9_]+\}\}")]
    private static partial Regex Placeholder();

    private static string Load(string templateName)
    {
        var resourceName = ResourcePrefix + templateName;
        using var stream = OwningAssembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            // Fail loudly at the first send rather than mailing a page of braces.
            var available = string.Join(
                ", ",
                OwningAssembly.GetManifestResourceNames()
                    .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                    .Select(n => n[ResourcePrefix.Length..]));

            throw new InvalidOperationException(
                $"E-mail template '{templateName}' is not embedded in this assembly. Available: {available}");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
