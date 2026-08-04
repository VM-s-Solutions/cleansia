namespace Cleansia.Core.Domain.Internationalization;

/// <summary>
/// <see cref="Country.IsoCode"/> is stored alpha-3 by the seed data ("CZE") and alpha-2 by several
/// fixtures ("CZ"), while an IBAN's country prefix and a BIC's characters 5–6 are alpha-2 by ISO 13616
/// and ISO 9362. Payout validation compares the two, so it needs one place that reconciles them.
/// <para>An unmapped code resolves to <c>null</c>, which every payout caller treats as "not open for
/// payouts" — the fail-closed default (ADR-0034 D4).</para>
/// </summary>
public static class CountryIsoCode
{
    private static readonly Dictionary<string, string> Alpha3ToAlpha2 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AUT"] = "AT", ["BEL"] = "BE", ["BGR"] = "BG", ["CAN"] = "CA", ["CHE"] = "CH", ["CYP"] = "CY",
        ["CZE"] = "CZ", ["DEU"] = "DE", ["DNK"] = "DK", ["ESP"] = "ES", ["EST"] = "EE", ["FIN"] = "FI",
        ["FRA"] = "FR", ["GBR"] = "GB", ["GRC"] = "GR", ["HRV"] = "HR", ["HUN"] = "HU", ["IRL"] = "IE",
        ["ITA"] = "IT", ["LTU"] = "LT", ["LUX"] = "LU", ["LVA"] = "LV", ["MLT"] = "MT", ["NLD"] = "NL",
        ["NOR"] = "NO", ["POL"] = "PL", ["PRT"] = "PT", ["ROU"] = "RO", ["RUS"] = "RU", ["SVK"] = "SK",
        ["SVN"] = "SI", ["SWE"] = "SE", ["UKR"] = "UA", ["USA"] = "US",
    };

    private static readonly Dictionary<string, string> Alpha2ToAlpha3 =
        Alpha3ToAlpha2.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    public static string? ToAlpha3(string? isoCode)
    {
        var alpha2 = ToAlpha2(isoCode);
        return alpha2 is not null && Alpha2ToAlpha3.TryGetValue(alpha2, out var alpha3) ? alpha3 : null;
    }

    public static string? ToAlpha2(string? isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode))
        {
            return null;
        }

        var trimmed = isoCode.Trim().ToUpperInvariant();

        return trimmed.Length switch
        {
            2 when trimmed.All(char.IsAsciiLetterUpper) => trimmed,
            3 when Alpha3ToAlpha2.TryGetValue(trimmed, out var alpha2) => alpha2,
            _ => null,
        };
    }
}
