namespace Cleansia.Core.Domain.Payouts;

/// <summary>
/// ISO 13616 structure + ISO 7064 MOD 97-10 check digits, and the CZ/SK composition that makes the IBAN
/// a derived value rather than a collected one (ADR-0034 D5.2).
/// </summary>
public static class IbanCalculator
{
    private const int MinLength = 15;
    private const int MaxLength = 34;

    /// <summary>
    /// ISO 13616 registry lengths for the markets this platform can plausibly pay into. A country absent
    /// from the table falls back to the generic 15–34 bound rather than being rejected — an IBAN is
    /// self-describing enough for its own checksum without our configuration.
    /// </summary>
    private static readonly Dictionary<string, int> RegistryLengths = new(StringComparer.Ordinal)
    {
        ["AT"] = 20, ["BE"] = 16, ["BG"] = 22, ["CH"] = 21, ["CY"] = 28, ["CZ"] = 24, ["DE"] = 22,
        ["DK"] = 18, ["EE"] = 20, ["ES"] = 24, ["FI"] = 18, ["FR"] = 27, ["GB"] = 22, ["GR"] = 27,
        ["HR"] = 21, ["HU"] = 28, ["IE"] = 22, ["IT"] = 27, ["LT"] = 20, ["LU"] = 20, ["LV"] = 21,
        ["MT"] = 31, ["NL"] = 18, ["NO"] = 15, ["PL"] = 28, ["PT"] = 25, ["RO"] = 24, ["SE"] = 24,
        ["SI"] = 19, ["SK"] = 24, ["UA"] = 29,
    };

    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static string? CountryPrefixOf(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && char.IsAsciiLetter(normalized[1])
            ? normalized[..2]
            : null;
    }

    public static int? RegistryLengthFor(string? countryAlpha2) =>
        countryAlpha2 is not null && RegistryLengths.TryGetValue(countryAlpha2.ToUpperInvariant(), out var length)
            ? length
            : null;

    public static bool IsValid(string? value)
    {
        var normalized = Normalize(value);

        if (normalized.Length is < MinLength or > MaxLength)
        {
            return false;
        }

        if (!char.IsAsciiLetterUpper(normalized[0]) || !char.IsAsciiLetterUpper(normalized[1]) ||
            !char.IsAsciiDigit(normalized[2]) || !char.IsAsciiDigit(normalized[3]))
        {
            return false;
        }

        var expected = RegistryLengthFor(normalized[..2]);
        if (expected is not null && normalized.Length != expected)
        {
            return false;
        }

        return Mod97(normalized[4..] + normalized[..4]) == 1;
    }

    /// <summary>
    /// CZ/SK: country + check digits + bank code (4) + zero-padded prefix (6) + zero-padded account (10).
    /// The parts are the source of truth; this is the only place the IBAN comes from.
    /// </summary>
    public static string ComposeCzsk(string countryAlpha2, string bankCode, string? accountPrefix, string accountNumber)
    {
        var bban = bankCode.PadLeft(4, '0')
                   + (accountPrefix ?? string.Empty).PadLeft(CzskAccountNumber.PrefixWidth, '0')
                   + accountNumber.PadLeft(CzskAccountNumber.NumberWidth, '0');

        var country = countryAlpha2.ToUpperInvariant();
        var checkDigits = 98 - Mod97(bban + country + "00");

        return $"{country}{checkDigits:D2}{bban}";
    }

    private static int Mod97(string value)
    {
        var remainder = 0;
        foreach (var character in value)
        {
            remainder = char.IsAsciiDigit(character)
                ? (remainder * 10 + (character - '0')) % 97
                : (remainder * 100 + (character - 'A' + 10)) % 97;
        }

        return remainder;
    }
}
