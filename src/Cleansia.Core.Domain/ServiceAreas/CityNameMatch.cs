using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cleansia.Core.Domain.ServiceAreas;

/// <summary>
/// Whether a customer's address is in the serviced city a <c>ServiceCity</c> row names. The iOS, Android
/// and customer web copies mirror this rule exactly and pin the same case table; a copy stricter than
/// this one refuses a booking the server would take.
///
/// <para><b>A row with a ZIP prefix answers by the postcode.</b> When the address carries a valid
/// postcode — five digits once every whitespace character is removed — the row serves it exactly when
/// the postcode starts with the prefix, whatever the city text says; only an address without a valid
/// postcode falls back to the name. The prefix is enforced for Prague alone (owner ruling 2026-10-25):
/// every Prague postcode starts with 1 and no other Czech one does, while the other cities' postal
/// regions cover their neighbours too.</para>
///
/// <para><b>The name strip runs on the CUSTOMER's string only.</b> By name, a row named <c>Praha 8</c>
/// serves only Praha 8 and its quarters, never the whole city; a row with a ZIP prefix answers by the
/// postcode first, as above.</para>
///
/// <para><b>An okres is not a city part.</b> <c>Praha-západ</c>, <c>Praha-východ</c>, <c>Brno-venkov</c>,
/// <c>Plzeň-sever</c> and <c>Plzeň-jih</c> have the shape of a dashed city part but are the rural
/// districts around the city; stripping them would serve the countryside on the strength of the city's
/// row.</para>
///
/// <para><b>Exonyms are data, not algorithm.</b> <c>Prague</c> matches only a <c>Prague</c> row, and
/// <c>Прага</c> matches by name nothing at all.</para>
/// </summary>
public static class CityNameMatch
{
    private const int PostcodeLength = 5;

    private static readonly (string City, string Ring)[] OkresRings =
    [
        ("praha", "zapad"),
        ("praha", "vychod"),
        ("brno", "venkov"),
        ("plzen", "sever"),
        ("plzen", "jih"),
    ];

    private static readonly Regex TrailingDistrict = new(
        @"^(?<base>.+) (?:\d{1,2}|[ivx]{1,6})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    public static bool Serves(string? servicedCityName, string? zipPrefix, string? customerCity, string? customerZipCode)
    {
        if (!string.IsNullOrWhiteSpace(zipPrefix) && PostcodeOf(customerZipCode) is { } postcode)
        {
            return postcode.StartsWith(zipPrefix.Trim(), StringComparison.Ordinal);
        }

        return Matches(servicedCityName, customerCity);
    }

    public static bool Matches(string? servicedCityName, string? customerCity)
    {
        var row = Fold(servicedCityName);
        var city = Fold(customerCity);

        if (row.Length == 0 || city.Length == 0)
        {
            return false;
        }

        return CandidatesOf(city).Contains(row, StringComparer.Ordinal);
    }

    private static string? PostcodeOf(string? zipCode)
    {
        if (zipCode is null)
        {
            return null;
        }

        var compact = string.Concat(zipCode.Where(character => !char.IsWhiteSpace(character)));
        return compact.Length == PostcodeLength && compact.All(char.IsAsciiDigit) ? compact : null;
    }

    /// <summary>
    /// The city itself, the city without the part after its first dash (unless that names an okres), and
    /// each of those without a trailing district number or Roman numeral.
    /// </summary>
    private static IEnumerable<string> CandidatesOf(string city)
    {
        string[] forms = WithoutCityPart(city) is { } head ? [city, head] : [city];
        foreach (var form in forms)
        {
            yield return form;

            var district = TrailingDistrict.Match(form);
            if (district.Success)
            {
                yield return district.Groups["base"].Value;
            }
        }
    }

    private static string? WithoutCityPart(string city)
    {
        var dash = city.IndexOf('-');
        if (dash < 0)
        {
            return null;
        }

        var head = city[..dash].Trim();
        var part = city[(dash + 1)..].Trim();
        if (head.Length == 0 || part.Length == 0)
        {
            return null;
        }

        var partFirstWord = part.Split(' ')[0];
        return OkresRings.Contains((head, partFirstWord)) ? null : head;
    }

    /// <summary>
    /// Trim, strip diacritics, lowercase, write every dash as a hyphen and collapse every whitespace run,
    /// a no-break space included, to one space.
    /// </summary>
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
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(IsDash(character) ? '-' : character);
        }

        var stripped = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return WhitespaceRun.Replace(stripped, " ");
    }

    private static bool IsDash(char character) => character is (>= '\u2010' and <= '\u2015') or '\u2212';
}
