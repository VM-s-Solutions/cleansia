namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Renders an e-mail body from the repository's own templates rather than from a
/// hosted SendGrid template. → /architecture/backend
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <param name="templateName">File name as it appears in <c>email-templates/</c>, e.g. <c>promo-code.html</c>.</param>
    /// <param name="values">Placeholder values, each HTML-encoded and never scanned for placeholders, so a value is always text, never markup and never another key; a missing key renders as empty rather than leaving braces.</param>
    string Render(string templateName, IReadOnlyDictionary<string, string?> values);
}
