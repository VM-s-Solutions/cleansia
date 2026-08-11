using System.Text.RegularExpressions;

namespace Cleansia.Core.Domain.Payouts;

/// <summary>ISO 9362 business identifier codes (ADR-0034 D4) — 8 or 11 characters, e.g. <c>RZBCCZPP</c>.</summary>
public static partial class SwiftBic
{
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static bool IsWellFormed(string? value) => ShapePattern().IsMatch(Normalize(value));

    /// <summary>Characters 5–6 of a BIC are the ISO 3166 alpha-2 country of the institution.</summary>
    public static string? CountryOf(string? value)
    {
        var normalized = Normalize(value);
        return IsWellFormed(normalized) ? normalized.Substring(4, 2) : null;
    }

    [GeneratedRegex("^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex ShapePattern();
}
