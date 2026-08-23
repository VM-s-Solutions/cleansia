using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cleansia.Core.Domain.ServiceAreas;

/// <summary>
/// Whether a customer's typed or geocoded city is the serviced city a <c>ServiceCity</c> row names.
///
/// <para><b>Why an exact compare was not enough.</b> The gate used to be
/// <c>row.Name.ToLower() == city.Trim().ToLower()</c>, which refuses a booking for any spelling the
/// operator did not seed verbatim. That is not hypothetical: this repo's own seed puts a customer at
/// <c>'Plzen'</c> (<c>sql-scripts/seed/insert_addresses.sql:39</c>) while the serviced row is
/// <c>'Plzeň'</c> (<c>insert_seed_data.sql:358</c>), so that address cannot be booked. Reverse geocoders
/// and hand-typing both drop diacritics, and both append districts.</para>
///
/// <para><b>The rule is MONOTONE — it accepts a strict superset of the exact compare.</b> Nothing that
/// books today stops booking. That is what makes it safe to ship on its own.</para>
///
/// <para><b>The district strip is one-directional and that is load-bearing.</b> It is applied only to
/// the CUSTOMER's string, never to the row's name. So a row named <c>Praha 8</c> continues to serve
/// only Praha 8 and does not quietly become a claim over the whole city — an operator who seeded a
/// single district meant a single district.</para>
///
/// <para><b>Exonyms are data, not algorithm.</b> <c>Prague</c> matches nothing but a <c>Prague</c> row,
/// and <c>Прага</c> matches nothing at all until somebody seeds it. Folding those in code would mean
/// shipping a translation table that goes stale the first time a market opens; a row is the honest
/// place for it, and the admin surface already creates rows.</para>
/// </summary>
public static class CityNameMatch
{
    /// <summary>
    /// A trailing 1–2 digit district, optionally followed by a quarter after a dash —
    /// <c>Praha 8</c>, <c>Praha 4 - Chodov</c>, <c>Praha 5 – Smíchov</c>.
    ///
    /// <para>A dash with NO leading number is deliberately not matched. <c>Praha-západ</c> and
    /// <c>Brno-venkov</c> have that exact shape and are <i>okresy</i> — the rural districts AROUND the
    /// city, not parts of it. Stripping them would serve the countryside on the strength of a row for
    /// the city.</para>
    /// </summary>
    private static readonly Regex DistrictSuffix = new(
        @"^(?<base>\S.*?)\s+\d{1,2}(\s*[-–—]\s*\S.*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    public static bool Matches(string? servicedCityName, string? customerCity)
    {
        var row = Fold(servicedCityName);
        var city = Fold(customerCity);

        if (row.Length == 0 || city.Length == 0)
        {
            return false;
        }

        return row == city || row == StripDistrict(city);
    }

    /// <summary>Trim, strip diacritics, lowercase, and collapse internal whitespace runs to one space.</summary>
    private static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var stripped = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return WhitespaceRun.Replace(stripped, " ");
    }

    /// <summary>Never returns empty — a bare "8" keeps its own shape and simply matches nothing.</summary>
    private static string StripDistrict(string folded)
    {
        var match = DistrictSuffix.Match(folded);
        return match.Success ? match.Groups["base"].Value : folded;
    }
}
