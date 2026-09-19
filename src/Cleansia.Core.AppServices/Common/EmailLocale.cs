namespace Cleansia.Core.AppServices.Common;

/// <summary>
/// The locale an e-mail is rendered in: the recipient's preferred language when the copy exists in
/// it, English otherwise. The five are the locales every in-code default and every admin locale
/// bundle carry; a sixth on a user row is a stored preference nothing can render yet.
/// </summary>
public static class EmailLocale
{
    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "en", "cs", "sk", "uk", "ru" };

    public static string Resolve(string? preferredLanguageCode) =>
        !string.IsNullOrWhiteSpace(preferredLanguageCode) && Supported.Contains(preferredLanguageCode)
            ? preferredLanguageCode.ToLowerInvariant()
            : Constants.Language.English;
}
