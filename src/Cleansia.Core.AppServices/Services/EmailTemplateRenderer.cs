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
/// Substitution is deliberately literal <c>{{Key}}</c> replacement, in a single pass
/// over the template, rather than a templating engine. The set uses no conditionals
/// or loops — the per-language copy arrives already resolved from
/// <c>EmailTemplateTranslation</c> — and a dependency that parses untrusted-looking
/// syntax around customer data would be paying for expressiveness nothing uses.
///
/// Every value is HTML-encoded, and none is markup: the copy (in-code defaults and
/// the admin's plain-text translation rows) is text, the names and numbers are
/// text, and the links built in code are URLs, which sit in double-quoted
/// <c>href</c> attributes where <c>&amp;amp;</c> reads back as <c>&amp;</c>. Markup an
/// e-mail needs belongs in its template. A value only ever lands in element text
/// or a double-quoted attribute (EmailTemplateRendererTests pins that), and
/// <c>&amp; &lt; &gt; "</c> are the four characters that can break out of either.
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

        // One walk over the template, never over a value: a name typed as "{{SupportEmail}}"
        // prints as typed instead of being filled by a later key. A placeholder with no value
        // renders empty — a translation row missing for one locale, most likely — because a
        // customer reading "{{Greeting}}" is the worse failure.
        return Placeholder().Replace(template, match => HtmlEncode(values.GetValueOrDefault(match.Groups[1].Value)));
    }

    [GeneratedRegex(@"\{\{([A-Za-z0-9_]+)\}\}")]
    private static partial Regex Placeholder();

    // Not WebUtility.HtmlEncode: it also rewrites every Latin-1 letter ("á" -> "&#225;"),
    // which changes the copy's bytes for nothing. Ampersand first, or the others double up.
    private static string HtmlEncode(string? value) => (value ?? string.Empty)
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

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
